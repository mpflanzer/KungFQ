// 
//  FastqDeCompresser.cs
//  
//  Author:
//       Elena Grassi <grassi.e@gmail.com>
// 
//  Copyright (c) 2010 Elena Grassi
// 
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//  
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
// 
using System;
using System.IO;
using System.Text;

namespace KungFq
{
    public class CasualPadderFastqDeCompresser 
    {

        public CasualPadderFastqDeCompresser(int l)
        {
            length = l;
        }

        int length;
        int qualityRunLengthContinuation = 0;
        const int BUFFER = 1048575; 
		const int BIT_BUFFER = BUFFER * 8;
		//TODO find right value, used to avoid desync between qualities and sequences
        const int MAX = 255;
        char[] progress = {'|', '/', '-', '\\'};
        int spinCounter = -1;
		static Random rand = new Random(42);
		//ASCIIEncoding ae = new ASCIIEncoding();

        /* Main compression method that keeps the two streams aligned and calls
         * the encoding methods for sequences and qualities.
         */
        public void Compress(FastqReader reader, BinaryWriter sequenceWriter, BinaryWriter qualityWriter)
        {
            long seqI = 0;
            long qualI = 0;
            byte[] buffer = new byte[BUFFER];
            WriteBitShepherd bits = new WriteBitShepherd(sequenceWriter);
            while (true) {
                Spin("Encoding...");
                if (seqI <= qualI && reader.HasSeqLeft(seqI, 1)) {
                    EncodeSeq(bits, ref seqI, reader);
                    continue;
                }
                if (reader.HasQLeft(qualI, 1)) {
                    EncodeQual(buffer, ref qualI, qualityWriter, reader);
                    continue;
                }
                break;
            }
            bits.Close();
        }

        void Spin(string message)
        {
            spinCounter = (spinCounter + 1) % 4;
            Console.Error.Write("\r{0}{1}", message, progress[spinCounter]);
        }

        /* Encodes sequencing starting at the given index (i) until buffer
         * is full or the sequence ends and writes the result in the given WriteBitSheperd.
         * Updates i according to its progression.
         */
        void EncodeSeq(WriteBitShepherd bits, ref long i, FastqReader reader)
        {
            int writtenBits = 0;
            while (reader.HasSeqLeft(i, 4) && writtenBits < BIT_BUFFER) {
                //Check for run-length run.
                if (reader.GetSeq(i) == reader.GetSeq(i+3) && reader.GetSeq(i) == reader.GetSeq(i+2) 
				 && reader.GetSeq(i) == reader.GetSeq(i+1)) {
                    long j = i + 4;
                    long l = i + 8199;
                    while (j < l && reader.HasSeqLeft(j, 1) && reader.GetSeq(j) == reader.GetSeq(j-1))
                        j++;
                    int length = (int) (j - i);
                    if (length > 35) { 
                        bits.Write(127, 7); //flag for long run length
						bits.Write(GetRandomBit(), 1); 
						bits.Write(Array.IndexOf(Bases, reader.GetSeq(j-1)), 3);
                        bits.Write(length-4, 13);
                        writtenBits += 24;
                    } else {
                        bits.Write(0, 7); //flag for short run length
						bits.Write(GetRandomBit(), 1); 
                        bits.Write(Array.IndexOf(Bases, reader.GetSeq(j-1)), 3);
                        bits.Write(length-4, 5);
                        writtenBits += 16;
                    }
                    i = j;
                }
                else {
                    bits.Write(Encode(reader.GetSeq(i), reader.GetSeq(i+1), reader.GetSeq(i+2)), 7);
					bits.Write(GetRandomBit(), 1); 
                    i += 3;
                    writtenBits += 8;
                }
            }

            bool end = false;
            string last = "";
            if (!reader.HasSeqLeft(i, 4)) {
                while (reader.HasSeqLeft(i, 1)) //could still have 1, 2 or 3 bases
                    last += reader.GetSeq(i++);
                end = true;
            }
            if (last != "") {
                last = last.PadRight(3, 'N');
                bits.Write(Encode(last[0], last[1], last[2]), 7);
				bits.Write(GetRandomBit(), 1); 
                writtenBits += 8;
            }
            if (end) {
                bits.Write(126, 7); // mark end of sequences blocks
				bits.Write(GetRandomBit(), 1); 
                writtenBits += 8;
            }
        }
		
		static int GetRandomBit() 
		{
			return rand.Next(0,2);
			//return 1;
		}
		

        void EncodeQual(byte[] buffer, ref long i, BinaryWriter writer, FastqReader reader)
        {
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
                    //ae.GetBytes(reader.GetQ(j-1).ToString(), 0, 1, buffer, b);
					buffer[b] = Convert.ToByte(reader.GetQ(j-1));
                    buffer[b] = (byte) (buffer[b] + 128);
                    b++;
                    if (b >= BUFFER) {
                        qualityRunLengthContinuation = rl;
                    } else {
                        buffer[b++] = (byte) rl;
                    }
                    i = j;
                } else { //single char
                    //ae.GetBytes(reader.GetQ(i).ToString(), 0, 1, buffer, b);
					buffer[b] = Convert.ToByte(reader.GetQ(i));
                    b++;
                    i++;
                }
            }

