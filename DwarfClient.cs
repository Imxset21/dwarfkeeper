using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

using DwarfCMD;
using DwarfData;
using Isis;

namespace DwarfKeeper
{
	public class DwarfClient
	{
        Client dclient;

		/** Creates a CLI instance with input of the form IP_ADDR:PORT_NUM
		 * 
		 * @param	serverIpPort	String containing IP_ADDR:PORT_NUM
		 */
		public DwarfClient(string groupname)
		{
            IsisSystem.Start();
            Isis.Msg.RegisterType(typeof(DwarfCommand), 111);
            Isis.Msg.RegisterType(typeof(DwarfStat), 113);
            dclient = new Client(groupname);
		}
        

        public List<string> create(string path, string data) {
            string args = path + " " + data;
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.CREATE, args),
                    new EOLMarker(),
                    retlst);
            return retlst;
        }

        public List<string> test(string args) {
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.TEST, args),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }

        public List<string> getChildren(string path) {
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_CHILDREN, path),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }

        public List<DwarfStat> getChildren2(string path) {
            List<DwarfStat> retlst = new List<DwarfStat>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_CHILDREN2, path),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }

        public List<string> delete(string path) {
            List<string> retlst = new List<string>();
            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.DELETE, path),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }

        public List<string> setNode(string path, string data) {
            string args = path + " " + data;
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.SET_NODE, args),
                    new EOLMarker(),
                    retlst);
            return retlst;
        }

        public List<DwarfStat> getNodeAll(string path) {
            List<DwarfStat> retlst = new List<DwarfStat>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_ALL, path),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }
	}
}

