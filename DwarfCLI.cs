using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Collections.Generic;

using dwarfkeeper; // Import Client
using DwarfCMD;
using Isis;

//FIXME:	There is a bug that if send_message is the first command, 
//			(without any setup) the client crashes with a null pointer exception.

namespace DwarfCLI
{
    public abstract class DwarfCMD2
    {
        protected Dictionary<string, dwarfCmd> dwarfCmds;


		/** Parses a user command to a delegate function.
		 *
		 * @param cmd Command to be parsed
		 */
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
				dwarfCmds[cmd](args);
			} catch (KeyNotFoundException kE) {
				Console.WriteLine("Unrecognized Command: "+cmd);
			} catch (ArgumentNullException anE){
				;
			}
			return;
		}
    }


	public class DwarfCLI : DwarfCMD2
	{
		private bool isRunning;                            //!< Current running status
		private DwarfClient client;                        //!< Client backend

		/** Creates a command-line-interface client wrapper.
		 *
         * By default this is considered to be running.
         *
		 */
		public DwarfCLI()
		{
			this.client = null;
			this.isRunning = true;
			
			// Setup delegate dictionary
			base.dwarfCmds = new Dictionary<string, dwarfCmd>();
			base.dwarfCmds["connect"] = this.connect;
			base.dwarfCmds["disconnect"] = this.disconnect;
			base.dwarfCmds["sendMessage"] = this.sendMessage;
			base.dwarfCmds["connectionStatus"] = this.connectionStatus;
			base.dwarfCmds["exit"] = this.exit;
		}

		/** Creates a CLI with the given server IP address.
		 *
		 * @param ipAddr IP address of the server
		 */
		public DwarfCLI(string ipAddr) : this()
		{
			this.client = new dwarfkeeper.DwarfClient(ipAddr);
		}

		/** Creates a CLI with the given server IP and port.
		 *
		 * @param ipAddr IP address of the server
		 * @param portNum Port number of the server
		 */
		public DwarfCLI(string ipAddr, int portNum) : this()
		{
			this.client = new dwarfkeeper.DwarfClient(ipAddr, portNum);
		}

		/** Gets current running status.
		 *
		 * @return Running status
		 */
		public bool getRunningStatus()
		{
			return this.isRunning;
		}

		/** Sends a message to the server.
		 *
		 * If no message is provided, the user is prompted.
		 * 
		 * @param args Message to be sent
		 */
		private void sendMessage(string args)
		{
			if (String.IsNullOrWhiteSpace(args))
			{
				Console.Write("\nEnter Message: ");
				String msg = Console.ReadLine();
				this.client.sendMessage(msg);
			} else {
				this.client.sendMessage(args);
			}

			return;
		}

		/** Gets connection status.
		 *
		 *  Note that this is distinct from running status.
		 */
		private void connectionStatus(string args)
		{
			Console.WriteLine("Connection status: " +
								(this.client.getConnectionStatus() ? "Open" : "Closed"));
			return;
		}

		/** Disconnects (closes connection to) the server.
		 *
		 *
		 */
		private void disconnect(string args)
		{
			this.client.closeConnection();
			return;
		}

		/** Connects to the server.
		 *
		 * @param args Arguments regarding connection attempts/timeout
		 */
		private void connect(string args)
		{
			//TODO: Parse connect() args w/ reasonable defaults
			this.client.connect(5, 1000);
			return;
		}

		/** Disconnects from server and exits the interface.
		 *
		 * @param args Arguments to pass to disconnect().
		 */
		private void exit(string args)
		{
			this.disconnect(args);
			this.isRunning = false;
			return;
		}

		static void Main(string[] args) 
		{
            IsisSystem.Start();
            Group dwarfGroup = new Group("dwarfkeeper");

            List<string> stringydwarves = new List<string>();
            int nr = dwarfGroup.Query(Group.ALL, 
                    new Timeout(1000, Timeout.TO_FAILURE),
                    0,
                    new DwarfCommand(CMDCode.MESSAGE, "LOLOLOLOLOLOLOLOLOLOL"),
                    new EOLMarker(),
                    stringydwarves);
            foreach (string rep in stringydwarves) {
                Console.WriteLine(rep);
            }

            IsisSystem.Shutdown();
            
			//DwarfCLI cli = null;

			//// Setup CLI for server connection
			//if (args.Length == 0)
			//{
			//	cli = new DwarfCLI("10.32.215.135", 9845);
			//} else {
			//	cli = new DwarfCLI(args[0]);
			//}

			//// Event loop
			//while (cli.getRunningStatus()) // Loop indefinitely
			//{
			//	Console.Write("\n>>DwarfKeeper: "); // Prompt
			//	string line = Console.ReadLine(); // Get string from user
			//	cli.parseCmd(line);
			//}

		}
	}
}

