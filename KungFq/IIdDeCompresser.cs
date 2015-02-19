using System;
namespace KungFq
{
    public interface IIdDeCompresser
    {
        
        /* Encodes IDs starting at the given index (id) until "buffer is full"
         * or the fastq file ends and writes the result in the given BinaryWriter.
         * Updates id according to its advancements.
         */
        void EncodeId(ref int id);
        
        
        string GetNextID(ref long IdByte);
        
    }
}

