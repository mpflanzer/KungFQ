using System;
using System.IO;
using System.Collections.Generic;

namespace KungFq
{
    public class FastqReader
    {
        public FastqReader(TextReader r, int l, bool encodeIds, bool encodeQualities, string histogram)
        {
            counter = 0;
            this.r = r;
            length = l;
            this.encodeIds = encodeIds;
            this.encodeQualities = encodeQualities;
            initQualityCounter(histogram);
        }

        public FastqReader(string s, int l, bool encodeIds, bool encodeQualities, string histogram)
        {
            counter = 0;
            r = new StreamReader(s);
            length = l;
            this.encodeIds = encodeIds;
            this.encodeQualities = encodeQualities;
            initQualityCounter(histogram);
        }

        public FastqReader(TextReader r, string histogram) {
            this.r = r;
            initQualityCounter(histogram);
        }
        
        
        public FastqReader(string s, string histogram) {
            r = new StreamReader(s);
            initQualityCounter(histogram);
        }
        
        private void initQualityCounter(string histogram) {
            if (histogram != "") {
                this.qualityCounter = new QualityCounter();
                histogramDrawer = new HistogramDrawer(histogram);
            } else {
                this.qualityCounter = new DummyQualityCounter();
            }
        }
        
        bool encodeIds;
        bool encodeQualities;
        int counter;
        TextReader r;
        string cId;
        string cSeq;
        string cQual;
        protected LinkedList<string> seqQueue = new LinkedList<string>();
        LinkedList<string> qualQueue = new LinkedList<string>();
        LinkedList<string> idQueue = new LinkedList<string>();
        protected long offset = 0;
        long qOffset = 0;
        long idOffset = 0;
        protected int length;
        IQualityCounter qualityCounter;
        HistogramDrawer histogramDrawer = null;
        
        public void Run() {
            while (CurrentQuality != null) {
                qualityCounter.Add(cQual);   
            }
        }
        
        /* The structure of this class is perfectly comparable with EncodedFastqReader. */
        //TODO comment
        
        int IDChargeUntil(int i)
        {
            int wantedQueue = (int) (i - idOffset);
            bool exit = false;
            while (wantedQueue >= idQueue.Count && !exit) {
                if (CurrentID == null)
                    exit = true;
                else
                    idQueue.AddLast(CurrentID);
                if (CurrentSeq != null)
                    seqQueue.AddLast(CurrentSeq);
                if (CurrentQuality != null && encodeQualities) {
                    qualQueue.AddLast(cQual);
                }
                qualityCounter.Add(cQual);
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                idQueue.RemoveFirst();
                idOffset += 1;
            }
            wantedQueue = (int) (i - idOffset);
            if (wantedQueue < idQueue.Count)
                return wantedQueue;
            else
                return -1;
        }

        public string GetID(int i) {
            int queue = IDChargeUntil(i);
            if (queue != -1) {
                LinkedListNode<string> n = idQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                return n.Value;
            } else
                return " ";
        }

        // XXX is it needed?
        public bool HasIDLeft(int w, int left) {
            return IDChargeUntil(w+left-1) != -1;
        }
        
        
        protected int ChargeUntil(long i)
        {
            int wantedQueue = ((int) (i - offset)) / length;
            bool exit = false;
            while (wantedQueue >= seqQueue.Count && !exit) {
                if (encodeIds && CurrentID != null)
                    idQueue.AddLast(CurrentID);
                if (CurrentSeq == null)
                    exit = true;
                else {
                    seqQueue.AddLast(CurrentSeq);
                } if (CurrentQuality != null && encodeQualities) { //inverted order to go on on fastq
                    qualQueue.AddLast(cQual);
                }
                qualityCounter.Add(cQual);
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

        public virtual char GetSeq(long i) {
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
                if (encodeIds && CurrentID != null)
                    idQueue.AddLast(CurrentID);
                if (CurrentSeq != null)
                    seqQueue.AddLast(CurrentSeq);
                if (CurrentQuality == null)
                    break;
                qualQueue.AddLast(cQual);
                qualityCounter.Add(cQual);
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
            if (histogramDrawer != null)
                histogramDrawer.Draw((QualityCounter) qualityCounter); 
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

