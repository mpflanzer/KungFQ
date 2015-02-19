//  
//  EncodedFastqReader.cs
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
using ICSharpCode.SharpZipLib.GZip;
using SevenZip;

namespace KungFq
{
    public class EncodedFastqReader
    {
        public EncodedFastqReader(Stream s, string compression)
        {
            if (compression == "none") {
                //bits = new ReadBitShepherd(s);
				r = new BinaryReader(s);
            } else if (compression == "gzip") {
                GZipInputStream zipReader = new GZipInputStream(s);
                //bits = new ReadBitShepherd(zipReader);
				r = new BinaryReader(zipReader);
            } else if (compression == "lzma") {
                LzmaDecodeStream zipReader = new LzmaDecodeStream(s);
                //bits = new ReadBitShepherd(zipReader);
				r = new BinaryReader(zipReader);
            } else {
                throw new InvalidOperationException("Wrong compression method given");
            }
        }

        BinaryReader r; 
		
        LinkedList<byte[]> seqQueue = new LinkedList<byte[]>();
        LinkedList<byte[]> qualQueue = new LinkedList<byte[]>();
		LinkedList<byte[]> idQueue = new LinkedList<byte[]>();
        const int BUFFER = 1048574; //XXX
		//const int ID_BUFFER = BUFFER / 4; 
		const int ID_BUFFER = BUFFER; 
        
		const int UNIT_WINDOW = 8; 
        long offset = 0;
        long qOffset = 0;
		int idOffset = 0;
        bool endSeq = false;
        bool endQual = false;
		bool endId = false;

		public BinaryReader Reader
        {
            get
            {
				return r;
            }
        }
		
		
        int IDChargeUntil(long i)
        {
			//Console.Error.WriteLine("Loading id {0} {1} {2}", idQueue.Count, seqQueue.Count, qualQueue.Count);
            int wantedQueue = ((int) (i - idOffset)) / BUFFER;
            while (wantedQueue >= idQueue.Count && !endId) {
                byte f = r.ReadByte();
                if ((f & 128) == 128) {
                    LoadSeq(f);
                } else if ((f & 64) == 64) {
					LoadID(f);
				} else {
                    LoadQual(f);
				}
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                idQueue.RemoveFirst();
                idOffset += BUFFER;
            }
            wantedQueue = ((int) (i - idOffset)) / BUFFER;
            if (wantedQueue < idQueue.Count)
                if (wantedQueue == idQueue.Count - 1 && ((i - idOffset) % BUFFER) >= idQueue.Last.Value.Length)
                    return -1;
                else
                    return wantedQueue;
            else
                return -1;
        }
		
