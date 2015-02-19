//  
//  WriteBitShepherd.cs
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

namespace KungFq
{
    public class WriteBitShepherd
    {
        public WriteBitShepherd(BinaryWriter writer)
        {
            this.writer = writer;
            counter = 0;
        }

        BinaryWriter writer;
        int counter;
        byte buildingByte = 0;
		const int UNIT_WINDOW = 8; 
		//now a single byte is our 'unity' considered for shifting things around.
		
		public BinaryWriter Writer
		{
			get {
				return writer;	
			}
		}
		
		// pre-requisite: count should be <= 8
        public void Write(int toWrite, int count)
        {
			//Console.Error.WriteLine("Writing {0} {1}", toWrite, count);
            /*if (counter == 8) {
                writer.Write(buildingByte);
                counter = 0;
                buildingByte = 0;
            }*/
            byte toAdd = (byte) toWrite;
            toAdd <<= UNIT_WINDOW - count;
            buildingByte |= (byte) (toAdd >> counter);
            counter += count;
            if (counter >= UNIT_WINDOW) {
                writer.Write(buildingByte);
				//Console.Error.WriteLine("Written {0}", buildingByte);

                buildingByte = 0;
                buildingByte |= (byte) (toAdd << UNIT_WINDOW - counter + count);
				// UNIT_WINDOW - counter are our exceedings bits (negative)
				// removing their number from counts yields how many bits we
				// have to flush away now to keep only exceeding bits
                counter -= UNIT_WINDOW;
            }
        }
		
		// pre-requisite: count should be <= 8
        public void WriteHighestBits(int toWrite, int count)
        {
			//Console.Error.WriteLine("WritingHB {0} {1}", toWrite, count);
            /*if (counter == 8) {
                writer.Write(buildingByte);
                counter = 0;
                buildingByte = 0;
            }*/
            byte toAdd = (byte) toWrite;
            toAdd >>= UNIT_WINDOW - count;
            buildingByte |= (byte) (toAdd << UNIT_WINDOW - counter - count);
            counter += count;
            if (counter >= UNIT_WINDOW) {
                writer.Write(buildingByte);
				//Console.Error.WriteLine("Written {0}", buildingByte);

                buildingByte = 0;
                buildingByte |= (byte) (toAdd << UNIT_WINDOW*2 - counter);
				// UNIT_WINDOW - counter are our exceedings bits (negative)
				// removing their number from counts yields how many bits we
				// have to flush away now to keep only exceeding bits
                counter -= UNIT_WINDOW;
            }
        }
		
		// if counter = 6, count = 4 the first 2 bits are written then
		// counter becomes 10, we enter the if and we shift toAdd to the left
		// UNIT_WINDOW - 10 + 4 = 2
		// counter = 1, count = 8 - becomes 9 and UNIT_WINDOW - 9 + 8 = 7

		public void Flush()
		{
			if (counter != 0) {
				writer.Write(buildingByte);	
			}

		
		}
		
        public void Close()
        {
			//we may have to write the final (possibly impartial) byte
			Flush();
            writer.Close();
        }

    }
}

