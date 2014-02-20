using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Collections.Generic;

using dwarfkeeper; // Import Client

//FIXME:	There is a bug that if send_message is the first command, 
//			(without any setup) the client crashes with a null pointer exception.

namespace DwarfCLI
{
	public delegate void dwarf_cmd(string args);

	public class DwarfCLI
	{
		private bool is_running;
		private DwarfClient client;
		private Dictionary<string, dwarf_cmd> dwarf_cmds;

		public DwarfCLI()
		{
			this.client = null;
			this.is_running = true;
			
			dwarf_cmds = new Dictionary<string, dwarf_cmd>();
			dwarf_cmds["connect"] = this.connect;
			dwarf_cmds["disconnect"] = this.disconnect;
			dwarf_cmds["send_message"] = this.send_message;
			dwarf_cmds["connection_status"] = this.connection_status;
			dwarf_cmds["exit"] = this.exit;
		}

		public DwarfCLI(string ip_addr) : this()
		{
			this.client = new dwarfkeeper.DwarfClient(ip_addr);
		}

		public DwarfCLI(string ip_addr, int port_num) : this()
		{
			this.client = new dwarfkeeper.DwarfClient(ip_addr, port_num);
		}

		public bool get_running_status()
		{
			return this.is_running;
		}

		private void send_message(string args)
		{
			if (String.IsNullOrWhiteSpace(args))
			{
				Console.Write("\nEnter Message: ");
				String msg = Console.ReadLine();
				this.client.send_message(msg);
			} else {
				this.client.send_message(args);
			}

			return;
		}

		private void connection_status(string args)
		{
			Console.WriteLine("Connection status: " +
								(this.client.get_connection_status() ? "Open" : "Closed"));
			return;
		}
		
		private void disconnect(string args)
		{
			this.client.close_connection();
			return;
		}

		private void connect(string args)
		{
			this.client.connect(5, 1000);
			return;
		}

		private void exit(string args)
		{
			this.disconnect(args);
			this.is_running = false;
			return;
		}

		private void parse_cmd(string cmd)
		{
			//TODO: Parse cmd, seperate command from command args
			if (String.IsNullOrWhiteSpace(cmd))
			{
				return;
			}

			string[] cmd_and_args = cmd.Split(new Char[] {' '}, 2);
			string args = null;

			cmd = cmd_and_args[0];

			try
			{
				args = cmd_and_args[1];
			} catch (IndexOutOfRangeException oor_e) {
				;
			}
			

			try
			{
				dwarf_cmds[cmd](args);
			} catch (KeyNotFoundException k_e) {
				Console.WriteLine("Unrecognized Command: "+cmd);
			} catch (ArgumentNullException an_e){
				;
			}
			return;
		}


		static void Main(string[] args) 
		{
			DwarfCLI cli = null;

			// Setup CLI for server connection
			if (args.Length == 0)
			{
				cli = new DwarfCLI("10.32.215.135", 9845);
			} else {
				cli = new DwarfCLI(args[0]);
			}

			// Event loop
			while (cli.get_running_status()) // Loop indefinitely
			{
				Console.Write("\n>>DwarfKeeper: "); // Prompt
				string line = Console.ReadLine(); // Get string from user
				cli.parse_cmd(line);
			}

		}
	}
}

