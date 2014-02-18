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

		public DwarfServer ()
		{
		}

		static void Main(string[] args) {
			MemoryStream ms = new MemoryStream ();
			byte[] inbuffer = new byte[256];

			IPHostEntry iphost = Dns.GetHostEntry (Dns.GetHostName ());

			// Set the server to listen for connections on the Isis default port num + 2
			TcpListener server = new TcpListener (new IPEndPoint (iphost.AddressList [0], Isis.Isis.ISIS_DEFAULT_PORTNOp + 2));

			Console.WriteLine ("Starting Server on " + iphost.AddressList[0] + ":" + (Isis.Isis.ISIS_DEFAULT_PORTNOp + 2));
			server.Start ();

			Console.WriteLine ("Waiting for client connection...");
			TcpClient client =  server.AcceptTcpClient ();

			NetworkStream stream = client.GetStream ();

			int len;

			byte[] header = new byte[4];
			stream.Read (header, 0, header.Length);
			Int32 msglen = System.BitConverter.ToInt32 (header, 0);

			inbuffer = new byte[msglen];
			stream.Read (inbuffer, 0, inbuffer.Length);
			string msg = System.Text.Encoding.Unicode.GetString(inbuffer);
			Console.WriteLine (msg);

		}
	}
}

