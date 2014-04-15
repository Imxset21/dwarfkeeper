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
        Client dclient; //!< The Isis2 client used to send requests

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

        /** Disconnect this client from the Isis group (if it is connected).
         * This involves shutting down Isis if it is is active.
         */
        public void disconnect() {
            if(IsisSystem.IsisIsActive()) {
                IsisSystem.Shutdown();
            }
        }
        

        /** 
         * Try to create the node at <path> containing <data>.
         *
         * A completed create will just return the node that was written (should
         * be equivalent to the <path> argument. Otherwise, the returned string
         * will contain the error that occurred.
         *
         * @param[in]   path    The path at which we want to create a node
         * @param[in]   data    The data to store at the node
         * @return The path on a successful create, error msg otherwise.
         */
        public string create(string path, string data) {
            string args = path + " " + data;
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.CREATE, args),
                    new EOLMarker(),
                    retlst);
            return retlst[0];
        }


        public string test(string args) {
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.TEST, args),
                    new EOLMarker(),
                    retlst);

            return retlst[0];
        }

        /**
         * Get a (comma-delimited) list of the names of the children of the node at <path>.
         *
         * The returned string will be an error message (starting with "Error:") 
         * if the list of children cannot be retrieved.
         *
         * @param[in]   path    The path to the node whose children we want.
         * @return Comma-delimited (',') list of the names of the children of the 
         *  node, error message otherwise.
         */
        public string getChildren(string path) {
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_CHILDREN, path),
                    new EOLMarker(),
                    retlst);

            return retlst[0];
        }

        /**
         * Get a DwarfStat object containing information about the node at <path>.
         *
         * Includes ','-delimited list of children, but node data is not included.
         * The Error field of the returned DwarfStat object will contain error details
         * if there is an error. All other fields will be blank (or obviously false) in
         * this case.
         *
         * @param[in]   path    The path to the wanted node
         * @return      A DwarfStat object with the requested information or an error message.
         */
        public DwarfStat getChildren2(string path) {
            List<DwarfStat> retlst = new List<DwarfStat>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_CHILDREN2, path),
                    new EOLMarker(),
                    retlst);

            return retlst[0];
        }

        /**
         * Delete the node found a <path> if it exists.
         *
         * If the node is the root of a subtree (e.g. has children), then
         *  the entire subtree will be removed (recursive delete).
         * The function will return the path to the deleted node (should be
         *  the same as the passed path) if the delete is successful, otherwise
         *  an error message will be returned (beginning with "Error:").
         * 
         * @param[in]   path    The path to the node to be deleted
         * @return      The path to the deleted node on success, an error message otherwise.
         */
        public List<string> delete(string path) {
            List<string> retlst = new List<string>();
            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.DELETE, path),
                    new EOLMarker(),
                    retlst);

            return retlst;
        }


        /**
         * Set the data at the node at <path> to <data>.
         *
         * Replies with the written data on a successful write, and returns
         * an error message otherwise.
         * Does NOT create a node if the path cannot be found.
         *
         * @param[in]   path    The path to the node we want to set
         * @param[in]   data    The data to set the node to
         * @return The written data on success, and an error message otherwise.
         */
        public string setNode(string path, string data) {
            string args = path + " " + data;
            List<string> retlst = new List<string>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.SET_NODE, args),
                    new EOLMarker(),
                    retlst);
            return retlst[0];
        }

        /**
         * Get all of the information about the node at <path>.
         *
         * @param   path    The path to the wanted node
         * @return DwarfStat object will the full details of the node,
         *  including list of children and the node's data.
         */
        public DwarfStat getNodeAll(string path) {
            List<DwarfStat> retlst = new List<DwarfStat>();

            this.dclient.P2PQuery((int)IsisDwarfCode.OPCODE,
                    new DwarfCommand((int)DwarfCode.GET_ALL, path),
                    new EOLMarker(),
                    retlst);

            return retlst[0];
        }
	}
}

