//  
//  FastqDeCompresser.cs
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
using System.Text;
using System.Text.RegularExpressions;

namespace KungFq
{
    public class FastqDeCompresser : IFastqDeCompresser
    {

        public FastqDeCompresser(int length, bool encodeIds, bool encodeQualities)
        {
            this.length = length;
            this.encodeIds = encodeIds;
            this.encodeQualities = encodeQualities;
        }

        int length;
        int qualityRunLengthContinuation = 0;
        const int BUFFER = 1048575;
        const int MAX = 255;
        ASCIIEncoding ae = new ASCIIEncoding();
        char[] progress = {'|', '/', '-', '\\'};
        int spinCounter = -1;
        IIdDeCompresser iddc;
        bool encodeIds;
        bool encodeQualities;
        
        
        /* Main compression method that keeps the two streams aligned and calls
         * the encoding methods for sequences and qualities.
         */
        public void Compress(FastqReader reader, BinaryWriter writer)
        {
            long seqI = 0;
            long qualI = 0;
            int idCounter = 0;
            byte[] buffer = new byte[BUFFER];
            ChooseIddc(reader, writer);
            
            while (true) {
                Spin("Encoding...");
                if (encodeIds && idCounter <= seqI / length && reader.HasIDLeft(idCounter, 1)) {
                    iddc.EncodeId(ref idCounter);
                    continue;
                }
                if ((seqI <= qualI || !encodeQualities) && reader.HasSeqLeft(seqI, 1)) {
                    EncodeSeq(buffer, ref seqI, writer, reader);
                    continue;
                }
                if (encodeQualities && reader.HasQLeft(qualI, 1)) {
                    EncodeQual(buffer, ref qualI, writer, reader);
                    continue;
                }
                break;
            }
            Console.Error.WriteLine();
        }
                
        /* Method used to print on Standard Error working messages with
         * a spinning ascii art. */
        void Spin(string message)
        {
            spinCounter = (spinCounter + 1) % 4;
            Console.Error.Write("\r{0}{1}", message, progress[spinCounter]);
        }    
        
        void ChooseIddc(FastqReader reader, BinaryWriter writer) {
            if (!encodeIds) {
                iddc = new PlaceholderIdGenerator();
                return;
            }
            //@SRX000571_SRR002321.54856271 080226_CMLIVERKIDNEY_0007:8:330:23:135 length=36
            Regex sra = new Regex(@"^(@[^.]+\.)\d+\s([\S]+)(?:\d+:){3}\d+.*$", RegexOptions.Singleline);
            Regex length = new Regex(@"^.+length=\d+$", RegexOptions.Singleline);
            //@HWUSI-EAS627_1:3:1:0:370/1 (or /2)
            //@BILLIEHOLIDAY_3_FC30G08AAXX:1:1:0:1966
            Regex encode = new Regex(@"^(@[\S]+)(?:\d+:){3}\d+(\/[12])*$", RegexOptions.Singleline);
            
            String id = reader.GetID(0);
            Match sraMatch = sra.Match(id);
            Match encodeMatch = encode.Match(id);
            if (sraMatch.Success) { //type 0
                Match lengthMatch = length.Match(id);
                writer.Write(0);
                iddc = new SraIdDeCompresser(reader, writer, sraMatch, lengthMatch.Success);
            } else if (encodeMatch.Success) { //type 2
                writer.Write(2);
                iddc = new EncodeIdDeCompresser(reader, writer, encodeMatch);
            } else { //type 1
                writer.Write(1);
                iddc = new PlainIdDeCompresser(reader, writer);    
            }
            
        }
        
        void ChooseIddc(EncodedFastqReader reader) {
            if (!encodeIds) {
                iddc = new PlaceholderIdGenerator();
                return;
            }
            int iddcType = reader.Reader.ReadInt32();
            if (iddcType == 0) {
                iddc = new SraIdDeCompresser(reader, length);
            } else if (iddcType == 1) {
                iddc = new PlainIdDeCompresser(reader);
            } else if (iddcType == 2) {
                iddc = new EncodeIdDeCompresser(reader);
            } else {
                throw new Exception("Missing header info in compressed file! " + iddcType);
            }
        }   
        
