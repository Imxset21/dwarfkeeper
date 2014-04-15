using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using DwarfServer;
using DwarfCMD;
using DwarfData;

using Isis;

namespace DwarfLogger
{
	[Serializable()]
	internal class DwarfLogRecord
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
	internal class DwarfTreeRecord
	{
		public DwarfTree tree;
		public int lastMsgNum;

		public DwarfTreeRecord(DwarfTree t, int num)
		{
			this.tree = t;
			this.lastMsgNum = num;
		}
	}
	
	public class DwarfLogger : DwarfServer.DwarfServer
	{
		const string DEFAULT_LOG_FILE_NAME = "dwarf_log.dat";
		const string DEFAULT_TREE_FILENAME = "dwarf_tree.dat";
		const int DEFAULT_QUEUE_BACKLOG = 50;

		private Queue<DwarfCommand> commandQueue;
		private FileStream logFileStream;
		private FileStream treeFileStream;
		private int globalMsgNum;
		
		public DwarfLogger() : this(DEFAULT_LOG_FILE_NAME, DEFAULT_TREE_FILENAME)
		{
			//#2spooky
		}

		public DwarfLogger(string logFilename, string treeFilename) : base()
		{
			this.commandQueue = new Queue<DwarfCommand>();
			this.logFileStream = 
				new FileStream(
					logFilename,
					FileMode.Create,
					FileAccess.ReadWrite
				);
			this.treeFileStream = 
				new FileStream(
					treeFilename,
					FileMode.Create,
					FileAccess.ReadWrite
				);
		}

		private static void writeLog(Queue<DwarfCommand> cmdQueue, int msgNum)
		{
			DwarfLogRecord lr = new DwarfLogRecord(cmdQueue, msgNum);
			BinaryFormatter b = new BinaryFormatter();
			b.Serialize(this.logFileStream, lr);
		}

		private static void writeTree(DwarfTree tree, int msgNum)
		{
			DwarfTreeRecord tr = new DwarfTreeRecord(tree, msgNum);
			BinaryFormatter b = new BinaryFormatter();
			b.Serialize(this.treeFileStream, tr);
		}

		private void appendToLog(DwarfCommand cmd)
		{
			this.commandQueue.Enqueue(cmd);

			if (this.commandQueue.Count >= DEFAULT_QUEUE_BACKLOG)
			{
				// Swap because we're doing this async
				Queue<DwarfCommand> tmpQ = this.commandQueue;
				
				// @TODO: Make copy constructor for DwarfTree so we can serialize it
				DwarfTree tmpT = //base.nodeSys;

				this.commandQueue = new Queue<DwarfCommand>();
				

				ThreadStart ts = new ThreadStart(() => writeLog(tmpQ, this.globalMsgNum));
				ThreadStart tr = new ThreadStart(() => writeTree(

				Thread t = new Thread(ts);
				t.Start();
			}
		}

        /** Dummy create method to log create commands.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query
         */
		protected override void create(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to create.");
				return;
			}

			if (!local)
			{
				dwarfGroup.Reply("Log server cannot issue commands.");
			} else {
				/*
				  Split the args string into constitutent
				  path and data arguments.
				*/
				string[] argslst = args.Split();
				if(argslst.Length < 2) {
					dwarfGroup.Reply("Too few arguments provided to create.");
					return;
				}
				string path = argslst[0];
				string data = argslst[1];
          
				bool success = this.nodeSys.addNode(path, data);
            
				//TODO: send update to rest of group
				if(success) {
					dwarfGroup.Reply(path);
				} else {
				
					string err = string.Format(
						"Error: Failed to create node {0}, with data {1}",
						path, data);
					dwarfGroup.Reply(err);
				}
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
		protected override void delete(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to delete.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
				dwarfGroup.Reply("Too few arguments provided to delete.");
                return;
            }
			
            string path = argslst[0];

			/*
			  A request is considered local if we're the point of origin
			  for this request,  i.e. the client has issued a delete to us.
			*/
            if(!local) {
				dwarfGroup.Reply("Log server cannot issue commands.");
            } else {
				/*
				  Actually deletes the node in this server's local tree.
				  
				  We are responding to an Isis query, which may or may not
				  have originated from this very server, but we cannot
				  assume either case.
				*/
            
				bool success = nodeSys.removeNode(path);

				if(success) {
					dwarfGroup.Reply(path);
				} else {
					string err = string.Format("Error: Failed to delete node {0}", path);
					dwarfGroup.Reply(err);
				}
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
		protected override void getNode(string args, bool local=false)
		{
			dwarfGroup.Reply("Data reads are not supported on the log server.");
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

		protected override void setNode(string args, bool local=false)
		{
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to setNode.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                dwarfGroup.Reply("Too few arguments provided to setNode.");
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
			
			/*
			  A request is considered local if we're the point of origin
			  for this request,  i.e. the client has issued a delete to us.
			*/
            if(!local) {
				dwarfGroup.Reply("Too few arguments provided to setNode.");
            } else {
				/*
				  Actually deletes the node in this server's local tree.
				  
				  We are responding to an Isis query, which may or may not
				  have originated from this very server, but we cannot
				  assume either case.
				*/
				bool success = nodeSys.setData(path, data);

				if(success) {
					dwarfGroup.Reply(data);
				} else {
					String err = string.Format(
                        "Failed to set data to {0} at node {1}",
                        data, path);
					dwarfGroup.Reply(err);
				}
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
		protected override void exists(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to exists.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply("No Arguments Provided to exists.");
                return;
            }

            DwarfStat stats = nodeSys.getNodeInfo(argslst[0]);

            if(null != stats) {
                stats.includeData();
                dwarfGroup.Reply(stats);
            } else {
                stats = new DwarfStat(argslst[0] + " does not exist");
                dwarfGroup.Reply(stats);
            }
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
        protected override void getChildren(string args, bool local=false) 
		{
			dwarfGroup.Reply("Reads are not supported in the log server.");
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
		protected override void getChildren2(string args, bool local=false)
		{
			dwarfGroup.Reply("Reads are not supported in the log server.");
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

        protected override void getNodeAll(string args, bool local=false)
		{
			dwarfGroup.Reply("Reads are not supported in the log server.");
		}
	
		protected override void sync(string args)
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
			
			DwarfLogger my_server = new DwarfLogger();
			
			Console.WriteLine("Waiting for client connection...");
		
			my_server.printTreeLoop();
		
			IsisSystem.WaitForever();
		}

	}

}