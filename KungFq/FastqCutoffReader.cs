using System;
using System.IO;
using System.Collections.Generic;

namespace KungFq
{
    public class FastqCutoffReader : FastqReader
    {
        public FastqCutoffReader(TextReader r, int l, bool encodeIds, bool encodeQualities, int cutoff, string histogram) 
                                     : base(r, l, encodeIds, encodeQualities, histogram) {
            this.cutoff = cutoff;
        }
        
        public FastqCutoffReader(string file, int l, bool encodeIds, bool encodeQualities, int cutoff, string histogram) 
                                 : base(file, l, encodeIds, encodeQualities, histogram) {
            this.cutoff = cutoff;
        }
        
        int cutoff = -1;
        
        
        public override char GetSeq(long i) {
            int queue = ChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - offset)) % length;
                LinkedListNode<string> n = seqQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                char res = n.Value[wantedIndex];
                if (GetQ(i) < cutoff) {
                    res = 'N';
                }
                return res;
            } else {
                return ' ';
            }
        }
        
    }
}

