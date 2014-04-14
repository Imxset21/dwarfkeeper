using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;

using DwarfCMD;
using DwarfData;
using Isis;

namespace DwarfServer
{
    //TODO: Add logging of some sort to servers - Hook into Isis logging?
	public class DwarfServer
	{
		//! Handler delegate for Isis2 Commands from other server instances
		protected delegate void dwarfIsisHandler(DwarfCommand args);

		//! Handler delegate for opcodes from clients/other servers
		protected delegate void dwarfOpHandler(string args, bool local);
		
		const int DEFAULT_PORT_NUM = 9845;     //!< Default port number (Isis' default + 2)
		
		/////////////////////////
		//  Dwarf Server State //
		/////////////////////////

		//! Underlying node file system
        protected DwarfTree nodeSys;

		//! Op-Code to Method dictionary for handling requests
        protected Dictionary<DwarfCode, dwarfOpHandler> dwarfOps;

		//! Isis2 Group this server will join
        protected Group dwarfGroup;

		/////////////////////////////// 
		// Dwarf Server Constructors //
		///////////////////////////////

		/** 
		 * Starts the TCP server on localhost with port number.
		 */
		public DwarfServer()
		{
            this.nodeSys = DwarfTree.CreateTree();

            this.dwarfGroup = new Group("dwarfkeeper");

            this.defineOpHandlers();
            this.initDwarfHandlers();

            Isis.Msg.RegisterType(typeof(DwarfCommand), 111);
            Isis.Msg.RegisterType(typeof(DwarfStat), 113);

            this.dwarfGroup.Join();

			//TODO: What to do server is still active. No-op?
			//this.tcpServer.Start();
		}


		//////////////////////////////////// 
		// Private Initialization Methods //
		//////////////////////////////////// 

		/**
		 * Registers the Op-Code handlers with the Isis2 sub-system.
		 *  
		 * Each handler spins off a seperate thread to handle the
		 * reply asynchronously so that the handler returns as
		 * quickly as possible.
		 */
        private void defineOpHandlers()
		{
			// Register handler for when we get an OPCODE from the client,
			// i.e. when we get any DwarfKeeper command.
            dwarfGroup.Handlers[(int)IsisDwarfCode.OPCODE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd) 
				{
					// Thread will execute with this server's registered
					// delegate method for the opcode recieved.
                    ThreadStart ts =
                        new ThreadStart(() => 
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args, local:false));

                    Thread t = new Thread(ts);
                    dwarfGroup.SetReplyThread(t);
                    t.Start();
                };
			
			// Only allow client requests of the OPCODE kind
            dwarfGroup.AllowClientRequests((int)IsisDwarfCode.OPCODE);

			// Register handler for responding to other servers' updates
            dwarfGroup.Handlers[(int)IsisDwarfCode.UPDATE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd)
				{
                    ThreadStart ts = 
                        new ThreadStart(() =>
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args, local:true));

