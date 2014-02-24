using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace DwarfTree
{
	//TODO Serizalization: For persistant state and/or data transfer
	[Serializable]
	public class DwarfTree
	{
		public string name { get; private set;} //!< The "name" of the node at the head of this (sub)tree
		public string data { get; private set;} //!< The data contained by this node
		Dictionary<string, DwarfTree> children; //!< The immediate children of this node


		/** Constructor is only needed for internal purposes.
		 * Factory method will be used to produce completely new trees
		 */
		private DwarfTree(string name = "", string data = "")
		{
			this.name = name;
			this.data = data;
			children = new Dictionary<string, DwarfTree>();
		}


		/** Simple factory method to start a new tree.
		 */
		public static DwarfTree createTree(string path = "") {
			DwarfTree tree;
			if(!path.Equals("") && (tree = loadTree(path)) == null) {
				return tree;
			}
			return new DwarfTree();
		}


		/** Add a node at location path with data newdata.
		 * 
		 * @param path the full path to the new node
		 * @param newdata the data to populate the new node with
		 */
		public bool addNode(string path, string newdata) {
			DwarfTree subtree;
			string loc;
			string[] path_elements = 
				path.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
			int pathlen = path_elements.Length;

			if(pathlen == 0) {
				return false;
			}

			// We're doing this iteratively so we won't overload the stack
			Dictionary<string, DwarfTree> cur_children = children;
			for (int i = 0; i < pathlen; i++) {
				loc = path_elements[i];

				// Traverse as far down the tree as possible,
				// if the node already exists, we will automatically leave the loop
				if (cur_children.TryGetValue(loc, out subtree)) {
					cur_children = subtree.children;
				} else {
					if (i < (pathlen - 1)) { // we don't do recursive adds
						return false;
					}
					cur_children.Add(loc, new DwarfTree(loc, newdata));
				}
			}
			return false;
		}


		/** Remove the node/subtree at location path.
		 * If the node is the root of a subtree, the entire subtree will be removed
		 * 
		 * @param path the node to remove
		 */
		public bool removeNode(string path) {
			DwarfTree subtree;
			string loc;
			string[] path_elements = 
				path.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
			int pathlen = path_elements.Length;

			if(pathlen == 0) {
				return false;
			}

			// Iterate through the tree to find parent node of node to remove
			Dictionary<string, DwarfTree> cur_children = children;
			for (int i = 0; i < (pathlen - 1); i++) {
				loc = path_elements[i];
				if (cur_children.TryGetValue(loc, out subtree)) {
					cur_children = subtree.children;
				} else {
					return false;
				}
			}

			// Will just return false if loc is not present
			return cur_children.Remove(path_elements[pathlen - 1]);
		}


		/** Print out the tree, with node names and data
		 */
		public void printTree() {
			Console.WriteLine("\n##### TREE START #####");
			Console.WriteLine("/");
			printTreeHelper(children);
			Console.WriteLine("##### TREE END #####\n");
		}
		/** Recursive helper method for printTree()
		 * 
		 * @param tree The tree (or subtree) to print
		 * @param level The level this subtree is rooted at (for indentation purposes
		 */
		private void printTreeHelper(Dictionary<string, DwarfTree> tree, int level = 1) {
			DwarfTree subtree;
			foreach (string child in tree.Keys) {
				tree.TryGetValue(child, out subtree);
				Console.WriteLine(new String('\t', level) + "/" + child + ' ' + subtree.data);
				printTreeHelper(subtree.children, level + 1);
			}
		}

		/** Write this DwarfTree to the file given by path.
		 */
		public void writeTree(string path) {
			Stream s = new FileStream(path, FileMode.Create);
			BinaryFormatter b = new BinaryFormatter();
			b.Serialize(s, this);
			s.Close();
		}

		public static DwarfTree loadTree(string path) {
			if(!File.Exists(path)) {
				return null;
			}

			DwarfTree tree;
			Stream s = new FileStream(path, FileMode.Open);
			BinaryFormatter b = new BinaryFormatter();
			tree = (DwarfTree)b.Deserialize(s);
			s.Close();
			return tree;
		}

		static void Main(string[] args) {
			DwarfTree tree = DwarfTree.createTree();
			tree.printTree();

			Console.WriteLine("\n### Init Tree / Adding Nodes ###\n");
			tree.addNode("/mynode", "10");
			tree.addNode("/mynode/mynodechild", "111");
			tree.addNode("/otherNode", "20");
			tree.addNode("/otherNode/otherNodeChild", "222");
			tree.addNode("/otherNode/otherNodeChild/otherNodeGrandChild", "22022");
			tree.addNode("/thirdNode", "33");
			tree.printTree();

			Console.WriteLine("\n### Removing Node that does not exist ###\n");
			tree.removeNode("/otherNodeChild");
			tree.printTree();

			Console.WriteLine("\n### Removing subtree ###\n");
			tree.removeNode("/otherNode/otherNodeChild");
			tree.printTree();

			Console.WriteLine("\n### Attempting To Remove Root (/) ###\n");
			tree.removeNode("/");
			tree.printTree();

			Console.WriteLine("\n##### Serializing ####\n");
			tree.writeTree("tree.dat");

			Console.WriteLine("\n##### Deserializing ####\n");
			DwarfTree newtree = loadTree("tree.dat");
			newtree.printTree();

			Console.WriteLine("\n### Adding /fourthnode ###\n");
			tree.addNode("/fourthnode", "ALL PRAISE DOME!");

			Console.WriteLine("\n##### Serializing (Again) ####\n");
			tree.writeTree("tree.dat");

			Console.WriteLine("\n##### Deserializing (Again) ####\n");
			newtree = loadTree("tree.dat");

			newtree.printTree();
		}
	}
}

