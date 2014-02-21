using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Isis;

namespace DwarfServer
{
	public class DwarfServer
	{
		const int DEFAULT_PORT_NUM = 9845;

		private IPHostEntry iphost;
		private TcpListener tcp_server;
		private TcpClient tcp_client;
		private NetworkStream network_stream;

        public DwarfServer(int portnum = DEFAULT_PORT_NUM)
		{
			// Set this to our actual IP (according to DNS)
			this.iphost = Dns.GetHostEntry(Dns.GetHostName());

			// Set the server to listen for connections on the Isis default port num + 2
			this.tcp_server = new TcpListener(new IPEndPoint(iphost.AddressList[0], portnum));

			this.tcp_client = null;
			this.network_stream = null;
		}

		public void server_start()
		{
			Console.WriteLine("Starting Server on " + this.tcp_server.ToString());

			//TODO: What to do server is still active. No-op?
			this.tcp_server.Start();
		}

		public void wait_for_client(int timeout = 0)
		{
			if (timeout == 0 || this.tcp_server.Pending())
			{
				// Attempt to get the connection immediately if
				// 1) We don't care about timeouts (will block)
				// 2) We have a client already trying to connect
				this.tcp_client = this.tcp_server.AcceptTcpClient();
			} else { 
				throw new NotImplementedException();
			}
			
			this.network_stream = this.tcp_client.GetStream();
		}

		public string get_message()
		{
			byte[] inbuffer = new byte[256];

			byte[] header = new byte[4];
            this.network_stream.Read (header, 0, header.Length);
			Int32 msglen = System.BitConverter.ToInt32(header, 0);

			inbuffer = new byte[msglen];
			this.network_stream.Read(inbuffer, 0, inbuffer.Length);
			string msg = System.Text.Encoding.Unicode.GetString(inbuffer);
			return msg;
		}

		static void Main(string[] args)
		{
			DwarfServer dwarf_server = new DwarfServer();
			dwarf_server.server_start();

			Console.WriteLine ("Waiting for client connection...");
			dwarf_server.wait_for_client();
			
			while (true)
			{
				Console.WriteLine(dwarf_server.get_message());
				// dwarf_server.wait_for_client();
			}
		}
	}
}

