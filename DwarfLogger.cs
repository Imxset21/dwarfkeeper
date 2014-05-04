using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using DwarfListener;
using DwarfServer;
using DwarfCMD;
using DwarfData;

using Isis;

namespace DwarfLogger
{
	[Serializable()]
	// internal
	public class DwarfLogRecord
	{
		public Queue<DwarfCommand> commandQueue;
		public int lastMsgNum;

		public DwarfLogRecord(Queue<DwarfCommand> q, int num)
		{
			this.commandQueue = q;
			this.lastMsgNum = num;
		}
	}

	[Serializable()]
	// internal
	public struct DwarfTreeRecord
	{
		public DwarfTree tree;
		public int lastMsgNum;

		public DwarfTreeRecord(DwarfTree t, int num)
		{
			this.tree = t;
			this.lastMsgNum = num;
		}
	}
	
	public class DwarfLogger : DwarfListener.DwarfListener
	{
		public const string DEFAULT_LOG_FILE_NAME = "dwarf_log.dat";
		public const string DEFAULT_TREE_FILENAME = "dwarf_tree.dat";
		const int DEFAULT_QUEUE_BACKLOG = 5;

		private Queue<DwarfCommand> commandQueue;
		private string logFileName;
		private string treeFileName;
		private int globalMsgNum;
		
		public DwarfLogger(string groupname) : this(groupname, DEFAULT_LOG_FILE_NAME, DEFAULT_TREE_FILENAME, 0)
		{
			//#2spooky
		}

        /**
         * Create a new DwarfLogger.
         *
         * @param groupname The group that this will be logging for (actual Isis2
         *  groupname will be differentiated automatically)
         */
		public DwarfLogger(string groupname, string logFilename, string treeFilename, int currMsgNum) : base(groupname)
		{
			this.commandQueue = new Queue<DwarfCommand>();
			this.logFileName = logFilename;
			this.treeFileName = treeFilename;
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

            base.dwarfSubGroup = new Group(groupname + "_logger");
		}			


        public void start() {
            base.dwarfGroup.Join();
            base.dwarfSubGroup.Join();
        }


		private void writeLog(Queue<DwarfCommand> cmdQueue, int msgNum)
		{
			DwarfLogRecord lr = new DwarfLogRecord(cmdQueue, msgNum);
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


		private void writeTree(DwarfTree tree, int msgNum)
		{
			DwarfTreeRecord tr = new DwarfTreeRecord(tree, msgNum);
			
			using(FileStream fs = new FileStream(
					"tr.tmp",
					FileMode.Create,
					FileAccess.ReadWrite
					))
			{
                BinaryFormatter b = new BinaryFormatter();
				b.Serialize(fs, tr);
                fs.Flush(true);
                System.IO.File.Copy("tr.tmp", this.treeFileName, true);
                System.IO.File.Delete("tr.tmp");
			}
            tree.printTree();
		}

        public static void loadTreeRecord(string filename) {
            DwarfTreeRecord dtr;
            using(Stream s = File.Open(
                        filename,
                        FileMode.Open,
                        FileAccess.Read))
            {
                BinaryFormatter b = new BinaryFormatter();
                object o= b.Deserialize(s);
                try {
                    dtr = (DwarfTreeRecord)o;
                    dtr.tree.printTree();
                    Console.WriteLine(dtr.lastMsgNum);
                } catch {
                    Console.WriteLine("GOOMAB");
                }
            }
        }

		private void appendToLog(DwarfCommand cmd)
		{
			this.commandQueue.Enqueue(cmd);
			this.globalMsgNum += 1;

			if (this.commandQueue.Count >= DEFAULT_QUEUE_BACKLOG)
			{
				// Swap because we're doing this async
				Queue<DwarfCommand> tmpQ = this.commandQueue;
				
				// @TODO: Make copy constructor for DwarfTree so we can serialize it
				DwarfTree tmpT = new DwarfTree(this.nodeSys);

				this.commandQueue = new Queue<DwarfCommand>();

				ThreadStart ts = new ThreadStart(() => writeLog(tmpQ, this.globalMsgNum));
				ThreadStart tr = new ThreadStart(() => writeTree(tmpT, this.globalMsgNum));

				Thread t1 = new Thread(ts);
				Thread t2 = new Thread(tr);
				t1.Start();
				t2.Start();
				//this.nodeSys.writeTree(this.treeFileName);
				//this.nodeSys = DwarfTree.loadTree(this.treeFileName);
				//this.nodeSys.printTree();
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
		 * Gets the value of a given node.
		 * 
		 * Note that we only support local gets, so the local parameter
		 * is ignored.
		 *
		 * @param[in]    args    Path to node to get value of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected override void getNode(string args)
		{
            ;
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
		
		/**
		 * Checks if the a given node exists.
		 * 
		 * Note that we only support local exists, so the local parameter
		 * is ignored.
		 *
		 * @param[in]    args    Path to node to check existance of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected override void exists(string args)
		{
            ;
		}

		/**
		 * Gets the children of a particular node.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children of.
		 * @param[in]    local   Ignored in this function.
		 */
        protected override void getChildren(string args) 
		{
            ;
        }
		
		/**
		 * Gets the children of a particular node.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected override void getChildren2(string args)
		{
            ;
		}
		
		/**
		 * Gets the children of a particular node and their data.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children and their data from.
		 * @param[in]    local   Ignored in this function.
		 */

        protected override void getNodeAll(string args)
		{
            ;
		}
	
		protected void sync(string args)
		{
			throw new NotImplementedException("Sync is not implemented.");
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