        /* Encodes sequencing starting at the given index (i) until buffer
         * is full or the sequence ends and writes the result in the given BinaryWriter.
         * Updates i according to its progression.
         */
        void EncodeSeq(byte[] buffer, ref long i, BinaryWriter writer, FastqReader reader)
        {
            //the first byte starts with 1 if we are encoding a seq
            byte first = (byte) 128;
            int b = 0;
            while (reader.HasSeqLeft(i, 4) && b < BUFFER) {
                //Check for run-length run.
                if (reader.GetSeq(i) == reader.GetSeq(i+3) && reader.GetSeq(i) == reader.GetSeq(i+2) && reader.GetSeq(i) == reader.GetSeq(i+1)) {
                    long j = i + 4;
                    long l = i + 19;
                    while (j < l && reader.HasSeqLeft(j, 1) && reader.GetSeq(j) == reader.GetSeq(i))
                        j++;
                    buffer[b++] = (byte)(128 + ((j - i - 4) << 3) + Array.IndexOf(Bases, reader.GetSeq(i)));
                    i = j;
                }
                else {
                    buffer[b++] =  Encode(reader.GetSeq(i++), reader.GetSeq(i++), reader.GetSeq(i++));
                }
            }

            string last = "";
            byte lastSeqByte = 0;
            bool lastByte = false;
            if (!reader.HasSeqLeft(i, 4)) {
                while (reader.HasSeqLeft(i, 1)) //could still have 1, 2 or 3 bases
                    last += reader.GetSeq(i++);
            }
            if (last != "") {
                lastByte = true;
                last = last.PadRight(3, 'N');
                lastSeqByte = Encode(last[0], last[1], last[2]);
            }

            if (b == BUFFER && !lastByte) {
                writer.Write(first);
                writer.Write(buffer);
            } else {
                first += (byte) 32; //we have to tell the decoder that we have a block with a length
                                    //different than BUFFER
                writer.Write(first);
                writer.Write(b + (lastByte ? 1 : 0));
                writer.Write(buffer, 0, b);
                if (lastByte)
                    writer.Write(lastSeqByte);
            }
        }

        void EncodeQual(byte[] buffer, ref long i, BinaryWriter writer, FastqReader reader)
        {
            //the first byte starts with 0 if we are encoding a quality
            byte first = (byte) 0;
            int b = 0;
            if (qualityRunLengthContinuation != 0) {
                buffer[b++] = (byte) qualityRunLengthContinuation;
                qualityRunLengthContinuation = 0;
            }
            while (reader.HasQLeft(i, 1) && b < BUFFER) {
                long j = i+1;
                int rl = 1;
                while (reader.HasQLeft(j, 1) && reader.GetQ(j-1) == reader.GetQ(j) && rl < MAX) {
                    j++;
                    rl++;
                }
                if (rl > 1) { //run length
                    ae.GetBytes(reader.GetQ(j-1).ToString(), 0, 1, buffer, b);
                    buffer[b] = (byte) (buffer[b] + 128);
                    b++;
                    if (b >= BUFFER) {
                        qualityRunLengthContinuation = rl;
                    } else {
                        buffer[b++] = (byte) rl;
                    }
                    i = j;
                } else { //single char
                    ae.GetBytes(reader.GetQ(i).ToString(), 0, 1, buffer, b);
                    b++;
                    i++;
                }
            }

            if (b == BUFFER) {
                writer.Write(first);
                writer.Write(buffer);
            } else {
                first += (byte) 32; //we have to tell the decoder that we have a block with a length
                                    //different than BUFFER
                writer.Write(first);
                writer.Write(b);
                writer.Write(buffer, 0, b);
            }
        }


