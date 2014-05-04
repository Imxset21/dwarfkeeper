using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;

using DwarfCMD;
using DwarfData;
using Isis;

namespace DwarfListener
{
	public abstract class DwarfListener
	{
		protected const int DEFAULT_PORT_NUM = 9845;  //!< Default port number (Isis' default + 2)

		//! Handler delegate for Isis2 Commands from other server instances
		protected delegate void dwarfIsisHandler(DwarfCommand args);

		//! Handler delegate for Operations over the tree
		protected delegate void dwarfOpHandler(string args);
		
		/////////////////////////
		//  Dwarf Server State //
		/////////////////////////

		//! Underlying node file system
        protected DwarfTree nodeSys;

		//! Op-Code to Method dictionary for handling requests
        protected Dictionary<DwarfCode, dwarfOpHandler> dwarfOps;

		//! Isis2 Group this server will join
        protected Group dwarfGroup;
        protected Group dwarfSubGroup;

		/////////////////////////////// 
		// Dwarf Server Constructors //
		///////////////////////////////



        /**
         * Create a new DwarfServer instance, with the given groupname.
         *
         * If this groupname is not already a logger, also join the logger group.
         *
         * @param groupname The name of the group for this server to join
         * @param update_handler An dwarfIsisHandler for handling an UPDATE operation sent to the
         *  entire group
         */
		protected DwarfListener(string groupname)
		{
            this.nodeSys = DwarfTree.CreateTree();

            this.dwarfGroup = new Group(groupname);

            this.initDwarfHandlers();

            Isis.Msg.RegisterType(typeof(DwarfCommand), 111);
            Isis.Msg.RegisterType(typeof(DwarfStat), 113);
		}


        protected void joinGroup() {
            dwarfGroup.Join();
        }
        protected void joinSubGroup() {
            dwarfSubGroup.Join();
        }

		//////////////////////////////////////
		// Protected Initialization Methods //
		//////////////////////////////////////

		/**
		 * Initializes the dwarfOps dictionary with this class' methods.
		 */
        protected abstract void initDwarfHandlers();


        /**************************************************
         *
         * DwarfTree Manipulation Functions
         *
         **************************************************/

        
        /** Create the node at path - CLI command is "create".
		 * 
		 * This method performs an in-order query to all the other
		 * servers in the group before writing the new data.
		 * The write only succeeds if all other servers
		 * have responded correctly to the query, including this one.
		 *
		 * The local parameter is used to distinguish between 
		 * two situations where this method is called:
		 * 
		 * 1) The client invokes us externally. In this case,
		 *    local is FALSE, and we send out an ordered query
		 *    to all other servers in the group, including 
		 *    this server. 
		 * 2) We've been invoked by another server's query
		 *    or our own. In either case, we attempt to write
		 *    to our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query
         */
		protected abstract void create(string args);
		
		/**
		 * Deletes the selected node from the tree.
		 *
		 * This method performs an in-order query to all the other
		 * servers in the group before deleting the data.
		 * The delete only succeeds if all other servers
		 * have responded correctly to the query, including this one.
		 *
		 * The local parameter is used to distinguish between 
		 * two situations where this method is called:
		 * 
		 * 1) The client invokes us externally. In this case,
		 *    local is FALSE, and we send out an ordered query
		 *    to all other servers in the group, including 
		 *    this server. 
		 * 2) We've been invoked by another server's query
		 *    or our own. In either case, we attempt to delete
		 *    nodes on our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query.
		 *
		 */
		protected abstract void delete(string args);

		/**
		 * Gets the value of a given node.
		 * 
		 * Note that we only support local gets, so the local parameter
		 * is ignored.
		 *
		 * @param[in]    args    Path to node to get value of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected abstract void getNode(string args);

		/**
		 * Sets the value of a given node.
		 * 
		 * This method performs an in-order query to all the other
		 * servers in the group before setting the node.
		 * The set only succeeds if all other servers
		 * have responded correctly to the query, including this one.
		 *
		 * The local parameter is used to distinguish between 
		 * two situations where this method is called:
		 * 
		 * 1) The client invokes us externally. In this case,
		 *    local is FALSE, and we send out an ordered query
		 *    to all other servers in the group, including 
		 *    this server. 
		 * 2) We've been invoked by another server's query
		 *    or our own. In either case, we attempt to delete
		 *    nodes on our tree and report the results back.
		 *
		 * @param[in]    args    Combined path and data to use.
		 * @param[in]    local   FALSE if this is an external query.
		 *
		 */
		protected abstract void setNode(string args);
		
		
		/**
		 * Checks if the a given node exists.
		 * 
		 * Note that we only support local exists, so the local parameter
		 * is ignored.
		 *
		 * @param[in]    args    Path to node to check existance of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected abstract void exists(string args);

		/**
		 * Gets the children of a particular node.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children of.
		 * @param[in]    local   Ignored in this function.
		 */
        protected abstract void getChildren(string args);
		
		/**
		 * Gets the children of a particular node.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children of.
		 * @param[in]    local   Ignored in this function.
		 */
		protected abstract void getChildren2(string args);
		
		/**
		 * Gets the children of a particular node and their data.
		 * 
		 * Note that we only support local tree lookups, so the
		 * local parameter is ignored.
		 *
		 * @param[in]    args    Path to node to get children and their data from.
		 * @param[in]    local   Ignored in this function.
		 */
        protected abstract void getNodeAll(string args);

        protected virtual void printTreeLoop() 
		{
            while (true) {
                nodeSys.printTree();
                Thread.Sleep(5000);
            }
        }
    }
}