            if (b == BUFFER) {
                writer.Write(buffer);
            } else {
                writer.Write(buffer, 0, b);
            }
        }
		
        private void CheckIDNeed(int s, int nSeq, StreamWriter writer)
        {
            if (s == 0) {
                if (nSeq == 0)
                    writer.WriteLine("@ID." + nSeq);
                else
                    writer.WriteLine("\n@ID." + nSeq);
            }
        }

        /* Main decompression method that decodes the compressed file and
         * directly write the obtained fastq in the given StreamWriter.
         */
        public void Decompress(EncodedBitSequenceReader sequenceReader, EncodedBitQualityReader qualityReader, StreamWriter writer)
        {
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
            bool hasAlready7SeqBits = false;
            int sevenSeqBits = 0;
			bool end = false;
			int spam = 0;
            while (hasAlready7SeqBits = sequenceReader.GetBitsAsInt(ref sevenSeqBits, ref seqI, 7)
			       || continueSequenceRunLength != 0 || continueSequenceChar[0] != ' ' 
			       || qualityReader.HasQLeft(qualI, 8)  || continueQualityRunLength != 0) {
                q = 0;
                s = 0;
				//Console.Error.WriteLine("In the main loop {0} {1} {2}", q, s, hasAlready7SeqBits);
				int encodedInt = 0;
                while (s < length) {
					//Console.Error.WriteLine("IIIIn the seq loop {0} {1} {2} {3} {4}", s, seqI, encodedInt, sevenSeqBits, hasAlready7SeqBits);
					//Console.Error.WriteLine("{0} ={1}=", continueSequenceRunLength, continueSequenceChar[0]);
                    if (continueSequenceRunLength != 0) {
						//Console.Error.WriteLine("in the first else");
                        CheckIDNeed(s, nSeq, writer);
                        while (continueSequenceRunLength > 0 && s < length) {
                            continueSequenceRunLength--;
                            writer.Write(continueSequenceChar[0]);
                            s++;
                        }
                        if (continueSequenceRunLength == 0) {
                            continueSequenceChar[0] = ' ';
                        }
                    } else if (continueSequenceChar[0] != ' ') {
						//Console.Error.WriteLine("in the second else");
                        //we assume that 1 or 2 char(s) will always fit in the new read
                        //ie reads will always be longer than 2
                        CheckIDNeed(s, nSeq, writer);
                        writer.Write(continueSequenceChar[0]);
                        s++;
                        if (continueSequenceChar[1] != ' ') {
                            writer.Write(continueSequenceChar[1]);
                            s++;
                        }
                        continueSequenceChar[0] = ' ';
                        continueSequenceChar[1] = ' ';
                    } else if (hasAlready7SeqBits || sequenceReader.GetBitsAsInt(ref encodedInt, ref seqI, 7)) {
						//Console.Error.WriteLine("in the main else {0}", encodedInt);
						//here we are sure that we have just read 7 bits, either way.
						sequenceReader.GetBitsAsInt(ref spam, ref seqI, 1);
                        if (hasAlready7SeqBits) {
                            encodedInt = sevenSeqBits;
							hasAlready7SeqBits = false;
                        } 
                        if (encodedInt != 0 && encodedInt != 126 && encodedInt != 127) { //triplet
                            string triplet = decoding[encodedInt];
                            int k = 0;
                            CheckIDNeed(s, nSeq, writer);
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
                        } else if (encodedInt != 126) {
							int rlLengthBits = 5; //short rl
							if (encodedInt == 127) //long rl
								rlLengthBits = 13;
							sequenceReader.GetBitsAsInt(ref encodedInt, ref seqI, 3);
                            which = Bases[encodedInt];
                            sequenceReader.GetBitsAsInt(ref howmany, ref seqI, rlLengthBits);
                            howmany += 4;
                            int i = 0;
                            CheckIDNeed(s, nSeq, writer);
                            while (i < howmany && s < length) {
                                i++;
                                s++;
                                writer.Write(which);
                            }
                            if (i < howmany) {
                                continueSequenceChar[0] = which;
                                continueSequenceRunLength = howmany - i;
                            }
						} else { //126 marks end
							//Console.Error.WriteLine("in the else");
							end = true;
							break;
						}
                    } 
					// here we had an else break TODO
                }
                nSeq++;
				Spin("Decoding...");
				//Console.Error.WriteLine("between seq and q");
                while (q < length) {
					//Console.Error.WriteLine("in she q loop");
                    if (q == 0 && (continueQualityRunLength != 0 || qualityReader.HasQLeft(qualI, 8)))
                        writer.WriteLine("\n+");
                    if (continueQualityRunLength != 0) {
                        while (continueQualityRunLength > 0 && q < length) {
                            q++;
                            continueQualityRunLength--;
                            writer.Write(continueQualityChar);
                        }
                        if (continueQualityRunLength == 0) {
                            continueQualityChar = ' ';
                        }
                    } else if (qualityReader.HasQLeft(qualI, 8)) {
                        encoded = qualityReader.GetQualByte(qualI);
                        qualI += 8;
                        if ((encoded & 128) != 128) { //single quality data
                            which = Convert.ToChar(encoded);
                            writer.Write(which);
                            q++;
                        } else { //run length
                            encoded = (byte) (127 & encoded);
                            which = Convert.ToChar(encoded);
                            howmany = (int) qualityReader.GetQualByte(qualI);
                            qualI += 8;
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
                    } else {
						break;	
					}
                }
				if (end)
					break;
                /*if (!hasAlready7SeqBits && sequenceReader.GetBitsAsInt(ref sevenSeqBits, ref seqI, 7)) {
                    hasAlready7SeqBits = true;
                    if (sevenSeqBits == 126)
                        break;
                }*/
            }
            writer.WriteLine();
            Console.Error.WriteLine();
        }

        /* Array that stores correspondences between triplets and their binary encoding. */
        static CasualPadderFastqDeCompresser() {
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