		public byte GetIDByte(long i) {
            int queue = IDChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - idOffset)) % BUFFER;
                LinkedListNode<byte[]> n = idQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                /* Performance hint TODO: use structs with the nodes length prestored XXX ??? */
                if (wantedIndex < n.Value.Length)
                    return n.Value[wantedIndex];
                else
                    return 0; //XXX throw Exception?
            } else
                return 0;
        }
		
		/* Check if there are 'left' more bytes in the encoded id stream starting at w
         * and if it's the case in charges them.
         * */
        public bool HasIDLeft(long w, int left) {
            return IDChargeUntil(w+left-1) != -1;
        }

        /* Loads a block from the encoded sequences stream.
         */
        void LoadID(byte f)
        {
			//Console.Error.WriteLine("Load ID {0}", f);
            if (f == 96) {
                endId = true;
                LoadLastId();
            } else {
                byte[] buffer = r.ReadBytes(ID_BUFFER);
				//Console.Error.WriteLine("Loaded id buffer {0} {1}", buffer.Length, f);
                if (buffer.Length == ID_BUFFER) {
                    idQueue.AddLast(buffer);
                } else if (buffer.Length == 0) {
                    endId = true;
                } else {
                    throw new Exception("Unexpected end of id buffer");
                }
            }
        }

        /* Specialized method that loads the last part of the id stream.
         */
        void LoadLastId()
        {
            int l = r.ReadInt32();
            if (l > ID_BUFFER) { //XXX ?
                idQueue.AddLast(r.ReadBytes(ID_BUFFER));
                l -= ID_BUFFER;
            }
			//Console.Error.WriteLine("Loaded last id buffer {0}", l);
            idQueue.AddLast(r.ReadBytes(l));
        }
		

		
        /* Charges sequences data until the given position is available in seqQueue.
         * Return the index in seqQueue where the given position is stored or -1
         * if it is not avaiable (end of the fastq).
         * */
        int ChargeUntil(long i)
        {
			//Console.Error.WriteLine("Loading seq {0} {1} {2}", idQueue.Count, seqQueue.Count, qualQueue.Count);
            int wantedQueue = ((int) (i - offset)) / BUFFER;
            while (wantedQueue >= seqQueue.Count && !endSeq) {
                byte f = r.ReadByte();
				if ((f & 128) == 128) {
                    LoadSeq(f);
                } else if ((f & 64) == 64) {
					LoadID(f);
				} else {
                    LoadQual(f);
				}
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                seqQueue.RemoveFirst();
                offset += BUFFER;
            }
            wantedQueue = ((int) (i - offset)) / BUFFER;
            if (wantedQueue < seqQueue.Count)
                if (wantedQueue == seqQueue.Count - 1 && ((i - offset) % BUFFER) >= seqQueue.Last.Value.Length)
                    return -1;
                else
                    return wantedQueue;
            else
                return -1;
        }

        public bool GetSeqBitsAsInt(ref int res, ref long pos, int count)
        {
			long wantedByte = pos / UNIT_WINDOW;
			MemoryStream bytes = new MemoryStream(2);
			if (HasSeqLeft(wantedByte, 1)) {
				byte b = GetSeqByte(wantedByte++);
				bytes.WriteByte(b);
				//Console.Error.WriteLine("GetBitASInts {0} {1} {2}", pos, count, b);
			} else  {
				return false;
			}
			int wantedBit = (int) (pos % UNIT_WINDOW);
			if (wantedBit + count >= 8) {
				if (HasSeqLeft(wantedByte, 1)) {
					byte b = GetSeqByte(wantedByte);
					bytes.WriteByte(b); 
					//Console.Error.WriteLine("GetBitASInts2 {0} {1} {2}", pos, count, b);
				} else {
					return false;
				}
			}
			
			ReadBitShepherd bits = new ReadBitShepherd(bytes);
			//Console.Error.WriteLine("Shepherd reading {0} {1}", bytes.Length, pos);
			bool hasGotten = bits.Read(out res, wantedBit, count);
			if (hasGotten)
				pos += count;
			//Console.Error.WriteLine("For seq gotten {0} {1}", res, count);
			
			//Console.Error.WriteLine("Giving back {0}", res);
			return hasGotten;
			
        }
		
        /* Return the byte at the given position in the encoded sequences stream - HasSeqLeft
         * _HAS TO_ be called first to be sure to obtain a sensible value. XXX
         * */
        public byte GetSeqByte(long i) {
            int queue = ChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - offset)) % BUFFER;
                LinkedListNode<byte[]> n = seqQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                /* Performance hint TODO: use structs with the nodes length prestored XXX ??? */
                if (wantedIndex < n.Value.Length) {
					//Console.Error.WriteLine("011Returing {0}", n.Value[wantedIndex]);
                    return n.Value[wantedIndex];
				}
                else {
					//Console.Error.WriteLine("0Returing 0");
                    return 0; //XXX throw Exception?
				}
            } else {
				//Console.Error.WriteLine("Returing 0");
                return 0;
			}
        }

        /* Check if there are 'left' more bytes in the encoded sequences stream starting at w
         * and if it's the case in charges them.
         * */
        public bool HasSeqLeft(long w, int left) {
            return ChargeUntil(w+left-1) != -1;
        }

        /* Loads a block from the encoded sequences stream.
         */
        void LoadSeq(byte f)
        {
			//Console.Error.WriteLine("Load seq {0}", f);
            if (f == 160) {
                endSeq = true;
                LoadLastSeq();
            } else {
                byte[] buffer = r.ReadBytes(BUFFER);
                if (buffer.Length == BUFFER) {
					//Console.Error.WriteLine("seq buffer {0} {1} {2}", buffer[0], buffer[1], buffer[2]);
                    seqQueue.AddLast(buffer);
                } else if (buffer.Length == 0) {
                    endSeq = true;
                } else {
                    throw new Exception("Unexpected end of seq buffer");
                }
            }
        }

        /* Specialized method that loads the last part of the sequences stream.
         */
        void LoadLastSeq()
        {
            int l = r.ReadInt32();
            if (l > BUFFER) {
                seqQueue.AddLast(r.ReadBytes(BUFFER));
                l -= BUFFER;
            }
            seqQueue.AddLast(r.ReadBytes(l));
			//Console.Error.WriteLine("{3} seq buffer {0} {1} {2}", seqQueue.Last.Value[0], 
			//                        seqQueue.Last.Value[1], seqQueue.Last.Value[2], l);
        }

        /* Specular methods for qualities stream follows. */

        int QChargeUntil(long i)
        {
			//Console.Error.WriteLine("Loading q {0} {1} {2}", idQueue.Count, seqQueue.Count, qualQueue.Count);
            int wantedQueue = ((int) (i - qOffset)) / BUFFER;
            while (wantedQueue >= qualQueue.Count && !endQual) {
                byte f = r.ReadByte();
                if (f == 128) {
                    LoadSeq(f);
                } else if ((f & 64) == 64) {
					LoadID(f);
				} else {
                    LoadQual(f);
				}
            }
            for (int x = wantedQueue - 2 ; x > 0 ; x--) {
                qualQueue.RemoveFirst();
                qOffset += BUFFER;
            }
            wantedQueue = (int) ((i - qOffset)) / BUFFER;
            if (wantedQueue < qualQueue.Count) {
                if (wantedQueue == qualQueue.Count - 1 && ((i - qOffset) % BUFFER) >= qualQueue.Last.Value.Length)
                    return -1;
                else
                    return wantedQueue;
            } else
                return -1;
        }


        public byte GetQualByte(long i)
        {
            int queue = QChargeUntil(i);
            if (queue != -1) {
                int wantedIndex = ((int) (i - qOffset)) % BUFFER;
                LinkedListNode<byte[]> n = qualQueue.First;
                for (int j = 0; j < queue; j++)
                    n = n.Next;
                if (wantedIndex < n.Value.Length) {
					//Console.Error.WriteLine("Giving back {0} {1} {2} {3}", n.Value[wantedIndex], i, wantedIndex, queue);
                    return n.Value[wantedIndex];
				}
                else
                    return 0;
            } else
                return 0;
        }

        public bool HasQLeft(long w, int left) {
            return QChargeUntil(w+left-1) != -1;
        }

        void LoadQual(byte f)
        {
			//Console.Error.WriteLine("Load qual {0}", f);
            if (f == 32) {
				//Console.Error.WriteLine("Going for end {0} {1}", f, f&32);
                endQual = true;
                LoadLastQual();
            } else {
                byte[] buffer = r.ReadBytes(BUFFER);
				//Console.Error.WriteLine("{0} and not {1} {2}", buffer.Length, BUFFER, f);
                if (buffer.Length == BUFFER) {
                    qualQueue.AddLast(buffer);
					//Console.Error.WriteLine("Quakity {0} {1}", buffer[0], buffer[1]);
                } else if (buffer.Length == 0) {
                    endQual = true;
                } else {
                    throw new Exception("Unexpected end of quality buffer");
                }
            }
        }

        void LoadLastQual()
        {
            int l = r.ReadInt32();
			qualQueue.AddLast(r.ReadBytes(l));
			//Console.Error.WriteLine("Quakityaa {2} {0} {1}", qualQueue.Last.Value[0], qualQueue.Last.Value[1], l);
        }

        public void Close()
        {
            r.Close();
        }
    }
}