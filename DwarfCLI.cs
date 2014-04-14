using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Collections.Generic;

using DwarfKeeper; // Import Client
using DwarfData;
using DwarfCMD;
using Isis;

//FIXME:	There is a bug that if send_message is the first command, 
//			(without any setup) the client crashes with a null pointer exception.

namespace DwarfCLI
{
    delegate string dwarfFun(string[] args);

	public class DwarfCLI
	{
		private static bool isRunning = false;                    //!< Current running status
		private static DwarfClient client;                        //!< Client backend
        private static string prompt = "DwarfKeeper>>";
        private static Dictionary<string, dwarfFun> dwarfFuns;

        private static List<string> noArgCmds = new List<string>() {"disconnect", "exit"};

        static void initCLI(String groupname = "") {
		    if(string.IsNullOrWhiteSpace(groupname)) {
                client = null;
            } else {
                connect(new string[]{groupname});
            }
            isRunning = true;
        }

        static void initCommands() {
			// Setup delegate dictionary
			dwarfFuns = new Dictionary<string, dwarfFun>();
			dwarfFuns["connect"] = (dwarfFun)connect;
			dwarfFuns["disconnect"] = (dwarfFun)disconnect;
			dwarfFuns["exit"] = (dwarfFun)exit;
            dwarfFuns["test"] = (dwarfFun)test;
            dwarfFuns["create"] = (dwarfFun)create;
            dwarfFuns["setNode"] = (dwarfFun)setNode;
            dwarfFuns["get"] = (dwarfFun)getNode;
            dwarfFuns["ls"] = (dwarfFun)getChildren;
            dwarfFuns["ls2"] = (dwarfFun)getChildren2;
        }

		/** Gets current running status.
		 *
		 * @return Running status
		 */
		static bool getRunningStatus()
		{
			return isRunning;
		}

        static string create(string[] args) {
            if(args.Length < 2) {
                return "Error: Not enought aruments";
            }
            List<string> retlst = client.create(args[0], args[1]);
            return string.Join(" -- ", retlst);
        }

        static string setNode(string[] args) {
            if(args.Length < 2) {
                return "Error: Not enought aruments";
            }
            List<string> retlst = client.setNode(args[0], args[1]);
            return string.Join(" -- ", retlst);
        }

        static string getNode(string[] args) {
            List<DwarfStat> retlst = client.getNodeAll(args[0]);
            return retlst[0].ToString();
        }

        static string getChildren(string[] args) {
            List<string> retlst = client.getChildren(args[0]);
            return retlst[0];
        }
        static string getChildren2(string[] args) {
            List<DwarfStat> retlst = client.getChildren2(args[0]);
            return retlst[0].ToString();
        }

        static string test(string[] args) {
            List<string> retlst = client.test(args[0]);
            return string.Join(" -- ", retlst);
        }

		/** Disconnects (closes connection to) the server.
		 *
		 *
		 */
		static string disconnect(string[] args)
		{
			client.disconnect();
            prompt = "DwarfKeeper>>";
			return "Disconnected";
		}

		/** Connects to the server.
		 *
		 * @param args Arguments regarding connection attempts/timeout
		 */
		static string connect(string[] args)
		{
            if(client != null) {
                return "Client is already connected to a server.";
            }

            client = new DwarfClient(args[0]);
            prompt = string.Format("{0}>>", args);
            return string.Format("Connected to {0}", args);
		}

		/** Disconnects from server and exits the interface.
		 *
		 * @param args Arguments to pass to disconnect().
		 */
		static string exit(string[] args)
		{
			disconnect(args);
			isRunning = false;
			return "Goodbye";
		}
        
        static string handleCMD(string input) {
            string cmd;
            string[] args;

            if(String.IsNullOrWhiteSpace(input)) {
                return "";
            }

            string[] cmdAndArgs = 
                input.Split(new char[]{' '}, 2, StringSplitOptions.RemoveEmptyEntries);

            
            cmd = cmdAndArgs[0];

            if(!isRunning && !cmd.EndsWith("connect")) {
                return "Client is not connected";
            }

            if (1 == cmdAndArgs.Length) {
                if(!noArgCmds.Contains(cmd)) {
                    return "Arguments required for cmd: " + cmd;
                }
                args = new string[]{};
            } else {
                args=cmdAndArgs[1].Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
            }

            try {
                return dwarfFuns[cmd](args);
            } catch (KeyNotFoundException knfe) {
                //TODO: Handle - Print help msg?
            }
            return "";
        }

        static void eventLoop() {
            while(isRunning) {
				Console.Write("\n " + prompt + " "); // Prompt
				string line = Console.ReadLine(); // Get string from user
                Console.WriteLine(handleCMD(line));
            }
        }

		static void Main(string[] args) 
		{
            initCommands();

            if(0 == args.Length) {
                initCLI("");
            } else {
                initCLI(args[0]);
            }

            eventLoop();

		}
	}
}

