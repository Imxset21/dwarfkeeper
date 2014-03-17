using System;
using System.IO;
using System.Collections.Generic;

using DwarfKeeper;
using DwarfData;

namespace DwarfTest {
    public class DwarfTest {
        static void Main(string[] args) {
            DwarfClient dclient = new DwarfClient("dwarfkeeper");

            List<string> strReps;
            List<DwarfStat> statReps;

            //Add a node mynode
            strReps = dclient.create("/mynode", "GROOMP");
            foreach (string s in strReps) {
                Console.WriteLine(s);
            }
            statReps = dclient.getNodeAll("/mynode");
            foreach (DwarfStat ds in statReps) {
                ds.printStat();
            }
            
            
            //Try to create an invalid node
            strReps = dclient.create("/othernode/that", "GROOMP2");
            foreach (string s in strReps) {
                Console.WriteLine(s);
            }
            statReps = dclient.getNodeAll("/mynode");
            foreach (DwarfStat ds in statReps) {
                ds.printStat();
            }

            //Try to change the Data at /mynode
            strReps = dclient.create("/mynode", "NEW_GROOMP");
            foreach (string s in strReps) {
                Console.WriteLine(s);
            }
            statReps = dclient.getNodeAll("/mynode");
            foreach (DwarfStat ds in statReps) {
                ds.printStat();
            }
        }
    }
}