                    Thread t = new Thread(ts);
                    dwarfGroup.SetReplyThread(t);
                    t.Start();
                };
        }

		/**
		 * Initializes the dwarfOps dictionary with this class' methods.
		 */
        private void initDwarfHandlers()
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
		protected void create(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to create.");
				return;
			}

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
            
			/*
			  A request is considered local if we're the point of origin
			  for this request,  i.e. the client has issued a create to us.
			 */
            if(!local) {
                
				/* 
				   Perform an in-order query to the group to make
				   sure everyone gets this write request at the
				   same time in the global request stream.
				*/
				List<string> retlst = new List<string>();
                dwarfGroup.OrderedQuery(
					Group.ALL,
					(int)IsisDwarfCode.UPDATE,
					new DwarfCommand((int)DwarfCode.CREATE, args),
					new EOLMarker(),
					retlst
				);
				
				/* 
				   Use predicate to make sure all nodes have successfuly 
				   completed the request before replying to the client.
				   If any of them failed, return an error string.
				*/
                Predicate<string> p = delegate(string s){
					return !String.IsNullOrEmpty(s) && s.Equals(path);
				};

                if(retlst.TrueForAll(p)) 
				{
                    dwarfGroup.Reply(path); // Replies IFF all servers succeeded
                } else {
                    string err = string.Format(
                            "Error: Not all servers created node {0}",
                            path, data);
                    dwarfGroup.Reply(err);
                }
			
			} else {
				/*
				  Actually performs the write to this server's local tree.
				  
				  We are responding to an Isis query, which may or may not
				  have originated from this very server, but we cannot
				  assume either case.
				  
				  @TODO: Support ACL inputs on adding new node
				  @TODO: Support error messages on bad node add
				*/
				
				bool success = this.nodeSys.addNode(path, data);
            
				//TODO: send update to rest of group
				if(success) {
				
					dwarfGroup.Reply(path);

					//TODO Remove this print
					nodeSys.printTree();

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
		protected void delete(string args, bool local=false)
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
                
				/* 
				   Perform an in-order query to the group to make
				   sure everyone gets this delete request at the
				   same time in the global request stream.
				*/
                List<string> retlst = new List<string>();

                dwarfGroup.OrderedQuery(
					Group.ALL,
					(int)IsisDwarfCode.UPDATE,
					new DwarfCommand((int)DwarfCode.DELETE, args),
					new EOLMarker(),
					retlst
				);

				/* 
				   Use predicate to make sure all nodes have successfuly 
				   completed the request before replying to the client.
				   If any of them failed, return an error string.

				   @TODO: This could be a function since delete/create/set use it.
				*/
				Predicate<string> p = delegate(string s){
					return !String.IsNullOrEmpty(s) && s.Equals(path);
				};
				
                if(retlst.TrueForAll(p)) {
                    dwarfGroup.Reply(path);
                } else {
                    string err = string.Format(
						"Error: Not all servers able to delete node {0}",
						path
					);
                    dwarfGroup.Reply(err);
                }

            } else {
				/*
				  Actually deletes the node in this server's local tree.
				  
				  We are responding to an Isis query, which may or may not
				  have originated from this very server, but we cannot
				  assume either case.
				  				 
				  @TODO: Enable Logging & user feedback
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
		protected void getNode(string args, bool local=false)
		{
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to getNode.");
				return;
			}

            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("Too few arguments provided to getNode."));
                return;
            }
			
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null != stat) {
                stat.includeData();
                dwarfGroup.Reply(stat);
            } else {
                stat = new DwarfStat("Get Failed.");
                dwarfGroup.Reply(stat);
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

		protected void setNode(string args, bool local=false)
		{
            // Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to setNode.");
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
			  A request is considered local if we're the point of origin
			  for this request,  i.e. the client has issued a delete to us.
			 */
            if(!local) {
				/* 
				   Perform an in-order query to the group to make
				   sure everyone gets this set request at the
				   same time in the global request stream.
				*/
                List<string> retlst = new List<string>();
				
                dwarfGroup.OrderedQuery(
					Group.ALL,
					(int)IsisDwarfCode.UPDATE,
					new DwarfCommand((int)DwarfCode.SET_NODE, args),
					new EOLMarker(),
					retlst
				);
				
				/* 
				   Use predicate to make sure all nodes have successfuly 
				   completed the request before replying to the client.
				   If any of them failed, return an error string.

				   @TODO: This could be a function since delete/create/set use it.
				*/
				Predicate<string> p = delegate(string s){
					return !String.IsNullOrEmpty(s) && s.Equals(path);
				};

                if(retlst.TrueForAll(p)) {
                    dwarfGroup.Reply(data);
                } else {
                    string err = string.Format(
						"Error: Not all servers able to set node {0} to {1}",
						path,
						data
					);
                    dwarfGroup.Reply(err);
                }

            } else {
				/*
				  Actually deletes the node in this server's local tree.
				  
				  We are responding to an Isis query, which may or may not
				  have originated from this very server, but we cannot
				  assume either case.
				  				 
				  @TODO: Enable Logging & user feedback
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
		protected void exists(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to exists.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("No Arguments Provided to exists."));
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
        protected void getChildren(string args, bool local=false) 
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to getChildren.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            
            string[] childs = nodeSys.getChildList(argslst[0]);

            if(null == childs) {
                dwarfGroup.Reply("Error: Get Children Failed");
                return;
            }
           
            dwarfGroup.Reply(string.Join(",", childs));
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
		protected void getChildren2(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to getChildren2.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply("Error: No Arguments Provided to getChildren2");
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply("Error: getChildren2 Failed");
                return;
            }
            stat.includeChildLst();
           
            dwarfGroup.Reply(stat);
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

        protected void getNodeAll(string args, bool local=false)
		{
			// Make sure our args aren't null before parsing
			if (String.IsNullOrEmpty(args))
			{
				dwarfGroup.Reply("Malformed/empty arguments to getNodeAll.");
				return;
			}

            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("Error: No Arguments Provided to getNodeAll"));
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply(new DwarfStat("Error: getNodeAll Failed"));
                return;
            }
            stat.includeChildLst();
            stat.includeData();
           
            dwarfGroup.Reply(stat);
       }

        protected void test(string args, bool local=false)
		{
            Console.WriteLine("TEST || " + args);
            dwarfGroup.Reply("0xDEADWARF-TEST");
        }
		
		protected void sync(string args)
		{
			throw new NotImplementedException("Sync is not implemented.");
		}

        protected void printTreeLoop() 
		{
            while (true) {
                nodeSys.printTree();
                Thread.Sleep(5000);
            }
        }
		
		static void Main(string[] args)
		{
            //TODO add better command line argument for fast start
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]) && args[0].Equals("fast")){ 
                IsisSystem.Start(true);
            } else {
                IsisSystem.Start();
            }
			
			DwarfServer my_server = new DwarfServer();

			Console.WriteLine ("Waiting for client connection...");

            my_server.printTreeLoop();
			
            IsisSystem.WaitForever();
		}
	}
}

