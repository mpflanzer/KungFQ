//  
//  EncodedQualityReader.cs
//  
//  Author:
//       data <${AuthorEmail}>
// 
//  Copyright (c) 2010 data
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
using ICSharpCode.SharpZipLib.GZip;
using SevenZip;

namespace KungFq
{
    public class EncodedBitQualityReader
    {

        public EncodedBitQualityReader(Stream s, string compression)
        {
             if (compression == "none") {
                bits = new ReadBitShepherd(s);
            } else if (compression == "gzip") {
                GZipInputStream zipReader = new GZipInputStream(s);
                bits = new ReadBitShepherd(zipReader);
                s = zipReader;
            } else if (compression == "lzma") {
                LzmaDecodeStream zipReader = new LzmaDecodeStream(s);
                bits = new ReadBitShepherd(zipReader);
            } else {
                throw new InvalidOperationException("Wrong compression method given");
            }
        }
		
        public ReadBitShepherd bits;

        public byte GetQualByte(long pos)
        {
            int res = 0;
            bits.Read(out res, pos, 8);
            return (byte) res;
        }

		public bool HasQLeft(long pos, int count)
        {
            int unused = 0;
            return bits.Read(out unused, pos, count); 
        }

        public void Close()
        {
           bits.Close();
        }
    }
}
