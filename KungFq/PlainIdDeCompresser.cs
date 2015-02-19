using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


namespace KungFq
{
	public class PlainIdDeCompresser : IIdDeCompresser
	{
		
		public PlainIdDeCompresser(EncodedFastqReader encReader)
		{
			this.encReader = encReader;
		}
		
		
		public PlainIdDeCompresser(FastqReader reader, BinaryWriter writer)
		{
			this.reader = reader;
			this.writer = writer;
		}
		
		const int BUFFER = 1048575;
		const int BIT_BUFFER = BUFFER * 8;
		const int ID_BUFFER = BUFFER; 
		EncodedFastqReader encReader;
		BinaryWriter writer;
		FastqReader reader;
		StringBuilder idContinuation = new StringBuilder();
		LinkedList<string> decodedIDs = new LinkedList<string>();
		ASCIIEncoding ae = new ASCIIEncoding();
		
		/* Encodes IDs starting at the given index (id) until "buffer is full"
		 * or the fastq file ends and writes the result in the given BinaryWriter.
		 * Updates id according to its advancements.
		 */
		public void EncodeId(ref int id)
		{
			// should check if "mode" is right (ie. reader && writer != null)
			// but we avoid doing so for efficiency
			
			//the first byte starts with 11 if we are encoding an ID
            byte first = (byte) 64;
			int b = 0;
			StringBuilder ids = new StringBuilder();
			if (idContinuation.Length != 0) {
				ids.Append(idContinuation);
				b += idContinuation.Length;
				idContinuation = new StringBuilder();
			}
			//we assume that a continuation will never be longer
			//than BUFFER
			
			while (reader.HasIDLeft(id, 1) && b < ID_BUFFER) {
				string currentId = reader.GetID(id);
				b += currentId.Length;
				if (b > ID_BUFFER) {
					//continuation
					ids.Append(currentId.Substring(0, ID_BUFFER - (b - currentId.Length)));
					
					idContinuation.Append(currentId.Substring(ID_BUFFER - (b - currentId.Length)));
					b = ID_BUFFER;
				} else {
					ids.Append(currentId);
				}
				id++;
				//here method to deal with known ID's structure
			}
			//we use ascii encoding, so 1 char = 1 byte
			if (b == ID_BUFFER) {
				writer.Write(first);
				writer.Write(ae.GetBytes(ids.ToString()));
			} else if (b < ID_BUFFER) {
				//mark smaller buffer
				first += (byte) 32; //we have to tell the decoder that we have a block with a length
                                    //different than BUFFER
                writer.Write(first);
                writer.Write(b);
                writer.Write(ae.GetBytes(ids.ToString()));
			}
		}
		
		public string GetNextID(ref long IdByte)
		{
			// should check if "mode" is right (ie. encReader != null)
			// but we avoid doing so for efficiency
			
			string res = "";
			byte[] b = new byte[1];
			string s = "";
			bool hasOneID = false;
			if (decodedIDs.Count >= 1) {
				res = decodedIDs.First.ToString();
				decodedIDs.RemoveFirst();
			} else {
				while (encReader.HasIDLeft(IdByte, 1) && !hasOneID) {
					b[0] = encReader.GetIDByte(IdByte++);
					idContinuation.Append(ae.GetString(b));
					s = idContinuation.ToString();
					if (s.StartsWith("@") && s.EndsWith("@") && s.Length != 1) {
						hasOneID = true;
						idContinuation.Remove(0, s.Length-1);
						res = s.Replace("@", "");
						//decodedIDs.AddLast(ids[0]);
					}
				}
			}
			if (!hasOneID && s.Length != 0) {
				res = s.Replace("@", "");
			}
			return "@" + res;
		}
	}
}

