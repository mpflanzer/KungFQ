using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace KungFq
{
    public class EncodeIdDeCompresser : IIdDeCompresser
    {
        
        public EncodeIdDeCompresser(EncodedFastqReader encReader)
        {
            this.encReader = encReader;
            int l = encReader.Reader.ReadInt32();
            string firstIdPart = ae.GetString(encReader.Reader.ReadBytes(l)); //@HWUSI-EAS627_1
            l = encReader.Reader.ReadInt32();
            string paired = "";
            if (l != 0)
                paired = ae.GetString(encReader.Reader.ReadBytes(l)); // /1 (or /2)
            
            //@HWUSI-EAS627_1:3:1:0:370/1 (or /2)
            //@BILLIEHOLIDAY_3_FC30G08AAXX:1:1:0:1966
            //Regex encode = new Regex(@"^(@[^:]+)(?:\d+:){3}\d+(\/[12])*$", RegexOptions.Singleline);
            
            //ignoring:
            //@080514_HWI-EAS229_0029_20768AAXX_5_1_120:242
            //@TUPAC:1:1:5:710#0/1
            idBuilder = firstIdPart + "{0}:{1}:{2}:{3}" + paired;
        }
        
        
        public EncodeIdDeCompresser(FastqReader reader, BinaryWriter writer, Match match)
        {
            this.reader = reader;
            this.writer = writer;
            string firstPart = match.Groups[1].Value; // id name
            string paired = "";
            writer.Write(firstPart.Length);
            writer.Write(ae.GetBytes(firstPart));
            if (match.Groups.Count == 3) {
                paired = match.Groups[2].Value; // paired reads info
                writer.Write(paired.Length);
                writer.Write(ae.GetBytes(paired));
            } else {
                writer.Write(0);   
            }
        }
        
        string idBuilder;
        const int BUFFER = 1048575;
        const int BIT_BUFFER = BUFFER * 8;
        const int ID_BUFFER = BUFFER; 
        const int ENCODED_ID_LENGTH = 8; //16 bit x 4 
        EncodedFastqReader encReader;
        BinaryWriter writer;
        FastqReader reader;
        byte[] idContinuation = new byte[ENCODED_ID_LENGTH];
        int continuationLength = 0;
        BinaryWriter encodedId = new BinaryWriter(new MemoryStream(ENCODED_ID_LENGTH));
        BinaryWriter idBuffer = new BinaryWriter(new MemoryStream(BUFFER));
        ASCIIEncoding ae = new ASCIIEncoding();
        char[] separators = new char[] {':', '/'};
        ushort[] decodedIdNumbers = new ushort[4];
        int writtenContinuation = 0;
        
        /* Encodes IDs starting at the given index (id) until "buffer is full"
         * or the fastq file ends and writes the result in the given BinaryWriter.
         * Updates id according to its advancements.
         */
        public void EncodeId(ref int id)
        {
            idBuffer.Seek(0, SeekOrigin.Begin);
            encodedId.Seek(0, SeekOrigin.Begin);
  
    
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
                if (currentId.Length < 5) {
                    throw new Exception("invalid ID format");
                }
                //1 2 3 4
                
                //XXX TODO check if you can use only idBuffer and not also encodedID
                
                //the first number that has to be encoded
                for (int i = 0; i < 4; i++) {
                    encodedId.Write(Convert.ToUInt16(currentId[i+1])); 
                    // we skip the first item 
                }
                b += ENCODED_ID_LENGTH;
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
                for (int i = 0; i < 4; i++) {
                    b1 = encReader.GetIDByte(idByte++);
                    b2 = encReader.GetIDByte(idByte++);
                    decodedIdNumbers[i] = ToUInt16(b1, b2);
                }
                
                return String.Format(idBuilder, decodedIdNumbers[0], decodedIdNumbers[1],
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
        }
                                   
    }
}