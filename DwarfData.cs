using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Isis;

namespace DwarfData
{
	[Serializable]
	public class DwarfTree
	{
        //! The "name" of the node at the head of this (sub)tree
		public string name { get; private set;} 

        string Data; //!< Private data field for this node
        //! The data contained by this node (public getter and setter)
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
		public Dictionary<string, DwarfTree> children; //!< The immediate children of this node
        public long dxid {get; private set;} //!< The transaction ID that set this node

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

        public DwarfTree(DwarfTree dt) {
            this.name = dt.name;
            this.data = dt.data;
            this.ctime = dt.ctime;
            this.mtime = dt.mtime;
            this.children = new Dictionary<string, DwarfTree>();

            foreach (string name in dt.children.Keys) {
                this.children.Add(name, new DwarfTree(dt.children[name]));
            }
        }

		/** Simple factory method to start a new tree.
         *
         * @param filepath The file from which to load the tree if wanted.
         *                  Defaults to "", which just creates a new tree.
         * @returns A new DwarfTree or tree loaded from disk if path is non-empty.
         *          Null if path is non-empty and loading from file fails
		 */
		public static DwarfTree CreateTree(string filepath = "") {
			if(!filepath.Equals("")) {
                return loadTree(filepath);
			}
			return new DwarfTree();
		}
        
        /** Set the node at path to contain new data
         *
         * @param path The path to the wanted node.
         * @param data The new data for the node.
         * @return True if the data is successfully set, false otherwise.
         */
        public bool setData(string path, string newData) {
            DwarfTree tree = this.findNode(path);
            if(tree == null) {
                return false;
            }
            tree.data = newData;
            return true;
        }

        /** Get information about the given node
         *
         * @param node The node (DwarfTree) that you want information about
         * @return A <string, string> dictionary from field/info name to value
         */
        public DwarfStat getNodeInfo(string path = null) {
            DwarfTree tree = this;
            if(null != path) {
                tree = this.findNode(path);
                if(null == tree) {
                    return null;
                }
            }
            
            return new DwarfStat(tree);
        }


        /** Check whether a node exists at a given path.
         *
         * @param path The path to the node you want to check
         * @return True if the node exists, false otherwise
         */
        public bool exists(string path = null) {
            return (null != findNode(path));
        }

        /** Get a list of the children of the node given by path.
         *
         * @param path The path to the wanted node
         * @return A sorted list of the child names
         */
        public string[] getChildList(string path) {
            DwarfTree tree = this.findNode(path);
            if(null == tree) {
                return null;
            }

            string[] childnames = tree.children.Keys.ToArray();
            System.Array.Sort(childnames);
            return childnames;
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
         *             omit the last 1 node in the path during the search).
         *             Must be a values in range [0, path length)
         */
        public DwarfTree findNode(string path, int stop = 0) {
            DwarfTree subtree = this;
            string loc;
			string[] path_elements = path.Split(pathdelim, StringSplitOptions.RemoveEmptyEntries);
			int pathlen = path_elements.Length;

			if(pathlen == 0) {
                if(path.Equals("/")) {
                    return subtree;
                }
				return null;
			} else if (stop > pathlen || stop < 0) {
                // Bounding the input
                return null;
            }
            
            // Iteratively search through tree until node 
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
	}


    [AutoMarshalled]
    public class DwarfStat {
        public string name = "";
        public string ctime = "";
        public string mtime = "";
        public int numChildren = -1;
        public string childlst = "";
        public string data = "";
        public string err = "";
        public string info = "";

        private DwarfTree tree;

        public DwarfStat() {}

        public DwarfStat(DwarfTree node) {
            this.tree = node;

            this.name = node.name;
            this.ctime = node.ctime;
            this.mtime = node.mtime;
            this.numChildren = node.children.Count;
        }

        public DwarfStat(String msg, bool error = false) {
            this.tree = null;
            if (error) {
                this.err = msg;
            } else {
                this.info = msg;
            }

        }

        public void includeData() {
            if(null == this.tree) {
               return;
            }
            this.data = tree.data;
        }

        public void includeChildLst() {
            if(null == this.tree) {
               return;
            }
            this.childlst = string.Join(",", tree.children.Keys.ToArray());
        }
        
        public override string ToString() {
            return string.Format(
                    "Name: {0}\n" +
                    "ctime: {1}\n" + 
                    "mtime: {2}\n" + 
                    "numChildren: {3}\n" +
                    "childlst: {4}\n" +
                    "data: {5}\n" + 
                    "Err: {6}",
                    this.name,
                    this.ctime,
                    this.mtime,
                    this.numChildren,
                    this.childlst,
                    this.data,
                    this.err);
        }

        public void printStat() {
            Console.WriteLine(string.Format("Name: {0}", this.name));
            Console.WriteLine(string.Format("ctime: {0}", this.ctime));
            Console.WriteLine(string.Format("mtime: {0}", this.mtime));
            Console.WriteLine(string.Format("numChildren: {0}", this.numChildren));
            Console.WriteLine(string.Format("childlst: {0}", this.childlst));
            Console.WriteLine(string.Format("data: {0}", this.data));
            Console.WriteLine(string.Format("Err: {0}", this.err));
        }
    }
}

