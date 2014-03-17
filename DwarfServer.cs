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
    delegate void dwarfOpHandler(string args);

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
        }

        private static void defineOpHandlers() {
            dwarfGroup.Handlers[(int)IsisDwarfCode.OPCODE] += 
                (dwarfIsisHandler)delegate(DwarfCommand cmd) {
                    ThreadStart ts =
                        new ThreadStart(() => 
                            dwarfOps[(DwarfCode)cmd.opCode](cmd.args));
                    Thread t = new Thread(ts);
                    dwarfGroup.SetReplyThread(t);
                    t.Start();
                };
            dwarfGroup.AllowClientRequests((int)IsisDwarfCode.OPCODE);
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
		private static void create(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }

            //TODO support ACL inputs
            //TODO support error messages on bad node adds
            bool success = nodeSys.addNode(argslst[0], argslst[1]);
            
            //TODO: send update to rest of group
            if(success) {
                dwarfGroup.Reply(argslst[0]);
                nodeSys.printTree();
            } else {
                string err = 
                    string.Format("Error: Failed to create node {0}, with data {1}",
                        argslst[0], argslst[1]);
                dwarfGroup.Reply(err);
            }
		}
		
		private static void delete(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            //TODO: Enable Logging & user feedback
            bool success = nodeSys.removeNode(argslst[0]);

            if(success) {
                dwarfGroup.Reply(argslst[0]);
            } else {
                string err = 
                    string.Format("Error: Failed to delete node {0}", argslst[0]);
                dwarfGroup.Reply(err);
            }
		}

		private static void getNode(string args)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            DwarfStat stat = nodeSys.getNode(argslst[0]);

            if(null != stat) {
                dwarfGroup.Reply(stat);
            } else {
                stat = new DwarfStat("Get Failed");
                dwarfGroup.Reply(stat);
            }
		}

		private static void setNode(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }
            bool success = nodeSys.setData(argslst[0], argslst[1]);
			throw new NotImplementedException("set is not implemented.");
		}

		private static void stat(string args)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            DwarfStat stats = nodeSys.getNodeInfo(argslst[0]);
			throw new NotImplementedException("stat is not implemented.");			
		}


        private static void getChildren(string args) {
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeChildren(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply("Error: Get Children Failed");
                return;
            }
           
            dwarfGroup.Reply(stat.childlst);
        }

		private static void getChildren2(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            
            DwarfStat stat = nodeSys.getNodeChildren(argslst[0]);

            if(null == stat) {
                dwarfGroup.Reply("Error: Get Children Failed");
                return;
            }
           
            dwarfGroup.Reply(stat);
		}

        private static void test(string args) {
            Console.WriteLine("TEST || " + args);
            dwarfGroup.Reply("0xDEADWARF-TEST");
        }
		
		private static void sync(string args)
		{
			throw new NotImplementedException("Sync is not implemented.");
		}
		

        private static void printTree() {
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
			
            IsisSystem.WaitForever();
		}
	}
}

