using System;
using System.Collections.Generic;
using System.Threading;

using DwarfCMD;
using DwarfData;
using DwarfListener;
using Isis;

namespace DwarfServer
{
	public class DwarfServer : DwarfListener.DwarfListener
	{
        private bool initializing = true;
        private string groupname;

		/////////////////////////
		//  Dwarf Server State //
		/////////////////////////

		//! Isis2 Server Group this server will join in addition to full group

        List<int> groupCommands = new List<int> {
            (int)DwarfCode.CREATE,
            (int)DwarfCode.DELETE,
            (int)DwarfCode.SET_NODE
        };

		/////////////////////////////// 
		// Dwarf Server Constructors //
		///////////////////////////////


        /**
         * Create a new DwarfServer instance, with the given groupname.
         *
         * @param groupname The name of the group for this server to join
         */
		public DwarfServer(string groupname) : base(groupname)
		{
            this.nodeSys = DwarfTree.CreateTree();
            this.cmdqueue = new Queue<DwarfCommand>();
            this.groupname = groupname;

            dwarfGroup.Handlers[(int)DwarfCMD.IsisDwarfCode.UPDATE] += 
                                                    (dwarfIsisHandler)groupCmdHandler;
            this.initDwarfHandlers();

            // We don't want a new server talking to a DwarfServer
            base.dwarfGroup.Handlers[(int)IsisDwarfCode.NEW] += 
                (Action)delegate() {
                    dwarfGroup.Reply(IsisSystem.GetNullAddress());
                };
			
		}

        public void start() {
            dwarfGroup.Join();

            View curview = dwarfGroup.GetView();
            if (curview.GetSize() == 1) {
                List<Address> addrs = new List<Address>();
                dwarfGroup.OrderedQuery (
                    Group.ALL,
                    (int)IsisDwarfCode.NEW,
                    new EOLMarker(),
                    addrs
                );


                Predicate<Address> p = delegate(Address a) {
                    return !(a.Equals(IsisSystem.GetNullAddress()) || a.Equals(this.address));
                };
                Console.WriteLine("Before:");
                foreach (Address a in addrs) {
                    Console.WriteLine(a);
                }

                addrs = addrs.FindAll(p);

                Console.WriteLine("After:");
                foreach (Address a in addrs) {
                    Console.WriteLine(a);
                }
            }


            // TODO: Retrieve latest tree from Loggers
            Thread.Sleep(2000);
           
            dwarfSubGroup = new Group(this.groupname + "_server");

            // TODO: clear cmd queue
            initializing = false;
            
            dwarfSubGroup.Handlers[(int)IsisDwarfCode.OPCODE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd) 
				{
					// Thread will execute with this server's registered
					// delegate method for the opcode recieved.
                    ThreadStart ts = new ThreadStart(() => passCommand(cmd));
                    Thread t = new Thread(ts);
                    dwarfSubGroup.SetReplyThread(t);
                    t.Start();
                };

			// Only allow client requests of the OPCODE kind
            dwarfSubGroup.AllowClientRequests((int)IsisDwarfCode.OPCODE);
            dwarfSubGroup.Join();
        }


        private void groupCmdHandler(DwarfCommand cmd) {
            if(initializing) {
                lock (cmdqueue) {
                    cmdqueue.Enqueue(cmd);
                }
            } else {
                ThreadStart ts = 
                    new ThreadStart(() =>
                        dwarfOps[(DwarfCode)cmd.opCode](cmd.args));

                Thread t = new Thread(ts);
                dwarfGroup.SetReplyThread(t);
                t.Start();
            }
        }


		//////////////////////////////////////
		// Protected Initialization Methods //
		//////////////////////////////////////

