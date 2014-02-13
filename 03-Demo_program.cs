using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Isis;
using System.Threading;

namespace ConsoleApplication8
{
    public class tuple
    {
        public int rank;
        public int value;

        public tuple(int r, int v)
        {
            rank = r; value = v;
        }
    }

    class Program
    {
        static List<tuple> database = new List<tuple>();
        public const int UPDATE = 0;
        public const int LOOKUP = 1;
        static Semaphore go = new Semaphore(0, 1), dbFull = new Semaphore(0, 1);
        static void Main(string[] args)
        {
            IsisSystem.Start();
            Group g = new Group("foo");
            int myRank = 0;
            g.ViewHandlers += (ViewHandler)delegate(View v)
            {
                IsisSystem.WriteLine("New View: " + v);
                myRank = v.GetMyRank();
                if (v.members.Length == 3)
                    go.Release(1);
            };
            g.Handlers[UPDATE] += (Action<int, int>)delegate(int rank, int n)
            {
                database.Add(new tuple(n, rank));
                IsisSystem.WriteLine("[" + database.Count() + "]  New tuple: " + rank + "/" + n);
                if (database.Count() == 15)
                    dbFull.Release(1);
            };
            g.Handlers[LOOKUP] += (Action<int>)delegate(int arg)
            {
                IsisSystem.WriteLine("=== Query for arg=" + arg);
                List<int> answer = new List<int>();
                int index = 0;
                foreach (tuple tp in database)
                    if (index++ % 3 == myRank)
                    {
                        IsisSystem.WriteLine("Looking at " + tp.rank + "/" + tp.value);
                        if (tp.rank == arg)
                        {
                            IsisSystem.WriteLine("Including " + tp.rank + "/" + tp.value);
                            answer.Add(tp.value);
                        }
                    }
                g.Reply(answer);
            };
            g.Join();
            go.WaitOne();
            for (int n = 0; n < 5; n++)
                g.OrderedSend(UPDATE, myRank, n);
            IsisSystem.WriteLine("Wait until database is full!");
            dbFull.WaitOne();
            IsisSystem.WriteLine("Database is fully populated!");
            if (myRank == 1)
                for (int n = 0; n < 3; n++)
                {
                    List<List<int>> results = new List<List<int>>();
                    g.OrderedQuery(Group.ALL, LOOKUP, n, new Isis.EOLMarker(), results);
                    IsisSystem.WriteLine("\r\nAnswers for Query rank=" + n);
                    foreach (List<int> list in results)
                        foreach (int value in list)
                            IsisSystem.Write(value + " ");
                }
            IsisSystem.WaitForever();
        }
    }
}
