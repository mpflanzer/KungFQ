//  
//  BitShepard.cs
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
using SevenZip;

namespace KungFq
{
    public class ReadBitShepherd
    {
        public ReadBitShepherd(Stream reader)
        {
            //this.reader = new BinaryReader(new BufferedStream(reader));
			this.reader = new BinaryReader(reader);
			if (reader.Position != 0) {
				reader.Position = 0;
			}
        }

        BinaryReader reader;
		const int UNIT_WINDOW = 8; 
		byte lastByte = 0;
		long byteWhere = 0;
		
        public bool Read(out int bits, long pos, int count)
        {
			//we can't seek on compressed streams - instructions
			//removed for efficiency's sake.
			//long byteWhere = reader.BaseStream.Position; 
			//we can't seek on compressed streams - instructions
			//removed for efficiency's sake.
			long wantedByte = pos / UNIT_WINDOW;
            
			//long wantedByte = pos / UNIT_WINDOW + 1;
            //int wantedBit = UNIT_WINDOW - (int) ((wantedByte * UNIT_WINDOW) - pos);
			//remember - 1
			bool useLastByte = false;
			
			int wantedBit = (int) (pos % UNIT_WINDOW);
            //Console.Error.WriteLine("InnerRead {0} {1}", byteWhere, wantedByte);
			if (byteWhere != wantedByte) {
				if (byteWhere - wantedByte == 1) {
					useLastByte = true;		
				} else {
					throw new InvalidOperationException("Wrong usage of Read, too back-seeking needed " + byteWhere  +  " " + wantedByte);	
				}
                //reader.BaseStream.Seek(wantedByte, SeekOrigin.Begin);
            }

            int filledBits = 0;
            try {
				byte wanted = lastByte;
				if (!useLastByte) {
					wanted = reader.ReadByte();
					byteWhere++;
					lastByte = wanted;
					//Console.Error.WriteLine("Read position2 {0}", byteWhere);
				}
				//Console.Error.WriteLine("READINNER {0} {1} {2}", pos, count, wanted);
                wanted <<= wantedBit;
                filledBits = UNIT_WINDOW - wantedBit;
                if (filledBits < count) {
                    byte wanted2 = reader.ReadByte();
					byteWhere++;
					lastByte = wanted2;
                    wanted2 >>= filledBits;
                    wanted |= wanted2;
                    filledBits = count;
                } else if (filledBits > count) {
                    wanted >>= filledBits-count;
                }
                if (count < UNIT_WINDOW)
                    wanted >>= UNIT_WINDOW - filledBits;
                bits = (int) wanted;

            } catch (EndOfStreamException eose) {
                bits = 0;
				//Console.Error.WriteLine("Not Read {0}", bits);
                return false;
            }
			//Console.Error.WriteLine("Read {0}", bits);
            return true;
        }

        public void Close()
        {
            reader.Close();
        }
    }
}