        /* Main decompression method that decodes the compressed file and
         * directly write the obtained fastq in the given StreamWriter.
         */
        public void Decompress(EncodedFastqReader reader, StreamWriter writer)
        {
            long IdByte = 0;
            long seqI = 0;
            int s = 0;
            long qualI = 0;
            int q = 0;
            int nSeq = 0;
            int howmany = 0;
            char which = ' ';
            byte encoded;

            //continuations variables
            int  continueSequenceRunLength = 0;
            char[] continueSequenceChar = new char[]  {' ', ' '};
            //0 - the rl char or the second char of the triplet across reads
            //1 - the rl char or the third char of the triplet across reads
            int  continueQualityRunLength = 0;
            char continueQualityChar = ' ';

            ChooseIddc(reader);
            writer.WriteLine(iddc.GetNextID(ref IdByte));
            
            while (reader.HasSeqLeft(seqI, 1) || continueSequenceRunLength != 0 || continueSequenceChar[0] != ' ' ||
                   (encodeQualities && reader.HasQLeft(qualI, 1))  || continueQualityRunLength != 0) {
                q = 0;
                s = 0;
                while (s < length) {
                    if (continueSequenceRunLength != 0) {
                        while (continueSequenceRunLength > 0 && s < length) {
                            s++;
                            continueSequenceRunLength--;
                            writer.Write(continueSequenceChar[0]);
                        }
                        if (continueSequenceRunLength == 0) {
                            continueSequenceChar[0] = ' ';
                        }
                    } else if (continueSequenceChar[0] != ' ') {
                        //we assume that 1 or 2 char(s) will always fit in the new read
                        //ie reads will always be longer than 2
                        writer.Write(continueSequenceChar[0]);
                        s++;
                        if (continueSequenceChar[1] != ' ') {
                            writer.Write(continueSequenceChar[1]);
                            s++;
                        }
                        continueSequenceChar[0] = ' ';
                        continueSequenceChar[1] = ' ';
                    } else if (reader.HasSeqLeft(seqI, 1)) {
                        encoded = reader.GetSeqByte(seqI);
                        seqI++;
                        if ((encoded & 128) == 128) { //run length
                            encoded = (byte) (127 & encoded);
                            howmany =  (encoded >> 3) + 4;
                            which = Bases[(int) (encoded & 7)];
                            int i = 0;
                            while (i < howmany && s < length) {
                                i++;
                                s++;
                                writer.Write(which);
                            }
                            if (i < howmany) {
                                continueSequenceChar[0] = which;
                                continueSequenceRunLength = howmany - i;
                            }
                        } else { //three bases
                            string triplet = decoding[(int) encoded];
                            int k = 0;
                            while (s < length && k < 3) {
                                writer.Write(triplet[k++]);
                                s++;
                            }
                            int i = 0;
                            while (k < 3) {
                                continueSequenceChar[i] = triplet[k];
                                k++;
                                i++;
                            }
                        }
                    }
                }
                nSeq++;
                if (encodeQualities)
                    writer.WriteLine("\n+");
                while (encodeQualities && q < length) {
                    if (continueQualityRunLength != 0) {
                        while (continueQualityRunLength > 0 && q < length) {
                            q++;
                            continueQualityRunLength--;
                            writer.Write(continueQualityChar);
                        }
                        if (continueQualityRunLength == 0) {
                            continueQualityChar = ' ';
                        }
                    } else if (reader.HasQLeft(qualI, 1)) {
                        encoded = reader.GetQualByte(qualI);
                        qualI++;
                        if ((encoded & 128) != 128) { //single quality data
                            which = Convert.ToChar(encoded);
                            writer.Write(which);
                            q++;
                        } else { //run length
                            encoded = (byte) (127 & encoded);
                            which = Convert.ToChar(encoded);
                            howmany = (int) reader.GetQualByte(qualI);
                            qualI++;
                            int i = 0;
                            while (i < howmany && q < length) {
                                i++;
                                q++;
                                writer.Write(which);
                            }
                            if (i < howmany) {
                                continueQualityRunLength = howmany - i;
                                continueQualityChar = which;
                            }
                        }
                    }
                }
                if (reader.HasSeqLeft(seqI, 1) || continueSequenceRunLength != 0) {
                    //if we have got a sequence run length it cannot be padding and if we have
                    //a continuation derived for a triplet we will have sequences left if it's
                    //not a padding
                    writer.WriteLine("\n" + iddc.GetNextID(ref IdByte));
                    if (seqI % 10000 == 0)
                        Spin("Decoding...");
                } else if (!reader.HasSeqLeft(seqI, 1) && continueSequenceChar[0] != ' ')
                    break;
            }
            writer.WriteLine();
            Console.Error.WriteLine();
        }

