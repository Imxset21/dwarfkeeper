using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using DwarfListener;
using DwarfCMD;
using DwarfData;

using Isis;

namespace DwarfLogger
{
	[Serializable]
	// internal
	public class DwarfLogRecord
	{
		public Queue<DwarfCommand> cmdqueue;
		public int lastMsgNum;

		public DwarfLogRecord(Queue<DwarfCommand> q, int num)
		{
			this.cmdqueue = q;
			this.lastMsgNum = num;
		}
	}

	
	public class DwarfLogger : DwarfListener.DwarfListener
	{
		public const string DEFAULT_LOG_FILE_NAME = "dwarf_log.dat";
		public const string DEFAULT_TREE_ROOT_PATH = "TreeData";
		const int DEFAULT_QUEUE_BACKLOG = 5;

		private string logFileName;
		private string treeRootPath;
		private int globalMsgNum;
		
		public DwarfLogger(string groupname) : this(groupname, DEFAULT_LOG_FILE_NAME, DEFAULT_TREE_ROOT_PATH, 0)
		{
			//#2spooky
		}

        /**
         * Create a new DwarfLogger.
         *
         * @param groupname The group that this will be logging for (actual Isis2
         *  groupname will be differentiated automatically)
         */
		public DwarfLogger(string groupname, string logFilename, string treeRootPath, int currMsgNum) : base(groupname)
		{
			this.cmdqueue = new Queue<DwarfCommand>();
			this.logFileName = logFilename;
			this.treeRootPath = treeRootPath;
			this.globalMsgNum = currMsgNum;

            base.dwarfGroup.Handlers[(int)IsisDwarfCode.UPDATE] +=
                (dwarfIsisHandler)delegate(DwarfCommand cmd)
                {
                    ThreadStart ts = 
                        new ThreadStart(() =>
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args));

                    Thread t = new Thread(ts);
                    t.Start();
                };


            // We want to give new servers our address for a P2P state transfer
            base.dwarfGroup.Handlers[(int)IsisDwarfCode.NEW] += 
                (Action)delegate() {
                    dwarfGroup.Reply(base.address);
                };

            base.dwarfSubGroup = new Group(groupname + "_logger");
		}			


        public void start() {
            base.dwarfGroup.Join();
            base.dwarfSubGroup.Join();
        }


		private void writeLog(Queue<DwarfCommand> cmdQueue, int msgNum)
		{ DwarfLogRecord lr = new DwarfLogRecord(cmdQueue, msgNum);
			BinaryFormatter b = new BinaryFormatter();

			using(FileStream logFileStream = new FileStream(
					this.logFileName,
					FileMode.Create,
					FileAccess.ReadWrite
					))
			{
				b.Serialize(logFileStream, lr);
			}
		}

        protected override void initDwarfHandlers() {
            dwarfOps = new Dictionary<DwarfCode, dwarfOpHandler>();

            dwarfOps.Add(DwarfCode.CREATE, (dwarfOpHandler) create);
            dwarfOps.Add(DwarfCode.DELETE, (dwarfOpHandler) delete);
            dwarfOps.Add(DwarfCode.SET_NODE, (dwarfOpHandler) setNode);
        }


        private void writeTree(DwarfTree tree, int msgnum) {
            string info_path = 
                Path.Combine(Directory.GetCurrentDirectory(),  DEFAULT_TREE_ROOT_PATH);
            writeTree(tree, msgnum, info_path);
        }

		private void writeTree(DwarfTree tree, int msgNum, string rootpath)
		{
            string info_path = Path.Combine(rootpath, "Info.dat");

            // Create the root directory if it exists
            if(!Directory.Exists(rootpath)) {
                Directory.CreateDirectory(rootpath);
            }

            // Write the msgNum to info file
            File.WriteAllText(info_path, "MSGNUM: " + msgNum);

            //write the tree
            tree.writeTree(rootpath);
		}

        public static DwarfTree loadTreeRecord(string rootpath) {
            return DwarfTree.loadTree(rootpath);
        }

