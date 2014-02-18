using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace DwarfClient
{
	public class DwarfClient
	{
		public DwarfClient ()
		{
		}

		static void Main(string[] args) {
			TcpClient client = new TcpClient ();
			client.Connect (new IPEndPoint (IPAddress.Parse ("10.32.215.237"), 9845));
			NetworkStream stream = client.GetStream ();

			string msg = "This is a message";
			byte[] msgbuffer = Encoding.Unicode.GetBytes (msg);
			stream.Write (BitConverter.GetBytes ((Int32)msgbuffer.Length), 0, sizeof(Int32));
			stream.Write (msgbuffer, 0, msgbuffer.Length);
		}
	}
}