        /* Array that stores correspondences between triplets and their binary encoding. */
        static FastqDeCompresser() {
            decoding[1] = "AAA";
            decoding[2] = "AAC";
            decoding[3] = "AAG";
            decoding[4] = "AAT";
            decoding[5] = "AAN";
            decoding[6] = "ACA";
            decoding[7] = "ACC";
            decoding[8] = "ACG";
            decoding[9] = "ACT";
            decoding[10] = "ACN";
            decoding[11] = "AGA";
            decoding[12] = "AGC";
            decoding[13] = "AGG";
            decoding[14] = "AGT";
            decoding[15] = "AGN";
            decoding[16] = "ATA";
            decoding[17] = "ATC";
            decoding[18] = "ATG";
            decoding[19] = "ATT";
            decoding[20] = "ATN";
            decoding[21] = "ANA";
            decoding[22] = "ANC";
            decoding[23] = "ANG";
            decoding[24] = "ANT";
            decoding[25] = "ANN";
            decoding[26] = "CAA";
            decoding[27] = "CAC";
            decoding[28] = "CAG";
            decoding[29] = "CAT";
            decoding[30] = "CAN";
            decoding[31] = "CCA";
            decoding[32] = "CCC";
            decoding[33] = "CCG";
            decoding[34] = "CCT";
            decoding[35] = "CCN";
            decoding[36] = "CGA";
            decoding[37] = "CGC";
            decoding[38] = "CGG";
            decoding[39] = "CGT";
            decoding[40] = "CGN";
            decoding[41] = "CTA";
            decoding[42] = "CTC";
            decoding[43] = "CTG";
            decoding[44] = "CTT";
            decoding[45] = "CTN";
            decoding[46] = "CNA";
            decoding[47] = "CNC";
            decoding[48] = "CNG";
            decoding[49] = "CNT";
            decoding[50] = "CNN";
            decoding[51] = "GAA";
            decoding[52] = "GAC";
            decoding[53] = "GAG";
            decoding[54] = "GAT";
            decoding[55] = "GAN";
            decoding[56] = "GCA";
            decoding[57] = "GCC";
            decoding[58] = "GCG";
            decoding[59] = "GCT";
            decoding[60] = "GCN";
            decoding[61] = "GGA";
            decoding[62] = "GGC";
            decoding[63] = "GGG";
            decoding[64] = "GGT";
            decoding[65] = "GGN";
            decoding[66] = "GTA";
            decoding[67] = "GTC";
            decoding[68] = "GTG";
            decoding[69] = "GTT";
            decoding[70] = "GTN";
            decoding[71] = "GNA";
            decoding[72] = "GNC";
            decoding[73] = "GNG";
            decoding[74] = "GNT";
            decoding[75] = "GNN";
            decoding[76] = "TAA";
            decoding[77] = "TAC";
            decoding[78] = "TAG";
            decoding[79] = "TAT";
            decoding[80] = "TAN";
            decoding[81] = "TCA";
            decoding[82] = "TCC";
            decoding[83] = "TCG";
            decoding[84] = "TCT";
            decoding[85] = "TCN";
            decoding[86] = "TGA";
            decoding[87] = "TGC";
            decoding[88] = "TGG";
            decoding[89] = "TGT";
            decoding[90] = "TGN";
            decoding[91] = "TTA";
            decoding[92] = "TTC";
            decoding[93] = "TTG";
            decoding[94] = "TTT";
            decoding[95] = "TTN";
            decoding[96] = "TNA";
            decoding[97] = "TNC";
            decoding[98] = "TNG";
            decoding[99] = "TNT";
            decoding[100] = "TNN";
            decoding[101] = "NAA";
            decoding[102] = "NAC";
            decoding[103] = "NAG";
            decoding[104] = "NAT";
            decoding[105] = "NAN";
            decoding[106] = "NCA";
            decoding[107] = "NCC";
            decoding[108] = "NCG";
            decoding[109] = "NCT";
            decoding[110] = "NCN";
            decoding[111] = "NGA";
            decoding[112] = "NGC";
            decoding[113] = "NGG";
            decoding[114] = "NGT";
            decoding[115] = "NGN";
            decoding[116] = "NTA";
            decoding[117] = "NTC";
            decoding[118] = "NTG";
            decoding[119] = "NTT";
            decoding[120] = "NTN";
            decoding[121] = "NNA";
            decoding[122] = "NNC";
            decoding[123] = "NNG";
            decoding[124] = "NNT";
            decoding[125] = "NNN";
        }

