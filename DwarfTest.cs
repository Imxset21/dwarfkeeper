using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using DwarfLogger;
using DwarfKeeper;
using DwarfData;

namespace DwarfTest {
    public class DwarfTest {

        static void Main(string[] args) {
            // testDwarfTree();
            //testDwarfTreeCopy();
            //testClientTreeManip();
			logTreeLoad();
			// logTreeTest();
        }

		static void logTreeLoad()
		{
			/*
			DwarfTreeRecord tr2 = null;
			object blah = null;
			BinaryFormatter b = new BinaryFormatter();
			
			using(FileStream treeFileStream =
				  new FileStream(
					  DwarfLogger.DwarfLogger.DEFAULT_TREE_FILENAME,
					  FileMode.Open,
					  FileAccess.Read
					  ))
			{
				blah = b.Deserialize(treeFileStream);
			}
			
			if (blah is DwarfTreeRecord)
			{
				tr2 = (DwarfTreeRecord) blah;
				tr2.tree.printTree();
			} else {
				Console.Write("Fuck you.");
			}
			*/
			DwarfTree t = new DwarfTree(DwarfTree.loadTree("dwarf_tree.dat"));
			t.printTree();
		}	
		

		static void logTreeTest()
		{
            DwarfTree tree = DwarfTree.CreateTree(); 

			tree.addNode("/mynode", "10");
			tree.addNode("/mynode/mynodechild", "111");
			tree.addNode("/otherNode", "20");
			tree.addNode("/otherNode/otherNodeChild", "222");
			tree.addNode("/otherNode/otherNodeChild/otherNodeGrandChild", "22022");
			tree.printTree();

			DwarfTreeRecord tr = new DwarfTreeRecord(tree, 5);
			DwarfTreeRecord tr2 = null;
			BinaryFormatter b = new BinaryFormatter();

			using(FileStream treeFileStream = new FileStream(
					DwarfLogger.DwarfLogger.DEFAULT_TREE_FILENAME,
					FileMode.Create,
					FileAccess.ReadWrite
					))
			{
				b.Serialize(treeFileStream, tr);
			}

			b = new BinaryFormatter();

			using(FileStream treeFileStream = new FileStream(
					DwarfLogger.DwarfLogger.DEFAULT_TREE_FILENAME,
					FileMode.Open,
					FileAccess.Read
					))
			{
				tr2 = (DwarfTreeRecord) b.Deserialize(treeFileStream);
			}
			tr2.tree.printTree();
		}

        static void testDwarfTreeCopy() {
            DwarfTree tree = DwarfTree.CreateTree(); 

			tree.addNode("/mynode", "10");
			tree.addNode("/mynode/mynodechild", "111");
			tree.addNode("/otherNode", "20");
			tree.addNode("/otherNode/otherNodeChild", "222");
			tree.addNode("/otherNode/otherNodeChild/otherNodeGrandChild", "22022");
			tree.addNode("/thirdNode", "33");
			tree.printTree();

            DwarfTree newTree = new DwarfTree(tree);
            newTree.printTree();

			tree.removeNode("/otherNode/otherNodeChild");
            tree.printTree();
            newTree.printTree();
        }