        protected void passCommand(DwarfCommand cmd) 
        { 
            // If this is a command that causes changes in the data forward on to the full group
            List<DwarfStat> retlst = new List<DwarfStat>(); 

            if (groupCommands.Contains(cmd.opCode)) 
            {
                dwarfGroup.OrderedQuery (
                    Group.ALL,
                    (int)IsisDwarfCode.UPDATE,
                    cmd,
                    new EOLMarker(),
                    retlst
                );

                // Predicate to check that if a server returned an error.
                Predicate<DwarfStat> p = delegate(DwarfStat ds)
                {
                    return (null != ds) && string.IsNullOrWhiteSpace(ds.err);
                };

                if(retlst.TrueForAll(p)) {
                    dwarfSubGroup.Reply(retlst[0]);
                } else {
                    //TODO: handle/find error
                    dwarfSubGroup.Reply(new DwarfStat("some server had an error",error:true));
                }
            } else {
                dwarfOps[(DwarfCode)(cmd.opCode)](cmd.args);
            }
        }

		/**
		 * Initializes the dwarfOps dictionary with this class' methods.
		 */
        protected override void initDwarfHandlers()
		{
            dwarfOps = new Dictionary<DwarfCode, dwarfOpHandler>();
			
            dwarfOps.Add(DwarfCode.TEST, (dwarfOpHandler) test);
            dwarfOps.Add(DwarfCode.CREATE, (dwarfOpHandler) create);
            dwarfOps.Add(DwarfCode.DELETE, (dwarfOpHandler) delete);
            dwarfOps.Add(DwarfCode.GET_NODE, (dwarfOpHandler) getNode);
            dwarfOps.Add(DwarfCode.GET_CHILDREN, (dwarfOpHandler) getChildren);
            dwarfOps.Add(DwarfCode.GET_CHILDREN2, (dwarfOpHandler) getChildren2);
            dwarfOps.Add(DwarfCode.SET_NODE, (dwarfOpHandler) setNode);
            dwarfOps.Add(DwarfCode.GET_ALL, (dwarfOpHandler) getNodeAll);
            dwarfOps.Add(DwarfCode.EXISTS, (dwarfOpHandler) exists);
        }

        /**************************************************
         *
         * DwarfTree Manipulation Functions
         *
         **************************************************/

        
        /** Create the node at path - CLI command is "create".
		 * 
		 * This method performs an in-order query to all the other
		 * servers in the group before writing the new data.
		 * The write only succeeds if all other servers
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
		 *    or our own. In either case, we attempt to write
		 *    to our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query
         */
		protected override void create(string args)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply(new DwarfStat("Malformed/empty arguments to create.", error:true));
				return;
			}

			/*
			  Split the args string into constitutent
			 path and data arguments.
			 */
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                dwarfGroup.Reply(new DwarfStat("Too few arguments provided to create.", error:true));
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
            
            /*
              Actually performs the write to this server's local tree.
              
              We are responding to an Isis query, which may or may not
              have originated from this very server, but we cannot
              assume either case.
              
              @TODO: Support ACL inputs on adding new node
              @TODO: Support error messages on bad node add
            */
            
            bool success = this.nodeSys.addNode(path, data);
        
            if(success) {
            
                dwarfGroup.Reply(new DwarfStat(path));

                //TODO Remove this print
                nodeSys.printTree();

            } else {
            
                string err = string.Format(
                    "Error: Failed to create node {0}, with data {1}",
                    path, data);
                dwarfGroup.Reply(new DwarfStat(err, error:true));
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
				dwarfGroup.Reply(new DwarfStat("Malformed/empty arguments to create.", error:true));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
				dwarfGroup.Reply(new DwarfStat("Too few arguments provided to delete.", error:true));
                return;
            }
			
            string path = argslst[0];

            /*
              Actually deletes the node in this server's local tree.
              
              We are responding to an Isis query, which may or may not
              have originated from this very server, but we cannot
              assume either case.
                             
              @TODO: Enable Logging & user feedback
            */
        
            bool success = nodeSys.removeNode(path);

