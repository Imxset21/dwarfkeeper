using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

using DwarfCMD;
using Isis;

namespace dwarfkeeper
{
	public class DwarfClient
	{
        Client dclient;

		/** Creates a CLI instance with input of the form IP_ADDR:PORT_NUM
		 * 
		 * @param	serverIpPort	String containing IP_ADDR:PORT_NUM
		 */
		public DwarfClient(string groupname)
		{
            IsisSystem.Start();
            Isis.Msg.RegisterType(typeof(DwarfCommand), 111);
            dclient = new Client(groupname);
		}
        

        public List<string> create(string path, string data) {
            string args = path + " " + data;
            List<string> retlist = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.CREATE, args),
                    new EOLMarker(),
                    retlist);
            return retlist;
        }

        public List<string> test(string args) {
            List<string> retlist = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.TEST, args),
                    new EOLMarker(),
                    retlist);

            return retlist;
        }

//		/** Connect to the server this CLI is configured against.
//		 * 
//		 * By default, try only once without a timeout.
//		 * Otherwise we try as many times as specified, waiting
//		 * for the timeout between each attempt.
//		 * 
//		 * @param	numRetries	Number of times to re-try connection
//		 * @param	timeOut     Number of seconds to wait for reconnect
//		 */
//		public void connect(int numRetries = 0, int timeOut = 0)
//		{
//			if (this.tcpClient == null) 
//			{
//				this.tcpClient = new TcpClient();
//			}
//
//			do
//			{
//				try 
//				{
//					this.tcpClient.Connect(this.serverIpEndpoint);
//				} catch (SocketException socketExcpt) {
//					Console.WriteLine("Caught socket exception while connecting to "
//									  + this.serverIpEndpoint.ToString());
//
//					numRetries--;
//
//					if (numRetries > 0)
//					{
//						Console.WriteLine("Retrying...");
//						Thread.Sleep(timeOut);
//					}
//				}
//			} while (numRetries > 0 && !this.tcpClient.Connected);
//
//			if (this.tcpClient.Connected)
//			{
//				this.networkStream = this.tcpClient.GetStream();
//				Console.WriteLine("Connection successful!");
//			} else {
//				Console.WriteLine("Connection failed.");
//			}
//		}
//
//		/** Closes the connection if it is open.
//		 * 
//		 * If the connection is already closed, it's a no-op.
//		 */
//		public void closeConnection()
//		{
//			if (this.getConnectionStatus())
//			{
//				this.networkStream.Close();
//				this.tcpClient.Close();
//				this.tcpClient = null;
//				Console.WriteLine("Connection closed.");
//			} else {
//				Console.WriteLine("Connection is already closed.");
//			}
//		}
//
//		/** Sends a string message to the server.
//		 * 
//		 * Message contents are sent through unmolested.
//		 * If we fail to write to the socket we assume that the
//		 * server has disconnected and close the connection.
//		 * 
//		 * @param	msg	Message to send to server
//		 */
//		public void sendMessage(string msg)
//		{
//			if (!this.getConnectionStatus())
//			{
//				Console.WriteLine("Cannot send message: disconnected");
//				return;
//			}
//
//			byte[] msgbuffer = Encoding.Unicode.GetBytes(msg); // Use UTF-8
//
//			try
//			{
//				this.networkStream.Write(BitConverter.GetBytes((Int32)msgbuffer.Length), 0, sizeof(Int32));
//				this.networkStream.Write(msgbuffer, 0, msgbuffer.Length);
//			} catch (System.IO.IOException socketExcpt) {
//				Console.WriteLine("Failed to send message to server. Closing connection... ");
//				this.closeConnection();
//			}
//		}
//
//
//        private string waitForResponse() {
//            byte[] header = new byte[4];
//            this.networkStream.Read(header, 0, header.Length);
//            Int32 msglen = System.BitConverter.ToInt32(header,0);
//
//            byte[] inbuffer = new byte[msglen];
//            this.networkStream.Read(inbuffer, 0, inbuffer.Length);
//            string msg = System.Text.Encoding.Unicode.GetString(inbuffer);
//
//            return msg;
//        }
//
//
//        public string ls() {
//           string response = this.waitForResponse();
//           return response;
//        }
	}

}

