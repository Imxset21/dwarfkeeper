using System;
using System.Collections.Generic;

namespace DwarfCMD
{
    public delegate void dwarfCmd(string args);
        
    public abstract class DwarfCMD
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
}
