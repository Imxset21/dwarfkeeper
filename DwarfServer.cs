using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

using DwarfCMD;
using DwarfTree;
using Isis;

namespace DwarfServer
{

    //TODO: Add logging of some sort to servers - Hook into Isis logging?
	public class DwarfServer : DwarfCMD.DwarfCMD
	{
		const int DEFAULT_PORT_NUM = 9845;      //!< Default port number (Isis' default + 2)

		private IPHostEntry iphost;             //!< IP entry point for this host
		private TcpListener tcpServer;          //!< TCP server listener
		private TcpClient tcpClient;            //!< TCP client connection
		private NetworkStream networkStream;    //!< Network stream for message passing
        private DwarfTree.DwarfTree nodeSys;              //!< Underlying node file system

		/** Initializes a server with a given/default port number.
		 *
		 * @param portnum Default port number or user override.
		 *
		 */
        public DwarfServer(int portnum = DEFAULT_PORT_NUM)
		{
			// Set this to our actual IP (according to DNS)
			this.iphost = Dns.GetHostEntry(Dns.GetHostName());

			// Set the server to listen for connections on the Isis default port num + 2
			this.tcpServer = new TcpListener(new IPEndPoint(iphost.AddressList[0], portnum));

			this.tcpClient = null;
			this.networkStream = null;

            // Initialize command parsing/handling from DwarfCMD
			base.dwarfCmds = new Dictionary<string, dwarfCmd>();
			base.dwarfCmds["create"] = this.create;
			base.dwarfCmds["rmr"] = this.delete;
			base.dwarfCmds["get"] = this.getNode;
			base.dwarfCmds["set"] = this.setNode;
			base.dwarfCmds["stat"] = this.stat;
			base.dwarfCmds["ls"] = this.getChildren;
			base.dwarfCmds["sync"] = this.sync; //TODO: Eventually implement (maybe)

            // Start a new DwarfTree instance for this server instance
            nodeSys = DwarfTree.DwarfTree.CreateTree();
		}

        /** Create the node at path - CLI command is "create".
         */
		private void create(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }

            //TODO support ACL inputs
            //TODO support error messages on bad node adds
            bool success = nodeSys.addNode(argslst[0], argslst[1]);
			throw new NotImplementedException("create is not implemented.");
		}
		
		private void delete(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            //TODO: Enable Logging & user feedback
            bool success = nodeSys.removeNode(argslst[0]);
			throw new NotImplementedException("rmr is not implemented.");
		}

		private void getNode(string args)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            Dictionary<string, string> stat = nodeSys.getNode(argslst[0]);
			throw new NotImplementedException("get is not implemented.");
		}

		private void setNode(string args)
		{
            string[] argslst = args.Split();
            if(argslst.Length < 2) {
                return;
            }
            bool success = nodeSys.setData(argslst[0], argslst[1]);
			throw new NotImplementedException("set is not implemented.");
		}

		private void stat(string args)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            Dictionary<string, string> stats = nodeSys.getNodeInfo(argslst[0]);
			throw new NotImplementedException("stat is not implemented.");			
		}

		private void getChildren(string args)
		{
            //TODO Support watches
            string[] argslst = args.Split();
            if(argslst.Length < 1) {
                return;
            }
            Dictionary<string, string> stats = nodeSys.getChildList(argslst[0]);
			throw new NotImplementedException("ls is not implemented.");			
		}
		
		private void sync(string args)
		{
			throw new NotImplementedException("Sync is not implemented.");
		}
		
		/** Starts the TCP server on localhost with port number.
		 *
		 */
		public void serverStart()
		{
			Console.WriteLine("Starting Server on " + this.tcpServer.ToString());

			//TODO: What to do server is still active. No-op?
			this.tcpServer.Start();
		}


		/** Wait for the client to connect with given timeout.
		 * 
		 * Note that this initializes the network stream after connecting.
		 *
		 * @param timeout Timeout for waiting for client.
		 *
		 */
		public void waitForClient(int timeout = 0)
		{
			if (timeout == 0 || this.tcpServer.Pending())
			{
				// Attempt to get the connection immediately if
				// 1) We don't care about timeouts (will block)
				// 2) We have a client already trying to connect
				this.tcpClient = this.tcpServer.AcceptTcpClient();
			} else { 
				//TODO: Implement waitForClient() timeout
				throw new NotImplementedException();
			}
			
			this.networkStream = this.tcpClient.GetStream();
		}
		
		/** Gets message sent from client.
		 *
		 * @return String message sent by client.
		 */
		public string getMessage()
		{
			byte[] inbuffer = new byte[256];

			byte[] header = new byte[4];
            this.networkStream.Read (header, 0, header.Length);
			Int32 msglen = System.BitConverter.ToInt32(header, 0);

			inbuffer = new byte[msglen];
			this.networkStream.Read(inbuffer, 0, inbuffer.Length);
			string msg = System.Text.Encoding.Unicode.GetString(inbuffer);
			return msg;
		}

		static void Main(string[] args)
		{
			DwarfServer dwarfServer = new DwarfServer();
			dwarfServer.serverStart();

			Console.WriteLine ("Waiting for client connection...");
			dwarfServer.waitForClient();
			
			while (true)
			{
				dwarfServer.parseCmd(dwarfServer.getMessage());
				// dwarf_server.wait_for_client();
			}
		}
	}
}

