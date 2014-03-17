using System;
using System.Collections.Generic;

using Isis;

namespace DwarfCMD
{
    public delegate void dwarfCmd(string args);

    [AutoMarshalled]
    public class DwarfCommand {
        public int opCode;
        public string args;

        public DwarfCommand() {}

        public DwarfCommand(int opcode, string args) {
           this.args = args;
           this.opCode = opcode;
        }

    }
    
    public enum DwarfCode {
        MESSAGE = 0,
        CREATE,
        GET_CHILDREN,
        GET_CHILDREN2,
        DELETE,
        EXISTS,
        GET_ALL,
        GET_NODE,
        SET_NODE,
        TEST
    }

    public enum IsisDwarfCode {
        OPCODE = 0
    }
}
