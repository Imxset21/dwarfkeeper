using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

namespace dwarfkeeper
{
	public class DwarfClient
	{
		private TcpClient tcpClient; 			//!< TCP Client for remote connection
		private IPEndPoint serverIpEndpoint;	//!< IP address of server
		private NetworkStream networkStream;	//!< Network stream

		/** Common setup for class constructors.
		 * 
		 * @param	serverIp	IP Address of the server as a string
		 * @param	portNum    	Port number as an integer
		 */
		private void initCli(string serverIp, int portNum)
		{
			this.serverIpEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), portNum);
			this.tcpClient = new TcpClient();
			this.networkStream = null;
		}

		/** Creates a CLI instance with server IP and port number
		 * 
		 * @param	serverIp    IP Address of the server as a string
		 * @param	portNum     Port number as an integer
		 */
		public DwarfClient(string serverIp, int portNum)
		{
			this.initCli(serverIp, portNum);
		}

		/** Creates a CLI instance with input of the form IP_ADDR:PORT_NUM
		 * 
		 * @param	serverIpPort	String containing IP_ADDR:PORT_NUM
		 */
		public DwarfClient(string serverIpPort)
		{
			string[] serverArgs = serverIpPort.Split(new Char[] {':'} );
			string serverIp = serverArgs[0];
			int portNum = int.Parse(serverArgs[1]);
			this.initCli(serverIp, portNum);
		}

		/** Gets connection status.
		 * 
		 * @return Connection status as a bool.
		 */
		public bool getConnectionStatus()
		{
			if (this.tcpClient != null)
			{
				return this.tcpClient.Connected;
			} else {
				return false;
			}
		}

		/** Connect to the server this CLI is configured against.
		 * 
		 * By default, try only once without a timeout.
		 * Otherwise we try as many times as specified, waiting
		 * for the timeout between each attempt.
		 * 
		 * @param	numRetries	Number of times to re-try connection
		 * @param	timeOut     Number of seconds to wait for reconnect
		 */
		public void connect(int numRetries = 0, int timeOut = 0)
		{
			if (this.tcpClient == null) 
			{
				this.tcpClient = new TcpClient();
			}

			do
			{
				try 
				{
					this.tcpClient.Connect(this.serverIpEndpoint);
				} catch (SocketException socketExcpt) {
					Console.WriteLine("Caught socket exception while connecting to "
									  + this.serverIpEndpoint.ToString());

					numRetries--;

					if (numRetries > 0)
					{
						Console.WriteLine("Retrying...");
						Thread.Sleep(timeOut);
					}
				}
			} while (numRetries > 0 && !this.tcpClient.Connected);

			if (this.tcpClient.Connected)
			{
				this.networkStream = this.tcpClient.GetStream();
				Console.WriteLine("Connection successful!");
			} else {
				Console.WriteLine("Connection failed.");
			}
		}

		/** Closes the connection if it is open.
		 * 
		 * If the connection is already closed, it's a no-op.
		 */
		public void closeConnection()
		{
			if (this.getConnectionStatus())
			{
				this.networkStream.Close();
				this.tcpClient.Close();
				this.tcpClient = null;
				Console.WriteLine("Connection closed.");
			} else {
				Console.WriteLine("Connection is already closed.");
			}
		}

		/** Sends a string message to the server.
		 * 
		 * Message contents are sent through unmolested.
		 * If we fail to write to the socket we assume that the
		 * server has disconnected and close the connection.
		 * 
		 * @param	msg	Message to send to server
		 */
		public void sendMessage(string msg)
		{
			if (!this.getConnectionStatus())
			{
				Console.WriteLine("Cannot send message: disconnected");
				return;
			}

			byte[] msgbuffer = Encoding.Unicode.GetBytes(msg); // Use UTF-8

			try
			{
				this.networkStream.Write(BitConverter.GetBytes((Int32)msgbuffer.Length), 0, sizeof(Int32));
				this.networkStream.Write(msgbuffer, 0, msgbuffer.Length);
			} catch (System.IO.IOException socketExcpt) {
				Console.WriteLine("Failed to send message to server. Closing connection... ");
				this.closeConnection();
			}
		}


        private string waitForResponse() {
            byte[] header = new byte[4];
            this.networkStream.Read(header, 0, header.Length);
            Int32 msglen = System.BitConverter.ToInt32(header,0);

            byte[] inbuffer = new byte[msglen];
            this.networkStream.Read(inbuffer, 0, inbuffer.Length);
            string msg = System.Text.Encoding.Unicode.GetString(inbuffer);

            return msg;
        }


        public string ls() {
           string response = this.waitForResponse();
           return response;
        }
	}


}

