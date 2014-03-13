using System;
using System.Collections.Generic;

using Isis;

namespace DwarfCMD
{
    public delegate void dwarfCmd(string args);

    [AutoMarshalled]
    public class DwarfCommand {
    public const int MESSAGE = 0;

       public int opcode;
       public string args;

       public DwarfCommand() {}

       public DwarfCommand(int opcode, string args) {
           this.args = args;
           this.opcode = opcode;
       }


    }
    
}