		private void appendToLog(DwarfCommand cmd)
		{
			base.cmdqueue.Enqueue(cmd);
			this.globalMsgNum += 1;

			if (base.cmdqueue.Count >= DEFAULT_QUEUE_BACKLOG)
			{
				// Swap because we're doing this async
				Queue<DwarfCommand> tmpQ = this.cmdqueue;
				
				DwarfTree tmpT = new DwarfTree(this.nodeSys);

				this.cmdqueue = new Queue<DwarfCommand>();

				ThreadStart ts = new ThreadStart(() => writeLog(tmpQ, this.globalMsgNum));
				ThreadStart tr = 
                    new ThreadStart(() => 
                        writeTree(
                            tmpT, 
                            this.globalMsgNum, 
                            Directory.GetCurrentDirectory() + "/TreeData"
                        )
                    );

				Thread t1 = new Thread(ts);
				Thread t2 = new Thread(tr);
				t1.Start();
				t2.Start();
			}
		}

        /** Dummy create method to log create commands.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query
         */
		protected override void create(string args)
		{
            /*
              Split the args string into constitutent
              path and data arguments.
            */
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
      
            bool success = this.nodeSys.addNode(path, data);
        
            //TODO: send update to rest of group
            if(success) {
                this.appendToLog(new DwarfCommand((int)DwarfCode.CREATE, args));
            }
		}
		
		/**
		 * Deletes the selected node from the tree.
		 *
		 * This method performs an in-order query to all the other
		 * servers in the group before deleting the data.
		 * The delete only succeeds if all other servers
		 * have responded correctly to the query, including this one.
		 *
		 * The local parameter is used to distinguish between 
		 * two situations where this method is called:
		 * 
		 * 1) The client invokes us externally. In this case,
		 *    local is FALSE, and we send out an ordered query
		 *    to all other servers in the group, including 
		 *    this server. 
		 * 2) We've been invoked by another server's query
		 *    or our own. In either case, we attempt to delete
		 *    nodes on our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query.
		 *
		 */
		protected override void delete(string args)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
			
            string path = argslst[0];

            /*
              Actually deletes the node in this server's local tree.
              
              We are responding to an Isis query, which may or may not
              have originated from this very server, but we cannot
              assume either case.
            */
        
            bool success = nodeSys.removeNode(path);

            if(success) {
                this.appendToLog(new DwarfCommand((int)DwarfCode.DELETE, args));
            }
		}

       
		/**
		 * Sets the value of a given node.
		 * 
		 * This method performs an in-order query to all the other
		 * servers in the group before setting the node.
		 * The set only succeeds if all other servers
		 * have responded correctly to the query, including this one.
		 *
		 * The local parameter is used to distinguish between 
		 * two situations where this method is called:
		 * 
		 * 1) The client invokes us externally. In this case,
		 *    local is FALSE, and we send out an ordered query
		 *    to all other servers in the group, including 
		 *    this server. 
		 * 2) We've been invoked by another server's query
		 *    or our own. In either case, we attempt to delete
		 *    nodes on our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query.
		 *
		 */

		protected override void setNode(string args) {
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args)) {
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
			
            /*
              Actually deletes the node in this server's local tree.
              
              We are responding to an Isis query, which may or may not
              have originated from this very server, but we cannot
              assume either case.
            */
            bool success = nodeSys.setData(path, data);

            if(success) {
                this.appendToLog(new DwarfCommand((int)DwarfCode.SET_NODE, args));
            }
		}


		static void Main(string[] args)
		{
			//TODO add better command line argument for fast start
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]) && args[0].Equals("fast")) 
			{
				IsisSystem.Start(true);
			} else {
				IsisSystem.Start();
			}
			
			DwarfLogger my_server = new DwarfLogger("dwarfkeeper");
            my_server.start();
			
			Console.WriteLine("Waiting for client connection...");
		
			my_server.printTreeLoop();
		
			IsisSystem.WaitForever();
		}

	}
}