        static void testClientTreeManip() {
            DwarfClient dclient = new DwarfClient("dwarfkeeper");
            Console.WriteLine();

            string strReps;
            DwarfStat statReps;

            //Add a node mynode
            Console.WriteLine(
                "***** Adding node /mynode with data \"GROOMP\" ******");
            strReps = dclient.create("/mynode", "GROOMP");
            Trace.Assert(strReps.Equals("/mynode"));
            Console.WriteLine(strReps + "\n");
            statReps = dclient.getNodeAll("/mynode");
            Trace.Assert(statReps.name.Equals("mynode"));
            statReps.printStat();
            Console.WriteLine("\n");
            
            //Try to add a node that already exists
            Console.WriteLine(
                "***** Adding node /mynode with data \"GROOMP2\" ******");
            strReps = dclient.create("/mynode", "GROOMP2");
            Trace.Assert(strReps.Equals("/mynode"));
            Console.WriteLine(strReps + "\n");
            statReps = dclient.getNodeAll("/mynode");
            Trace.Assert(statReps.name.Equals("mynode"));
            statReps.printStat();
            Console.WriteLine("\n");

            //Try to access a node that does not exist
            Console.WriteLine("***** Trying to access a node that " +
                    "has not been created *****");
            statReps = dclient.getNodeAll("/othernode");
            Trace.Assert(!statReps.err.Equals(""));
            statReps.printStat();
            Console.WriteLine("\n");
            
            //Try to create an invalid node
            Console.WriteLine("***** Trying to create a node under a parent " +
                    "that does not exist *****");
            strReps = dclient.create("/othernode/that", "GROOMP2");
            Trace.Assert(!strReps.Equals("/othernode/that"));
            Console.WriteLine(strReps + "\n");
            statReps = dclient.getNodeAll("/othernode/that");
            Trace.Assert(!statReps.err.Equals(""));
            statReps.printStat();
            Console.WriteLine("\n");

            //Try to change the Data at /mynode
            Console.WriteLine("***** Changing the data at /nynode from " +
                    "\"GROOMP\" to \"NEW_GROOMP\" *****");
            strReps = dclient.setNode("/mynode", "NEW_GROOMP");
            Trace.Assert(strReps.Equals("NEW_GROOMP"));
            Console.WriteLine(strReps + "\n");
            statReps = dclient.getNodeAll("/mynode");
            Trace.Assert(statReps.data.Equals("NEW_GROOMP"));
            statReps.printStat();
            Console.WriteLine("\n");


            //Create a second layer node /mynode/whatnode
            Console.WriteLine( "***** Adding node /mynode/whatnode " +
                    "with data \"PUMBLOOM\" ******");
            strReps = dclient.create("/mynode/whatnode", "PUMBLOOM");
            Trace.Assert(strReps.Equals("/mynode/whatnode"));
            Console.WriteLine(strReps + "\n");
            statReps = dclient.getNodeAll("/mynode/whatnode");
            Trace.Assert(statReps.name.Equals("whatnode"));
            statReps.printStat();
            Console.WriteLine("\n");

            //disconnect
            dclient.disconnect();
        }

        /** Tests for the DwarfTree class. 
         */
		static void testDwarfTree() {
			DwarfTree tree = DwarfTree.CreateTree();
			tree.printTree();

			Console.WriteLine("\n*** Init Tree / Adding Nodes ***\n");
			tree.addNode("/mynode", "10");
			tree.addNode("/mynode/mynodechild", "111");
			tree.addNode("/otherNode", "20");
			tree.addNode("/otherNode/otherNodeChild", "222");
			tree.addNode("/otherNode/otherNodeChild/otherNodeGrandChild", "22022");
			tree.addNode("/thirdNode", "33");
			tree.printTree();

			Console.WriteLine("\n*** Removing Node that does not exist ***\n");
			tree.removeNode("/otherNodeChild");
			tree.printTree();

			Console.WriteLine("\n*** Removing subtree at /otherNode/otherNodeChild ***\n");
			tree.removeNode("/otherNode/otherNodeChild");
			tree.printTree();

			Console.WriteLine("\n*** Attempting To Remove Root (/) ***\n");
			tree.removeNode("/");
			tree.printTree();

			Console.WriteLine("\n***** Serializing to Disk ****\n");
			tree.writeTree("tree.dat");

			Console.WriteLine("\n***** Deserializing from Disk ****\n");
			DwarfTree newtree = DwarfTree.loadTree("tree.dat");
			newtree.printTree();

			Console.WriteLine("\n*** Adding /fourthnode ***\n");
			newtree.addNode("/fourthnode", "ALL PRAISE DOME!");

			Console.WriteLine("\n***** Serializing (Again) ****\n");
			newtree.writeTree("tree.dat");

			Console.WriteLine("\n***** Deserializing (Again) ****\n");
			tree = DwarfTree.loadTree("tree.dat");

            tree.printTree();

            Console.WriteLine("\n***** Setting data on /mynode to KEEPER  ****\n");
            System.Threading.Thread.Sleep(2000);
            tree.setData("/mynode", "KEEPER");
            tree.printTree();

            Console.WriteLine("\n***** Checking stat() on /mynode  ****\n");
            DwarfStat mynodestat = tree.getNodeInfo("/mynode");
            mynodestat.printStat();
		}
    }
}
