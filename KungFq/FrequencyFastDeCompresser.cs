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
    public class FrequencyFastqDeCompresser : IFastqDeCompresser
    {

        public FrequencyFastqDeCompresser(int l)
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
                if (reader.GetSeq(i) == reader.GetSeq(i+3) && reader.GetSeq(i) == reader.GetSeq(i+2) && reader.GetSeq(i) == reader.GetSeq(i+1)) {
                    long j = i + 4;
                    long l = i + 259;
                    while (j < l && reader.HasSeqLeft(j, 1) && reader.GetSeq(j) == reader.GetSeq(j-1))
                        j++;
                    int length = (int) (j - i);
					//Console.Error.WriteLine("RL!");
                    if (length > 19) { 
                        //memoryBits.Write(127, 7); //flag for long run length
                        bits.Write(63, 6); //flag for long run length
                        bits.Write(Array.IndexOf(Bases, reader.GetSeq(j-1)), 3);
                        bits.Write(length-4, 8);
                        writtenBits += 17;
                    } else {
                        //memoryBits.Write(0, 7); //flag for short run length
                        bits.Write(1, 6); //flag for short run length
                        bits.Write(Array.IndexOf(Bases, reader.GetSeq(j-1)), 3);
                        bits.Write(length-4, 4);
                        writtenBits += 13;
                    }
                    i = j;
                }
                else {
					bool frequent = false;
                    byte encoded = Encode(new String(new char[] {reader.GetSeq(i),reader.GetSeq(i+1),reader.GetSeq(i+2)}), out frequent);
                    if (frequent) {
                        //Console.Error.WriteLine("Econdign frequent {0}",encoded);
                        bits.Write(encoded, 6);   
                        writtenBits += 6;
                    } else {
                        //Console.Error.WriteLine("Econdign rare {0}",encoded);
                        bits.Write(0, 6);
                        bits.Write(encoded, 7);   
                        writtenBits += 13;
                    }
					i += 3;
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
				bool frequent = false;
                byte encoded = Encode(last, out frequent);
                if (frequent) {
                    //Console.Error.WriteLine("Econdign frequent {0}",encoded);
                    bits.Write(encoded, 6);   
                    writtenBits += 6;
                } else {
                    //Console.Error.WriteLine("Econdign rare {0}",encoded);
                    bits.Write(0, 6);
                    bits.Write(encoded, 7);   
                    writtenBits += 13;
                }
            }
            if (end) {
                bits.Write(126, 7); // mark end of sequences blocks
                writtenBits += 7;
            }
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
        public void Decompress(EncodedSequenceReader sequenceReader, EncodedQualityReader qualityReader, StreamWriter writer)
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
            while (hasAlready7SeqBits = sequenceReader.GetBitsAsInt(ref sevenSeqBits, ref seqI, 6)
			       || continueSequenceRunLength != 0 || continueSequenceChar[0] != ' ' 
			       || qualityReader.HasQLeft(qualI, 8)  || continueQualityRunLength != 0) {
                q = 0;
                s = 0;
				//Console.Error.WriteLine("In the main loop {0} {1} {2}", q, s, hasAlready7SeqBits);
				int encodedInt = 0;
                while (s < length) {
					if (!hasAlready7SeqBits && sequenceReader.GetBitsAsInt(ref sevenSeqBits, ref seqI, 6)) {
                        hasAlready7SeqBits = true;
                    }
					//Console.Error.WriteLine("IIIIn the seq loop {0} {1} {2} {3} {4}", s, seqI, encodedInt, sevenSeqBits, hasAlready7SeqBits);
					//.Error.WriteLine("{0} ={1}=", continueSequenceRunLength, continueSequenceChar[0]);
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
                    } else if (hasAlready7SeqBits || sequenceReader.GetBitsAsInt(ref encodedInt, ref seqI, 6)) {
                        /*if (hasAlready7SeqBits && sevenSeqBits == 62) {
                            Console.Error.WriteLine("Going out {0} {1}", sevenSeqBits, seqI);
                            end = true;
                            break;
                        }*/ //check if it's needed
                        //Console.Error.WriteLine("in the main else {0} {1} {2}", encodedInt, sevenSeqBits, hasAlready7SeqBits);
                        if (hasAlready7SeqBits) {
                            encodedInt = sevenSeqBits;
                            hasAlready7SeqBits = false;
                        } 
                        if (encodedInt != 1 && encodedInt != 63 && encodedInt != 62) { //triplet 
                            string triplet = "";
                            if (encodedInt == 0) { //rare triplet or end 
                                //Console.Error.WriteLine("Seen rare triplet {0}", encodedInt);
                                sequenceReader.GetBitsAsInt(ref encodedInt, ref seqI, 7);
                                triplet = decodingRare[encodedInt];
                            
                            } else { //frequent triplet
                                //Console.Error.WriteLine("Seen frequente triplet {0}", encodedInt);
                                triplet = decodingFreq[encodedInt];
                            }
                            
                            int k = 0;
                            CheckIDNeed(s, nSeq, writer);
                            //Console.Error.WriteLine("Seen triplet {0}", triplet);
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
                        } else if (encodedInt != 62) { //1 or 63 are long rl
                            int rlLengthBits = 4; //short rl
                            if (encodedInt == 63) //long rl
                                rlLengthBits = 8;
                            sequenceReader.GetBitsAsInt(ref encodedInt, ref seqI, 3);
                            which = Bases[encodedInt];
                            sequenceReader.GetBitsAsInt(ref howmany, ref seqI, rlLengthBits);
                            howmany += 4;
                            int i = 0;
                            CheckIDNeed(s, nSeq, writer);
                            //Console.Error.WriteLine("Seen rl {0} {1}", howmany, which);
                            while (i < howmany && s < length) {
                                i++;
                                s++;
                                writer.Write(which);
                            }
                            if (i < howmany) {
                                continueSequenceChar[0] = which;
                                continueSequenceRunLength = howmany - i;
                            }
                        } else {
                            //Console.Error.WriteLine("Exiting!");
                            end = true;
                            break;
                        }
                    } else {
                        Console.Error.WriteLine("LUP!");    
                        break;
                    }
				
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
        static FrequencyFastqDeCompresser() {
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
                            
            decodingFreq[2] = "TCG";
            decodingFreq[3] = "CTA";
            decodingFreq[4] = "TAG";
            decodingFreq[5] = "TAT";
            decodingFreq[6] = "GTA";
            decodingFreq[7] = "TAA";
            decodingFreq[8] = "TTA";
            decodingFreq[9] = "CGC";
            decodingFreq[10] = "AGT";
            decodingFreq[11] = "GCG";
            decodingFreq[12] = "GTT";
            decodingFreq[13] = "NNN";
            decodingFreq[14] = "GTC";
            decodingFreq[15] = "ATT";
            decodingFreq[16] = "ACT";
            decodingFreq[17] = "AAT";
            decodingFreq[18] = "GAC";
            decodingFreq[19] = "CCG";
            decodingFreq[20] = "ATC";
            decodingFreq[21] = "GAT";
            decodingFreq[22] = "CGG";
            decodingFreq[23] = "AAC";
            decodingFreq[24] = "CAT";
            decodingFreq[25] = "GGT";
            decodingFreq[26] = "ACC";
            decodingFreq[27] = "TGT";
            decodingFreq[28] = "TTG";
            decodingFreq[29] = "ATG";
            decodingFreq[30] = "TCA";
            decodingFreq[31] = "CAA";
            decodingFreq[32] = "GTG";
            decodingFreq[33] = "ACA";
            decodingFreq[34] = "CAC";
            decodingFreq[35] = "GCA";
            decodingFreq[36] = "TGA";
            decodingFreq[37] = "TGC";
            decodingFreq[38] = "GCT";
            decodingFreq[39] = "TTC";
            decodingFreq[40] = "TCC";
            decodingFreq[41] = "CTC";
            decodingFreq[42] = "CTT";
            decodingFreq[43] = "AGC";
            decodingFreq[44] = "TCT";
            decodingFreq[45] = "CCT";
            decodingFreq[46] = "AAG";
            decodingFreq[47] = "GGC";
            decodingFreq[48] = "GAG";
            decodingFreq[49] = "GCC";
            decodingFreq[50] = "CCA";
            decodingFreq[51] = "TTT";
            decodingFreq[52] = "GAA";
            decodingFreq[53] = "TGG";
            decodingFreq[54] = "AGA";
            decodingFreq[55] = "GGA";
            decodingFreq[56] = "AGG";
            decodingFreq[57] = "CTG";
            decodingFreq[58] = "CAG";
            decodingFreq[59] = "AAA";
            decodingFreq[60] = "GGG";
            decodingFreq[61] = "CCC";
            decodingRare[0] = "TNG";
            decodingRare[1] = "NTA";
            decodingRare[2] = "NCG";
            decodingRare[3] = "NGT";
            decodingRare[4] = "TAN";
            decodingRare[5] = "ANG";
            decodingRare[6] = "CGN";
            decodingRare[7] = "GNT";
            decodingRare[8] = "ANT";
            decodingRare[9] = "TNA";
            decodingRare[10] = "GNA";
            decodingRare[11] = "GTN";
            decodingRare[12] = "NGN";
            decodingRare[13] = "NAT";
            decodingRare[14] = "CNA";
            decodingRare[15] = "GNC";
            decodingRare[16] = "NGA";
            decodingRare[17] = "GNG";
            decodingRare[18] = "CNG";
            decodingRare[19] = "NAN";
            decodingRare[20] = "ATN";
            decodingRare[21] = "NAG";
            decodingRare[22] = "ANC";
            decodingRare[23] = "NGC";
            decodingRare[24] = "GAN";
            decodingRare[25] = "NTG";
            decodingRare[26] = "NTN";
            decodingRare[27] = "NAC";
            decodingRare[28] = "CNT";
            decodingRare[29] = "NGG";
            decodingRare[30] = "TNC";
            decodingRare[31] = "AGN";
            decodingRare[32] = "TNT";
            decodingRare[33] = "GCN";
            decodingRare[34] = "TGN";
            decodingRare[35] = "ACN";
            decodingRare[36] = "NTC";
            decodingRare[37] = "ANA";
            decodingRare[38] = "GGN";
            decodingRare[39] = "NAA";
            decodingRare[40] = "TCN";
            decodingRare[41] = "NTT";
            decodingRare[42] = "TTN";
            decodingRare[43] = "NCA";
            decodingRare[44] = "NCT";
            decodingRare[45] = "NNG";
            decodingRare[46] = "NCN";
            decodingRare[47] = "CAN";
            decodingRare[48] = "CTN";
            decodingRare[49] = "AAN";
            decodingRare[50] = "NNA";
            decodingRare[51] = "NNT";
            decodingRare[52] = "CNC";
            decodingRare[53] = "GNN";
            decodingRare[54] = "NCC";
            decodingRare[55] = "ANN";
            decodingRare[56] = "CCN";
            decodingRare[57] = "TNN";
            decodingRare[58] = "NNC";
            decodingRare[59] = "CNN";
            decodingRare[60] = "ACG";
            decodingRare[61] = "CGT";
            decodingRare[62] = "CGA";
            decodingRare[63] = "ATA";
            decodingRare[64] = "TAC";
            
        }

        public static char[] Bases = new char[] {'A', 'C', 'G', 'T', 'N'};

        private static string[] decoding = new string[126];
        private static string[] decodingFreq = new string[62]; //63
        private static string[] decodingRare = new string[65];
        /* Method that encodes a triplet in a byte (the first bit is always 0). */
        static byte Encode(string triplet, out bool frequent)
        {
            switch (triplet)
            {
                case "TCG":
                    frequent = true;
                    return 2;
                case "CTA":
                    frequent = true;
                    return 3;
                case "TAG":
                    frequent = true;
                    return 4;
                case "TAT":
                    frequent = true;
                    return 5;
                case "GTA":
                    frequent = true;
                    return 6;
                case "TAA":
                    frequent = true;
                    return 7;
                case "TTA":
                    frequent = true;
                    return 8;
                case "CGC":
                    frequent = true;
                    return 9;
                case "AGT":
                    frequent = true;
                    return 10;
                case "GCG":
                    frequent = true;
                    return 11;
                case "GTT":
                    frequent = true;
                    return 12;
                case "NNN":
                    frequent = true;
                    return 13;
                case "GTC":
                    frequent = true;
                    return 14;
                case "ATT":
                    frequent = true;
                    return 15;
                case "ACT":
                    frequent = true;
                    return 16;
                case "AAT":
                    frequent = true;
                    return 17;
                case "GAC":
                    frequent = true;
                    return 18;
                case "CCG":
                    frequent = true;
                    return 19;
                case "ATC":
                    frequent = true;
                    return 20;
                case "GAT":
                    frequent = true;
                    return 21;
                case "CGG":
                    frequent = true;
                    return 22;
                case "AAC":
                    frequent = true;
                    return 23;
                case "CAT":
                    frequent = true;
                    return 24;
                case "GGT":
                    frequent = true;
                    return 25;
                case "ACC":
                    frequent = true;
                    return 26;
                case "TGT":
                    frequent = true;
                    return 27;
                case "TTG":
                    frequent = true;
                    return 28;
                case "ATG":
                    frequent = true;
                    return 29;
                case "TCA":
                    frequent = true;
                    return 30;
                case "CAA":
                    frequent = true;
                    return 31;
                case "GTG":
                    frequent = true;
                    return 32;
                case "ACA":
                    frequent = true;
                    return 33;
                case "CAC":
                    frequent = true;
                    return 34;
                case "GCA":
                    frequent = true;
                    return 35;
                case "TGA":
                    frequent = true;
                    return 36;
                case "TGC":
                    frequent = true;
                    return 37;
                case "GCT":
                    frequent = true;
                    return 38;
                case "TTC":
                    frequent = true;
                    return 39;
                case "TCC":
                    frequent = true;
                    return 40;
                case "CTC":
                    frequent = true;
                    return 41;
                case "CTT":
                    frequent = true;
                    return 42;
                case "AGC":
                    frequent = true;
                    return 43;
                case "TCT":
                    frequent = true;
                    return 44;
                case "CCT":
                    frequent = true;
                    return 45;
                case "AAG":
                    frequent = true;
                    return 46;
                case "GGC":
                    frequent = true;
                    return 47;
                case "GAG":
                    frequent = true;
                    return 48;
                case "GCC":
                    frequent = true;
                    return 49;
                case "CCA":
                    frequent = true;
                    return 50;
                case "TTT":
                    frequent = true;
                    return 51;
                case "GAA":
                    frequent = true;
                    return 52;
                case "TGG":
                    frequent = true;
                    return 53;
                case "AGA":
                    frequent = true;
                    return 54;
                case "GGA":
                    frequent = true;
                    return 55;
                case "AGG":
                    frequent = true;
                    return 56;
                case "CTG":
                    frequent = true;
                    return 57;
                case "CAG":
                    frequent = true;
                    return 58;
                case "AAA":
                    frequent = true;
                    return 59;
                case "GGG":
                    frequent = true;
                    return 60;
                case "CCC":
                    frequent = true;
                    return 61;
                case "TNG":
                    frequent = false;
                    return 0;
                case "NTA":
                    frequent = false;
                    return 1;
                case "NCG":
                    frequent = false;
                    return 2;
                case "NGT":
                    frequent = false;
                    return 3;
                case "TAN":
                    frequent = false;
                    return 4;
                case "ANG":
                    frequent = false;
                    return 5;
                case "CGN":
                    frequent = false;
                    return 6;
                case "GNT":
                    frequent = false;
                    return 7;
                case "ANT":
                    frequent = false;
                    return 8;
                case "TNA":
                    frequent = false;
                    return 9;
                case "GNA":
                    frequent = false;
                    return 10;
                case "GTN":
                    frequent = false;
                    return 11;
                case "NGN":
                    frequent = false;
                    return 12;
                case "NAT":
                    frequent = false;
                    return 13;
                case "CNA":
                    frequent = false;
                    return 14;
                case "GNC":
                    frequent = false;
                    return 15;
                case "NGA":
                    frequent = false;
                    return 16;
                case "GNG":
                    frequent = false;
                    return 17;
                case "CNG":
                    frequent = false;
                    return 18;
                case "NAN":
                    frequent = false;
                    return 19;
                case "ATN":
                    frequent = false;
                    return 20;
                case "NAG":
                    frequent = false;
                    return 21;
                case "ANC":
                    frequent = false;
                    return 22;
                case "NGC":
                    frequent = false;
                    return 23;
                case "GAN":
                    frequent = false;
                    return 24;
                case "NTG":
                    frequent = false;
                    return 25;
                case "NTN":
                    frequent = false;
                    return 26;
                case "NAC":
                    frequent = false;
                    return 27;
                case "CNT":
                    frequent = false;
                    return 28;
                case "NGG":
                    frequent = false;
                    return 29;
                case "TNC":
                    frequent = false;
                    return 30;
                case "AGN":
                    frequent = false;
                    return 31;
                case "TNT":
                    frequent = false;
                    return 32;
                case "GCN":
                    frequent = false;
                    return 33;
                case "TGN":
                    frequent = false;
                    return 34;
                case "ACN":
                    frequent = false;
                    return 35;
                case "NTC":
                    frequent = false;
                    return 36;
                case "ANA":
                    frequent = false;
                    return 37;
                case "GGN":
                    frequent = false;
                    return 38;
                case "NAA":
                    frequent = false;
                    return 39;
                case "TCN":
                    frequent = false;
                    return 40;
                case "NTT":
                    frequent = false;
                    return 41;
                case "TTN":
                    frequent = false;
                    return 42;
                case "NCA":
                    frequent = false;
                    return 43;
                case "NCT":
                    frequent = false;
                    return 44;
                case "NNG":
                    frequent = false;
                    return 45;
                case "NCN":
                    frequent = false;
                    return 46;
                case "CAN":
                    frequent = false;
                    return 47;
                case "CTN":
                    frequent = false;
                    return 48;
                case "AAN":
                    frequent = false;
                    return 49;
                case "NNA":
                    frequent = false;
                    return 50;
                case "NNT":
                    frequent = false;
                    return 51;
                case "CNC":
                    frequent = false;
                    return 52;
                case "GNN":
                    frequent = false;
                    return 53;
                case "NCC":
                    frequent = false;
                    return 54;
                case "ANN":
                    frequent = false;
                    return 55;
                case "CCN":
                    frequent = false;
                    return 56;
                case "TNN":
                    frequent = false;
                    return 57;
                case "NNC":
                    frequent = false;
                    return 58;
                case "CNN":
                    frequent = false;
                    return 59;
                case "ACG":
                    frequent = false;
                    return 60;
                case "CGT":
                    frequent = false;
                    return 61;
                case "CGA":
                    frequent = false;
                    return 62;
                case "ATA":
                    frequent = false;
                    return 63;
                case "TAC":
                    frequent = false;
                    return 64;
            default:
                throw new ArgumentException(triplet);
            }       
        }
        
        
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
	