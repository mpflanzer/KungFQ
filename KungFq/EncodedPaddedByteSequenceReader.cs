//  
//  EncodedSequenceReader.cs
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
    public class EncodedPaddedByteSequenceReader
    {
        public EncodedPaddedByteSequenceReader(Stream s, string compression)
        {
            if (compression == "none") {
                bits = new ReadBitPaddingShepherd(s);
            } else if (compression == "gzip") {
                GZipInputStream zipReader = new GZipInputStream(s);
                bits = new ReadBitPaddingShepherd(zipReader);
  			} else if (compression == "lzma") {
                LzmaDecodeStream zipReader = new LzmaDecodeStream(s);
                bits = new ReadBitPaddingShepherd(zipReader);
            } else {
                throw new InvalidOperationException("Wrong compression method given");
            }
        }

        public ReadBitPaddingShepherd bits;

        public bool GetBitsAsInt(ref int res, ref long pos, int count)
        {
            bool hasGotten = bits.Read(out res, pos, count);
			if (hasGotten)
				pos += count;
			return hasGotten;
        }

        public bool HasSeqLeft(long pos, int count)
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