            if(success) {
                dwarfGroup.Reply(new DwarfStat(path));
            } else {
                string err = string.Format("Error: Failed to delete node {0}", path);
				dwarfGroup.Reply(new DwarfStat(err, error:true));
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
		protected void getNode(string args)
		{
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfSubGroup.Reply(
                        new DwarfStat("Malformed/empty arguments to getNode.", error:true));
				return;
			}

            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfSubGroup.Reply(
                        new DwarfStat("Too few arguments provided to getNode.",error:true));
                return;
            }
			
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null != stat) {
                stat.includeData();
                dwarfSubGroup.Reply(stat);
            } else {
                stat = new DwarfStat("Get Failed.", error:true);
                dwarfSubGroup.Reply(stat);
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

		protected override void setNode(string args)
		{
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
                dwarfGroup.Reply(new DwarfStat("Malformed/empty arguments to setNode."));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                dwarfGroup.Reply(new DwarfStat("Too few arguments provided to setNode."));
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
			
            /*
              Actually deletes the node in this server's local tree.
              
              We are responding to an Isis query, which may or may not
              have originated from this very server, but we cannot
              assume either case.
                             
              @TODO: Enable Logging & user feedback
            */
            bool success = nodeSys.setData(path, data);

            if(success) {
                dwarfGroup.Reply(new DwarfStat(path));
            } else {
                String err = string.Format(
                    "Failed to set data to {0} at node {1}",
                    data, path);
                dwarfGroup.Reply(new DwarfStat(err, error:true));
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
		protected void exists(string args)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
                dwarfSubGroup.Reply(new DwarfStat("Malformed/empty arguments to exists."));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfSubGroup.Reply(new DwarfStat("No Arguments Provided to exists.", error:true));
                return;
            }

            DwarfStat stats = nodeSys.getNodeInfo(argslst[0]);

            if(null != stats) {
                stats.includeData();
                dwarfSubGroup.Reply(stats);
            } else {
                stats = new DwarfStat(argslst[0] + " does not exist", error:true);
                dwarfSubGroup.Reply(stats);
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
        protected void getChildren(string args) 
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
                dwarfSubGroup.Reply(
                        new DwarfStat("Malformed/empty arguments to getChildren.",error:true));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            
            string[] childs = nodeSys.getChildList(argslst[0]);

            if(null == childs) {
                dwarfSubGroup.Reply(new DwarfStat("Error: Get Children Failed", error:true));
                return;
            }
           
            dwarfSubGroup.Reply(new DwarfStat(string.Join(",", childs)));
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
		protected void getChildren2(string args)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfSubGroup.Reply(
                        new DwarfStat("Malformed/empty arguments to getChildren2.", error:true));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfSubGroup.Reply(new DwarfStat("No Arguments Provided to getChildren2", error:true));
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfSubGroup.Reply(new DwarfStat("getChildren2 Failed", error:true));
                return;
            }
            stat.includeChildLst();
           
            dwarfSubGroup.Reply(stat);
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
        protected void getNodeAll(string args)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfSubGroup.Reply(
                        new DwarfStat("Malformed/empty arguments to getNodeAll.", error:true));
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfSubGroup.Reply(new DwarfStat("Error: No Arguments Provided to getNodeAll"));
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfSubGroup.Reply(new DwarfStat("Error: getNodeAll Failed"));
                return;
            }
            stat.includeChildLst();
            stat.includeData();
           
            dwarfSubGroup.Reply(stat);
       }

        protected void test(string args)
		{
            Console.WriteLine("TEST || " + args);
            dwarfSubGroup.Reply(new DwarfStat("0xDEADWARF-TEST"));
        }
		
		
		static void Main(string[] args)
		{
            //TODO add better command line argument for fast start
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]) && args[0].Equals("fast")){ 
                IsisSystem.Start(true);
            } else {
                IsisSystem.Start();
            }
			
			DwarfServer my_server = new DwarfServer("dwarfkeeper");
            my_server.start();

			Console.WriteLine ("Waiting for client connection...");

            my_server.printTreeLoop();
			
            IsisSystem.WaitForever();
		}
	}
}

