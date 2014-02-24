using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Isis;

//TODO: Move parsing to DwarfCMD

namespace DwarfServer
{
	//TODO: Move parsing to DwarfCMD
	public delegate void dwarfCmd(string args);

	public class DwarfServer
	{
		const int DEFAULT_PORT_NUM = 9845;      //!< Default port number (Isis' default + 2)

		private IPHostEntry iphost;             //!< IP entry point for this host
		private TcpListener tcpServer;          //!< TCP server listener
		private TcpClient tcpClient;            //!< TCP client connection
		private NetworkStream networkStream;    //!< Network stream for message passing

		//TODO: Move to DwarfCMD
		private Dictionary<string, dwarfCmd> dwarfCmds;

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

			//TODO: Move parsing to DwarfCMD
			this.dwarfCmds = new Dictionary<string, dwarfCmd>();
			this.dwarfCmds["create"] = this.create;
			this.dwarfCmds["rmr"] = this.delete;
			this.dwarfCmds["get"] = this.getNode;
			this.dwarfCmds["set"] = this.setNode;
			this.dwarfCmds["stat"] = this.stat;
			this.dwarfCmds["ls"] = this.getChildren;
			this.dwarfCmds["sync"] = this.sync; //TODO: Eventually implement (maybe)
		}

		private void create(string args)
		{
			throw new NotImplementedException("Create is not implemented.");
		}
		
		private void delete(string args)
		{
			throw new NotImplementedException("rmr is not implemented.");
		}

		private void getNode(string args)
		{
			throw new NotImplementedException("get is not implemented.");
		}

		private void setNode(string args)
		{
			throw new NotImplementedException("set is not implemented.");
		}

		private void stat(string args)
		{
			throw new NotImplementedException("stat is not implemented.");			
		}

		private void getChildren(string args)
		{
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

		//TODO: Move to DwarfCMD
		public void parseCmd(string cmd)
		{
			//TODO: Parse cmd, seperate command from command args
			if (String.IsNullOrWhiteSpace(cmd))
			{
				return;
			}

			string[] cmdAndArgs = cmd.Split(new Char[] {' '}, 2);
			string args = null;

			cmd = cmdAndArgs[0];

			try
			{
				args = cmdAndArgs[1];
			} catch (IndexOutOfRangeException oorE) {
				;
			}
			

			try
			{
				this.dwarfCmds[cmd](args);
			} catch (KeyNotFoundException kE) {
				Console.WriteLine("Unrecognized Command: "+cmd);
			} catch (ArgumentNullException anE){
				;
			}
			return;
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
				DwarfServer.parseCmd(dwarfServer.getMessage());
				// dwarf_server.wait_for_client();
			}
		}
	}
}

