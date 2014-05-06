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

        //! Storage queue for commands that have been recieved but cannot yet be handled
        protected Queue<DwarfCommand> cmdqueue;

		//! Op-Code to Method dictionary for handling requests
        protected Dictionary<DwarfCode, dwarfOpHandler> dwarfOps;

		//! Isis2 Group this server will join
        protected Group dwarfGroup;
        protected Group dwarfSubGroup;

        //! The Adress of this server (for P2P communication)
        protected Address address;

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

            address = IsisSystem.GetMyAddress();
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
		

        protected virtual void printTreeLoop() 
		{
            while (true) {
                nodeSys.printTree();
                Thread.Sleep(5000);
            }
        }
        
		protected virtual void sync(string args)
		{
            dwarfGroup.Flush();
		}
    }
}

