using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

using dwarfkeeper; // Import CLI

//FIXME:	There is a bug that if send_message is the first command, 
//			(without any setup) the client crashes with a null pointer exception.

namespace DwarfClient
{
	public class DwarfClient
	{
		public DwarfClient ()
		{
		}


		static void Main(string[] args) 
		{
			dwarfkeeper.DwarfCLI cli = null;

			// Setup CLI for server connection
			if (args.Length == 0)
			{
				cli = new dwarfkeeper.DwarfCLI("10.32.215.135", 9845);
			} else {
				cli = new dwarfkeeper.DwarfCLI(args[0]);
			}

			// Event loop
			while (true) // Loop indefinitely
			{
				Console.Write("\n>>DwarfKeeper: "); // Prompt
				string line = Console.ReadLine(); // Get string from user
				string msg = null;

				//TODO: Replace with better string parser -- probably a function
				switch (line)
				{
					case "exit": // Exit the prompt
					{
						goto exit_cli;
					}
					case  "connect": // Attempt a connection
					{
						cli.connect(5, 1000);
						break;
					}
					case "send_message": // Sends a message
					{
						Console.Write("\nEnter Message: ");
						msg = Console.ReadLine();
						if (String.IsNullOrWhiteSpace(msg))
						{
							Console.WriteLine ("Malformed input, discarding message...");
						} else {
							cli.send_message (msg);
						}
						break;
					}
					case "connection_status": // Get connection status
					{
						Console.WriteLine("Connection status: " +
									(cli.get_connection_status() ? "Open" : "Closed"));
						break;
					}
					case "disconnect":	// Disconnect from server
					{
						cli.close_connection();
						break;
					}
					default:
					{
						Console.WriteLine("Unknown command: " + line);
						break;
					}
				}
			}

			exit_cli:
			cli.close_connection();
		}
	}
}

