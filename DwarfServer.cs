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
    
    delegate void dwarfIsisHandler(DwarfCommand args);
    delegate void dwarfOpHandler(string args, bool local);

    //TODO: Add logging of some sort to servers - Hook into Isis logging?
	public class DwarfServer
	{
		const int DEFAULT_PORT_NUM = 9845;      //!< Default port number (Isis' default + 2)

        private static DwarfTree nodeSys;              //!< Underlying node file system
        private static Dictionary<DwarfCode, dwarfOpHandler> dwarfOps;
        private static Group dwarfGroup;

        private static void initDwarfHandlers() {
            dwarfOps = new Dictionary<DwarfCode, dwarfOpHandler>();
            dwarfOps.Add(DwarfCode.TEST, (dwarfOpHandler)test);
            dwarfOps.Add(DwarfCode.CREATE, (dwarfOpHandler)create);
            dwarfOps.Add(DwarfCode.DELETE, (dwarfOpHandler)delete);
            dwarfOps.Add(DwarfCode.GET_NODE, (dwarfOpHandler)getNode);
            dwarfOps.Add(DwarfCode.GET_CHILDREN, (dwarfOpHandler)getChildren);
            dwarfOps.Add(DwarfCode.GET_CHILDREN2, (dwarfOpHandler)getChildren2);
            dwarfOps.Add(DwarfCode.SET_NODE, (dwarfOpHandler)setNode);
            dwarfOps.Add(DwarfCode.GET_ALL, (dwarfOpHandler)getNodeAll);
            dwarfOps.Add(DwarfCode.EXISTS, (dwarfOpHandler)exists);
        }

        private static void defineOpHandlers() {
            dwarfGroup.Handlers[(int)IsisDwarfCode.OPCODE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd) {
                    ThreadStart ts =
                        new ThreadStart(() => 
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args,local:false));
                    Thread t = new Thread(ts);
                    dwarfGroup.SetReplyThread(t);
                    t.Start();
                };
            dwarfGroup.AllowClientRequests((int)IsisDwarfCode.OPCODE);

            dwarfGroup.Handlers[(int)IsisDwarfCode.UPDATE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd) {
                    ThreadStart ts = 
                        new ThreadStart(() =>
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args,local:true));
                    Thread t = new Thread(ts);
                    dwarfGroup.SetReplyThread(t);
                    t.Start();
                };
        }

		/** Starts the TCP server on localhost with port number.
		 *
		 */
		public static void serverStart()
		{
            nodeSys = DwarfTree.CreateTree();

            dwarfGroup = new Group("dwarfkeeper");

            defineOpHandlers();
            initDwarfHandlers();

            Isis.Msg.RegisterType(typeof(DwarfCommand), 111);
            Isis.Msg.RegisterType(typeof(DwarfStat), 113);

            dwarfGroup.Join();

			//TODO: What to do server is still active. No-op?
			//this.tcpServer.Start();
		}




        /**************************************************
         *
         * DwarfTree Manipulation Functions
         *
         **************************************************/

        
        /** Create the node at path - CLI command is "create".
         */
		private static void create(string args, bool local=false)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                dwarfGroup.Reply("Too few arguments provided");
                return;
            }
            string path = argslst[0];
            string data = argslst[1];
            
            if(!local) {
                List<string> retlst = new List<string>();
                dwarfGroup.OrderedQuery(Group.ALL,
                        (int)IsisDwarfCode.UPDATE,
                        new DwarfCommand((int)DwarfCode.CREATE, args),
                        new EOLMarker(),
                        retlst);
                Predicate<string> p = delegate(string s) {return s.Equals(path);};
                if(retlst.TrueForAll(p)) {
                    dwarfGroup.Reply(path);
                } else {
                    string err = string.Format(
                            "Error: Not all servers created node {0}",
                            path, data);
                    dwarfGroup.Reply(err);
                }
                return;
            }

            //TODO support ACL inputs 
            //TODO support error messages on bad node add
            bool success = nodeSys.addNode(path, data);
            
            //TODO: send update to rest of group
            if(success) {
                dwarfGroup.Reply(path);

                //TODO remove print
                nodeSys.printTree();

            } else {
                string err = string.Format(
                        "Error: Failed to create node {0}, with data {1}",
                        path, data);
                dwarfGroup.Reply(err);
            }
		}
		
		private static void delete(string args, bool local=false)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply("Too few arguments provided");
                return;
            }
            string path = argslst[0];

            if(!local) {
                List<string> retlst = new List<string>();
                dwarfGroup.OrderedQuery(Group.ALL,
                        (int)IsisDwarfCode.UPDATE,
                        new DwarfCommand((int)DwarfCode.DELETE, args),
                        new EOLMarker(),
                        retlst);
                Predicate<string> p = delegate(string s) {return s.Equals(path);};
                if(retlst.TrueForAll(p)) {
                    dwarfGroup.Reply(path);
                } else {
                    string err = string.Format(
                            "Error: Not all servers able to delete node {0}",
                            path);
                    dwarfGroup.Reply(err);
                }
                return;
            }
            
            //TODO: Enable Logging & user feedback
            bool success = nodeSys.removeNode(path);
            if(success) {
                dwarfGroup.Reply(path);
            } else {
                string err = 
                    string.Format("Error: Failed to delete node {0}", path);
                dwarfGroup.Reply(err);
            }
		}

		private static void getNode(string args, bool local=false)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("Too few arguments provided"));
                return;
            }
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null != stat) {
                stat.includeData();
                dwarfGroup.Reply(stat);
            } else {
                stat = new DwarfStat("Get Failed");
                dwarfGroup.Reply(stat);
            }
		}

		private static void setNode(string args, bool local=false)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                dwarfGroup.Reply(new DwarfStat("Too few arguments provided"));
                return;
            }
            string path = argslst[0];
            string data = argslst[1];

            if(!local) {
                List<string> retlst = new List<string>();
                dwarfGroup.OrderedQuery(Group.ALL,
                        (int)IsisDwarfCode.UPDATE,
                        new DwarfCommand((int)DwarfCode.SET_NODE, args),
                        new EOLMarker(),
                        retlst);
                Predicate<string> p = delegate(string s) {return s.Equals(data);};
                if(retlst.TrueForAll(p)) {
                    dwarfGroup.Reply(data);
                } else {
                    string err = string.Format(
                            "Error: Not all servers able to set node {0} to {1}",
                            path, data);
                    dwarfGroup.Reply(err);
                }
                return;
            }
            
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

		private static void exists(string args, bool local=false)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("No Arguments Provided"));
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


        private static void getChildren(string args, bool local=false) {
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

		private static void getChildren2(string args, bool local=false)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply("Error: No Arguments Provided");
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply("Error: Get Children Failed");
                return;
            }
            stat.includeChildLst();
           
            dwarfGroup.Reply(stat);
		}

        private static void getNodeAll(string args, bool local=false) {
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                dwarfGroup.Reply(new DwarfStat("Error: No Arguments Provided"));
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeInfo(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply(new DwarfStat("Error: Get Node Failed"));
                return;
            }
            stat.includeChildLst();
            stat.includeData();
           
            dwarfGroup.Reply(stat);
       }

        private static void test(string args, bool local=false) {
            Console.WriteLine("TEST || " + args);
            dwarfGroup.Reply("0xDEADWARF-TEST");
        }
		
		private static void sync(string args)
		{
			throw new NotImplementedException("Sync is not implemented.");
		}
		

        private static void printTreeLoop() {
            while (true) {
                nodeSys.printTree();
                Thread.Sleep(5000);
            }
        }



		static void Main(string[] args)
		{
            //TODO add better command line argument for fast start
            if (args.Length > 0 && args[0].Equals("fast")) {
                IsisSystem.Start(true);
            } else {
                IsisSystem.Start();
            }
			DwarfServer.serverStart();

//            Thread t = new Thread(printTree);
//            t.Start();

			Console.WriteLine ("Waiting for client connection...");
			//dwarfServer.waitForClient();
            //
            printTreeLoop();
			
            IsisSystem.WaitForever();
		}
	}
}

