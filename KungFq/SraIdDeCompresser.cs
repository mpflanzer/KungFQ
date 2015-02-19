using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace KungFq
{
    public class SraIdDeCompresser : IIdDeCompresser
    {
        
        public SraIdDeCompresser(EncodedFastqReader encReader, int length)
        {
            this.encReader = encReader;
            int l = encReader.Reader.ReadInt32();
            string firstIdPart = ae.GetString(encReader.Reader.ReadBytes(l)); //@SRX000571_SRR002322.
            l = encReader.Reader.ReadInt32();
            string secondIdPart = ae.GetString(encReader.Reader.ReadBytes(l)); //080317_CM-KID-LIV-2-REPEAT_0003: SPAZIO
            
            //@SRX000571_SRR002322.18437692 080317_CM-KID-LIV-2-REPEAT_0003:7:330:466:87 length=36
            //or
            //@SRR029238.3 SOLEXAWS1_20FDNAAXX:1:1:737:1043
            if (encReader.Reader.ReadBoolean())
                idBuilder = firstIdPart + "{0} " + secondIdPart + "{1}:{2}:{3}:{4} length=" + length; 
            else
                idBuilder = firstIdPart + "{0} " + secondIdPart + "{1}:{2}:{3}:{4}";
        }
        
        
        public SraIdDeCompresser(FastqReader reader, BinaryWriter writer, Match match, bool length)
        {
            this.reader = reader;
            this.writer = writer;
            string firstPart = match.Groups[1].Value; // id name
            string secondPart = match.Groups[2].Value; // sample name
            writer.Write(firstPart.Length);
            writer.Write(ae.GetBytes(firstPart));
            writer.Write(secondPart.Length);
            writer.Write(ae.GetBytes(secondPart));
            writer.Write(length);
            if (!length)
                wantedSplit = 7;
        }
        
        string idBuilder;
        const int BUFFER = 1048575; 
        const int ID_BUFFER = BUFFER; 
        const int ENCODED_ID_LENGTH = 12; //16 bit x 4 + 32 bit per l'uint
        EncodedFastqReader encReader;
        BinaryWriter writer;
        FastqReader reader;
        byte[] idContinuation = new byte[ENCODED_ID_LENGTH];
        int continuationLength = 0;
        BinaryWriter encodedId = new BinaryWriter(new MemoryStream(ENCODED_ID_LENGTH));
        BinaryWriter idBuffer = new BinaryWriter(new MemoryStream(BUFFER));
        ASCIIEncoding ae = new ASCIIEncoding();
        char[] separators = new char[] {':', ' ', '.'};
        ushort[] decodedIdNumbers = new ushort[4];
        int writtenContinuation = 0;
        int wantedSplit = 8;
        
        /* Encodes IDs starting at the given index (id) until "buffer is full"
         * or the fastq file ends and writes the result in the given BinaryWriter.
         * Updates id according to its advancements.
         */
        public void EncodeId(ref int id)
        {
            idBuffer.Seek(0, SeekOrigin.Begin);
            encodedId.Seek(0, SeekOrigin.Begin);
            //@SRX000571_SRR002322.18437692 080317_CM-KID-LIV-2-REPEAT_0003:7:330:466:87 length=36
    
            // should check if "mode" is right (ie. reader && writer != null)
            // but we avoid doing so for efficiency
            
            //the first byte starts with 11 if we are encoding an ID
            byte first = (byte) 64;
            int b = 0;

            if (continuationLength != 0) {
                encodedId.Write(idContinuation, 0, continuationLength);
                b += continuationLength;
                writtenContinuation = continuationLength;
                continuationLength = 0;
            }
            //we assume that a continuation will never be longer
            //than BUFFER
            
            while (reader.HasIDLeft(id, 1) && b < ID_BUFFER) {
                //encodedId.Seek(0, SeekOrigin.Begin);
                string[] currentId = reader.GetID(id).Split(separators);
                if (currentId.Length != wantedSplit) {
                    throw new FormatException("Invalid format for ID " + id);
                }
                //1 3 4 5 6
                
                //XXX TODO check if you can use only idBuffer and not also encodedID
                try {
                    encodedId.Write(Convert.ToUInt32(currentId[1])); 
                    //the first number that has to be encoded
                    for (int i = 0; i < 4; i++) {
                        encodedId.Write(Convert.ToUInt16(currentId[i+3])); 
                        // we skip the first three items and the last one
                    }
                    b += ENCODED_ID_LENGTH;
                } catch (FormatException fe) {
                    throw new FormatException("Invalid format for ID " + id, fe);    
                }
                byte[] buffer = ((MemoryStream) encodedId.BaseStream).GetBuffer();
                if (b > ID_BUFFER) {
                    //continuation
                    continuationLength = b - ID_BUFFER;
                    int firstExceedingByte = ENCODED_ID_LENGTH - continuationLength;
                    for (int i = 0; i < continuationLength; i++) {
                        idContinuation[i] = buffer[firstExceedingByte+i];
                    }
                    idBuffer.Write(buffer, 0, firstExceedingByte); 
                    //we have to write firstExceedingByte bytes as the count argument
                    b = ID_BUFFER;            
                } else {
                    //XXX dopo continuation non scrive primi byte per l'uint?248.1
                    idBuffer.Write(buffer, 0, ENCODED_ID_LENGTH + writtenContinuation);
                }
                id++;
                encodedId.Seek(0, SeekOrigin.Begin);
                writtenContinuation = 0;
            }
            
            if (b == ID_BUFFER) {
                writer.Write(first);
                writer.Write(((MemoryStream) idBuffer.BaseStream).GetBuffer(), 0, b);
            } else if (b < ID_BUFFER) {
                //mark smaller buffer
                first += (byte) 32; //we have to tell the decoder that we have a block with a length
                                    //different than BUFFER
                writer.Write(first);
                writer.Write(b);
                writer.Write(((MemoryStream) idBuffer.BaseStream).GetBuffer(), 0, b);
            }
        }
        
        public string GetNextID(ref long idByte)
        {
            // should check if "mode" is right (ie. encReader != null)
            // but we avoid doing so for efficiency
            
            if (encReader.HasIDLeft(idByte, ENCODED_ID_LENGTH)) {
                byte b1, b2;
                byte[] bytes = new byte[4];
                uint idNumber;
                for (int i = 0; i < 4; i++) {
                    bytes[i] = encReader.GetIDByte(idByte++);
                }
                idNumber = ToUInt32(bytes);
                for (int i = 0; i < 4; i++) {
                    b1 = encReader.GetIDByte(idByte++);
                    b2 = encReader.GetIDByte(idByte++);
                    decodedIdNumbers[i] = ToUInt16(b1, b2);
                }
                
                return String.Format(idBuilder, idNumber, decodedIdNumbers[0], decodedIdNumbers[1],
                              decodedIdNumbers[2], decodedIdNumbers[3]);
            } else {
                return "";    
            }
        }
            
        ushort ToUInt16(byte b1, byte b2) 
        {
            ushort res = (ushort) (((ushort) b2) << 8);
            res |= (ushort) b1;
            return res;
        }
        
        
        uint ToUInt32(byte[] b) 
        {
            int leftShift = 24;
            uint res = (uint) (((uint) b[3]) << leftShift);
            for (int i = 2; i >= 0; i--) {
                leftShift -= 8;
                res |= (uint) (((uint) b[i]) << leftShift);
            }
            return res;
            
            /*BinaryWriter bw = new BinaryWriter(new MemoryStream(4));
            bw.Write(b);
            bw.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < 4; i++) {
                Console.Error.WriteLine(((MemoryStream) bw.BaseStream).GetBuffer()[i]);    
            }
            BinaryReader br = new BinaryReader(bw.BaseStream);
            uint res = br.ReadUInt32();
            bw.Close();
            br.Close();
            return res;*/
        }
                                   
    }
}