using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace DwarfTree
{
	[Serializable]
	public class DwarfTree
	{
        //! The "name" of the node at the head of this (sub)tree
		public string name { get; private set;} 
        //! The data contained by this node
        string Data;
		public string data {
            get {
                return Data;
            }
            private set {
                mtime = System.DateTime.UtcNow.ToString();
                Data = value;
            }
        }
        public string ctime {get; private set;} //!< The creation time for this node
        public string mtime {get; private set;} //!< The modification time for this node
		Dictionary<string, DwarfTree> children; //!< The immediate children of this node

        private readonly char[] pathdelim = new char[] {'/'};

		/** Constructor is only needed for internal purposes.
		 * Factory method will be used to produce completely new trees
		 */
		private DwarfTree(string name = "", string data = "")
		{
			this.name = name;
			this.data = data;
			children = new Dictionary<string, DwarfTree>();
            ctime = System.DateTime.UtcNow.ToString();
            mtime = ctime;
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
        

        /** A set of information about the node given by path.
         *
         * @param path The path to the wanted node.
         * @return A <string, string> dictionary from field/info name to value
         */
        public Dictionary<string, string> stat(string path) {
            DwarfTree tree = this.findNode(path);
            if(tree == null) {
                return null;
            }
            
            return new Dictionary<string, string>() {
                {"name", tree.name},
                {"ctime", tree.ctime},
                {"mtime", tree.mtime},
                {"numChildren", tree.children.Count.ToString()}
            };
        }

        
        /** Set the node at path to contain new data
         *
         * @param path The path to the wanted node.
         * @param data The new data for the node.
         * @return True if the data is successfully set, false otherwise.
         */
        public bool setData(string path, string data) {
            DwarfTree tree = this.findNode(path);
            if(tree == null) {
                return false;
            }
            tree.data = data;
            return true;
        }

        /** Get the data from the node given by path.
         *
         * @param path The path to the wanted node
         * @return The data contained by the node, null if the node does not exist.
         */
        public string getData(string path) {
            DwarfTree tree = this.findNode(path);
            if(tree == null) {
                return null;
            }
            return tree.data;
        }

		/** Add a node at location path with data newdata.
		 * 
		 * @param path the full path to the new node
		 * @param newdata the data to populate the new node with
		 */
		public bool addNode(string path, string newdata) {
            DwarfTree subtree = this.findNode(path, stop : 1);

            if(null == subtree) {
                return false;
            }

            string[] path_arr = path.Split(pathdelim, StringSplitOptions.RemoveEmptyEntries);
            if (0 == path_arr.Length) {
                return false;
            }
            string newNode = path_arr[path_arr.Length - 1];
            try {
                subtree.children.Add(newNode, new DwarfTree(newNode, newdata));
            } catch (ArgumentException) {
                return false;
            }
            return true;
		}


		/** Remove the node/subtree at location path.
		 * If the node is the root of a subtree, the entire subtree will be removed
		 * 
		 * @param path the node to remove
		 */
		public bool removeNode(string path) {
            DwarfTree subtree = this.findNode(path, stop : 1);
            if(subtree == null) {
                return false;
            }
            
            string[] path_arr = path.Split(pathdelim, StringSplitOptions.RemoveEmptyEntries);
            if (0 == path_arr.Length) {
                return false;
            }
            string newNode = path_arr[path_arr.Length - 1];
            
			// Will just return false if loc is not present
			return subtree.children.Remove(newNode);
		}


        /** Return the DwarfTree at location path if it exists, return null otherwise.
         * 
         * @param path The location of the node/subtree
         * @param stop The number of nodes before the end of the path to stop (e.g stop=1 will
         *             omit the last 1 node in the path during the search)
         */
        public DwarfTree findNode(string path, int stop = 0) {
            DwarfTree subtree = this;
            string loc;
			string[] path_elements = 
				path.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
			int pathlen = path_elements.Length;

			if(pathlen == 0) {
                if(path.Equals("/")) {
                    return subtree;
                }
				return null;
			}
            
			Dictionary<string, DwarfTree> cur_children = children;
			for (int i = 0; i < (pathlen - stop); i++) {
				loc = path_elements[i];
				if (cur_children.TryGetValue(loc, out subtree)) {
					cur_children = subtree.children;
				} else {
					return null;
				}
			}
            return subtree;
        }


		/** Print out the tree, with node names and data
		 */
		public void printTree() {
			Console.WriteLine("\n##### TREE START #####");
			Console.WriteLine("/");
			printTreeHelper(this.children);
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
				Console.WriteLine(new String('\t', level) + "/"
                           + child + " || " + subtree.data + " || " + subtree.mtime);
				printTreeHelper(subtree.children, level + 1);
			}
		}

		/** Write this DwarfTree to the file given by path.
         * Any existing file at that location will be overwritten.
         *
         * @param path The file to write the tree to.
		 */
		public void writeTree(string path) {
			Stream s = new FileStream(path, FileMode.Create);
			BinaryFormatter b = new BinaryFormatter();
			b.Serialize(s, this);
			s.Close();
		}

        /** Load a DwarfTree from the file (as written by writeTree()) located at path.
         *
         * @param path The path to the file to load the tree from.
         * @return The deserialized DwarfTree if possible, null on failure.
         */
		public static DwarfTree loadTree(string path) {
			if(!File.Exists(path)) {
				return null;
			}

			DwarfTree tree = null;
			Stream s = new FileStream(path, FileMode.Open);
			BinaryFormatter b = new BinaryFormatter();
            object expected_tree = b.Deserialize(s);
            if (expected_tree is DwarfTree) {
			    tree = (DwarfTree)expected_tree;
            }
			s.Close();
			return tree;
		}

        /** Tests for the DwarfTree class. */
		static void Main(string[] args) {
			DwarfTree tree = DwarfTree.createTree();
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
			DwarfTree newtree = loadTree("tree.dat");
			newtree.printTree();

			Console.WriteLine("\n*** Adding /fourthnode ***\n");
			newtree.addNode("/fourthnode", "ALL PRAISE DOME!");

			Console.WriteLine("\n***** Serializing (Again) ****\n");
			newtree.writeTree("tree.dat");

			Console.WriteLine("\n***** Deserializing (Again) ****\n");
			tree = loadTree("tree.dat");

            tree.printTree();

            Console.WriteLine("\n***** Setting data on /mynode to KEEPER  ****\n");
            System.Threading.Thread.Sleep(2000);
            tree.setData("/mynode", "KEEPER");
            tree.printTree();

            Console.WriteLine("\n***** Checking stat() on /mynode  ****\n");
            Dictionary<string, string> mynodestat = tree.stat("/mynode");
            foreach(var pair in mynodestat) {
                Console.WriteLine("\t{0} : {1}", pair.Key, pair.Value);
            }
		}
	}
}

