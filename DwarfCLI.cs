using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

namespace dwarfkeeper
{
	public class DwarfCLI
	{
		private TcpClient tcp_client; 			//! TCP Client for remote connection
		private IPEndPoint server_ip_endpoint;	//! IP address of server
		private NetworkStream network_stream;	//! Network stream

		/** Common setup for class constructors.
		 * 
		 * @param	server_ip	IP Address of the server as a string
		 * @param	port_num	Port number as an integer
		 */
		private void init_cli(string server_ip, int port_num)
		{
			this.server_ip_endpoint = new IPEndPoint(IPAddress.Parse(server_ip), port_num);
			this.tcp_client = new TcpClient();
			this.network_stream = null;
		}

		/** Creates a CLI instance with server IP and port number
		 * 
		 * @param	server_ip	IP Address of the server as a string
		 * @param	port_num	Port number as an integer
		 */
		public DwarfCLI(string server_ip, int port_num)
		{
			init_cli(server_ip, port_num);
		}

		/** Creates a CLI instance with input of the form IP_ADDR:PORT_NUM
		 * 
		 * @param	server_ip_and_port	String containing IP_ADDR:PORT_NUM
		 */
		public DwarfCLI(string server_ip_and_port)
		{
			string[] server_args = server_ip_and_port.Split(new Char[] {':'} );
			string server_ip = server_args[0];
			int port_num = int.Parse(server_args[1]);
			init_cli(server_ip, port_num);
		}

		/** Gets connection status.
		 * 
		 * @return	Connection status as a bool.
		 */
		public bool get_connection_status()
		{
			if (this.tcp_client != null)
			{
				return this.tcp_client.Connected;
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
		 * @param	num_retries	Number of times to re-try connection
		 * @param	time_out	Number of seconds to wait for reconnect
		 */
		public void connect(int num_retries = 0, int time_out = 0)
		{
			if (this.tcp_client == null) 
			{
				this.tcp_client = new TcpClient();
			}

			do
			{
				try 
				{
					this.tcp_client.Connect(this.server_ip_endpoint);
				} catch (SocketException socket_excpt) {
					Console.WriteLine("Caught socket exception while connecting to "+this.server_ip_endpoint.ToString());

					num_retries--;

					if (num_retries > 0)
					{
						Console.WriteLine("Retrying...");
						Thread.Sleep(time_out);
					}
				}
			} while (num_retries > 0 && !this.tcp_client.Connected);

			if (this.tcp_client.Connected)
			{
				this.network_stream = this.tcp_client.GetStream();
				Console.WriteLine("Connection successful!");
			} else {
				Console.WriteLine("Connection failed.");
			}
		}

		/** Closes the connection if it is open.
		 * 
		 * If the connection is already closed, it's a no-op.
		 */
		public void close_connection()
		{
			if (get_connection_status())
			{
				this.network_stream.Close();
				this.tcp_client.Close();
				this.tcp_client = null;
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
		public void send_message(string msg)
		{
			if (!this.get_connection_status())
			{
				Console.WriteLine("Cannot send message: disconnected");
				return;
			}

			byte[] msgbuffer = Encoding.Unicode.GetBytes(msg); // Use UTF-8

			try
			{
				this.network_stream.Write(BitConverter.GetBytes ((Int32)msgbuffer.Length), 0, sizeof(Int32));
				this.network_stream.Write(msgbuffer, 0, msgbuffer.Length);
			} catch (System.IO.IOException socket_excpt) {
				Console.WriteLine("Failed to send message to server. Closing connection... ");
				this.close_connection();
			}
		}
	}


}

