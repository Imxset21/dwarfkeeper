using System;
using System.Collections.Generic;

using Isis;

namespace DwarfCMD
{
    public delegate void dwarfCmd(string args);

    [AutoMarshalled]
    public class DwarfCommand {
       public CMDCode opcode;
       public string args;

       public DwarfCommand() {}

       public DwarfCommand(CMDCode opcode, string args) {
           this.args = args;
           this.opcode = opcode;
       }
    }
    
    public enum CMDCode {
        MESSAGE
    }
        
}