        public static char[] Bases = new char[] {'A', 'C', 'G', 'T', 'N'};

        private static string[] decoding = new string[126];

        /* Method that encodes a triplet in a byte (the first bit is always 0). */
        static byte Encode(char b1, char b2, char b3)
        {

            if (b1 == 65) {
                if (b2 == 65) {
                    if (b3 == 65) return 0x01;
                    if (b3 == 67) return 0x02;
                    if (b3 == 71) return 0x03;
                    if (b3 == 84) return 0x04;
                    if (b3 == 78) return 0x05;
                }
                if (b2 == 67) {
                    if (b3 == 65) return 0x06;
                    if (b3 == 67) return 0x07;
                    if (b3 == 71) return 0x08;
                    if (b3 == 84) return 0x09;
                    if (b3 == 78) return 0x0a;
                }
                if (b2 == 71) {
                    if (b3 == 65) return 0x0b;
                    if (b3 == 67) return 0x0c;
                    if (b3 == 71) return 0x0d;
                    if (b3 == 84) return 0x0e;
                    if (b3 == 78) return 0x0f;
                }
                if (b2 == 84) {
                    if (b3 == 65) return 0x10;
                    if (b3 == 67) return 0x11;
                    if (b3 == 71) return 0x12;
                    if (b3 == 84) return 0x13;
                    if (b3 == 78) return 0x14;
                }
                if (b2 == 78) {
                    if (b3 == 65) return 0x15;
                    if (b3 == 67) return 0x16;
                    if (b3 == 71) return 0x17;
                    if (b3 == 84) return 0x18;
                    if (b3 == 78) return 0x19;
                }
            }
            if (b1 == 67) {
                if (b2 == 65) {
                    if (b3 == 65) return 0x1a;
                    if (b3 == 67) return 0x1b;
                    if (b3 == 71) return 0x1c;
                    if (b3 == 84) return 0x1d;
                    if (b3 == 78) return 0x1e;
                }
                if (b2 == 67) {
                    if (b3 == 65) return 0x1f;
                    if (b3 == 67) return 0x20;
                    if (b3 == 71) return 0x21;
                    if (b3 == 84) return 0x22;
                    if (b3 == 78) return 0x23;
                }
                if (b2 == 71) {
                    if (b3 == 65) return 0x24;
                    if (b3 == 67) return 0x25;
                    if (b3 == 71) return 0x26;
                    if (b3 == 84) return 0x27;
                    if (b3 == 78) return 0x28;
                }
                if (b2 == 84) {
                    if (b3 == 65) return 0x29;
                    if (b3 == 67) return 0x2a;
                    if (b3 == 71) return 0x2b;
                    if (b3 == 84) return 0x2c;
                    if (b3 == 78) return 0x2d;
                }
                if (b2 == 78) {
                    if (b3 == 65) return 0x2e;
                    if (b3 == 67) return 0x2f;
                    if (b3 == 71) return 0x30;
                    if (b3 == 84) return 0x31;
                    if (b3 == 78) return 0x32;
                }
            }
            if (b1 == 71) {
                if (b2 == 65) {
                    if (b3 == 65) return 0x33;
                    if (b3 == 67) return 0x34;
                    if (b3 == 71) return 0x35;
                    if (b3 == 84) return 0x36;
                    if (b3 == 78) return 0x37;
                }
                if (b2 == 67) {
                    if (b3 == 65) return 0x38;
                    if (b3 == 67) return 0x39;
                    if (b3 == 71) return 0x3a;
                    if (b3 == 84) return 0x3b;
                    if (b3 == 78) return 0x3c;
                }
                if (b2 == 71) {
                    if (b3 == 65) return 0x3d;
                    if (b3 == 67) return 0x3e;
                    if (b3 == 71) return 0x3f;
                    if (b3 == 84) return 0x40;
                    if (b3 == 78) return 0x41;
                }
                if (b2 == 84) {
                    if (b3 == 65) return 0x42;
                    if (b3 == 67) return 0x43;
                    if (b3 == 71) return 0x44;
                    if (b3 == 84) return 0x45;
                    if (b3 == 78) return 0x46;
                }
                if (b2 == 78) {
                    if (b3 == 65) return 0x47;
                    if (b3 == 67) return 0x48;
                    if (b3 == 71) return 0x49;
                    if (b3 == 84) return 0x4a;
                    if (b3 == 78) return 0x4b;
                }
            }
            if (b1 == 84) {
                if (b2 == 65) {
                    if (b3 == 65) return 0x4c;
                    if (b3 == 67) return 0x4d;
                    if (b3 == 71) return 0x4e;
                    if (b3 == 84) return 0x4f;
                    if (b3 == 78) return 0x50;
                }
                if (b2 == 67) {
                    if (b3 == 65) return 0x51;
                    if (b3 == 67) return 0x52;
                    if (b3 == 71) return 0x53;
                    if (b3 == 84) return 0x54;
                    if (b3 == 78) return 0x55;
                }
                if (b2 == 71) {
                    if (b3 == 65) return 0x56;
                    if (b3 == 67) return 0x57;
                    if (b3 == 71) return 0x58;
                    if (b3 == 84) return 0x59;
                    if (b3 == 78) return 0x5a;
                }
                if (b2 == 84) {
                    if (b3 == 65) return 0x5b;
                    if (b3 == 67) return 0x5c;
                    if (b3 == 71) return 0x5d;
                    if (b3 == 84) return 0x5e;
                    if (b3 == 78) return 0x5f;
                }
                if (b2 == 78) {
                    if (b3 == 65) return 0x60;
                    if (b3 == 67) return 0x61;
                    if (b3 == 71) return 0x62;
                    if (b3 == 84) return 0x63;
                    if (b3 == 78) return 0x64;
                }
            }
            if (b1 == 78) {
                if (b2 == 65) {
                    if (b3 == 65) return 0x65;
                    if (b3 == 67) return 0x66;
                    if (b3 == 71) return 0x67;
                    if (b3 == 84) return 0x68;
                    if (b3 == 78) return 0x69;
                }
                if (b2 == 67) {
                    if (b3 == 65) return 0x6a;
                    if (b3 == 67) return 0x6b;
                    if (b3 == 71) return 0x6c;
                    if (b3 == 84) return 0x6d;
                    if (b3 == 78) return 0x6e;
                }
                if (b2 == 71) {
                    if (b3 == 65) return 0x6f;
                    if (b3 == 67) return 0x70;
                    if (b3 == 71) return 0x71;
                    if (b3 == 84) return 0x72;
                    if (b3 == 78) return 0x73;
                }
                if (b2 == 84) {
                    if (b3 == 65) return 0x74;
                    if (b3 == 67) return 0x75;
                    if (b3 == 71) return 0x76;
                    if (b3 == 84) return 0x77;
                    if (b3 == 78) return 0x78;
                }
                if (b2 == 78) {
                    if (b3 == 65) return 0x79;
                    if (b3 == 67) return 0x7a;
                    if (b3 == 71) return 0x7b;
                    if (b3 == 84) return 0x7c;
                    if (b3 == 78) return 0x7d;
                }
            }
            throw new ArgumentException(String.Format("arguments out of range: b1={0} b2={1} b3={2}", b1, b2, b3));
        }
    }
}

