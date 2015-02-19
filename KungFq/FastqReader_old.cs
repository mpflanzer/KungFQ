//  FastqReader.cs
//  
//  Author:
//       Elena Grassi <grassi.e@gmail.com>
// 
//  Copyright (c) 2010 Elena Grassi
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Collections.Generic;

namespace KungFq
{
    public class FastqReader
    {
        public FastqReader(TextReader r, int l)
        {
            counter = 0;
            this.r = r;
            length = l;
        }

        public FastqReader(string s, int l)
        {
            counter = 0;
            r = new StreamReader(s);
            length = l;
        }

        int counter;
        TextReader r;
        string cId;
        string cSeq;
        string cQual;
        LinkedList<string> seqQueue = new LinkedList<string>();
        LinkedList<string> qualQueue = new LinkedList<string>();
        long offset = 0;
        long qOffset = 0;
        int length;

        /* The structure of this class is perfectly comparable with EncodedFastqReader. */

        int ChargeUntil(long i)
        {
            int wantedQueue = ((int) (i - offset)) / length;
            bool exit = false;
            while (wantedQueue >= seqQueue.Count && !exit) {
                if (CurrentSeq == null)
                    exit = true;
                else
                    seqQueue.AddLast(CurrentSeq);
                if (CurrentQuality != null)
                    qualQueue.AddLast(cQual);
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                seqQueue.RemoveFirst();
                offset += length;
            }
            wantedQueue = ((int) (i - offset)) / length;
            if (wantedQueue < seqQueue.Count)
                return wantedQueue;
            else
                return -1;
        }

        public char GetSeq(long i) {
            int queue = ChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - offset)) % length;
                LinkedListNode<string> n = seqQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                return n.Value[wantedIndex];
            } else
                return ' ';
        }

        public bool HasSeqLeft(long w, int left) {
            return ChargeUntil(w+left-1) != -1;
        }


        int QChargeUntil(long i)
        {
            int wantedQueue = ((int) (i - qOffset)) / length;
            while (wantedQueue >= qualQueue.Count) {
                if (CurrentSeq != null)
                    seqQueue.AddLast(CurrentSeq);
                if (CurrentQuality == null)
                    break;
                qualQueue.AddLast(cQual);
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                qualQueue.RemoveFirst();
                qOffset += length;
            }
            wantedQueue = ((int) (i - qOffset)) / length;
            if (wantedQueue < qualQueue.Count)
                return wantedQueue;
            else
                return -1;
        }

        public char GetQ(long i) {
            int queue = QChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - qOffset)) % length;

                LinkedListNode<string> n = qualQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                return n.Value[wantedIndex];
            } else
                return ' ';
        }

        public bool HasQLeft(long w, int left) {
            return QChargeUntil(w+left-1) != -1;
        }

        public void Close()
        {
            r.Close();
        }

        string CurrentID
        {
            get
            {
                if (counter == 0) {
                    cId = r.ReadLine();
                    counter = 1;
                    return cId;
                } else {
                    return cId;
                }
            }
            set
            {
                cId = value;
            }
        }

        string CurrentSeq
        {
            get
            {
                if (counter == 0) {
                    CurrentID = r.ReadLine();
                    counter = 1;
                }
                if (counter == 1) {
                    cSeq = r.ReadLine();
                    counter = 2;
                    return cSeq;
                } else {
                    return cSeq;
                }
            }
            set
            {
                cSeq = value;
            }
        }

        string CurrentQuality
        {
            get
            {
                if (counter == 0) {
                    CurrentID = r.ReadLine();
                    counter = 1;
                }
                if (counter == 1) {
                    CurrentSeq = r.ReadLine();
                    counter = 2;
                }
                if (counter == 2) { //we ignore the + line
                    r.ReadLine();
                    counter = 3;
                }
                if (counter == 3) {
                    cQual = r.ReadLine();
                    counter = 0;
                }
                return cQual;
            }
            set
            {
                cQual = value;
            }
        }

    }
}

