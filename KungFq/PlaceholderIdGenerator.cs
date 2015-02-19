using System;
namespace KungFq
{
    public class PlaceholderIdGenerator : IIdDeCompresser
    {
        
        public void EncodeId(ref int id)
        {
            throw new NotImplementedException("This class generates only placeholder IDs");
        }
        
        public string GetNextID(ref long IdByte)
        {
            IdByte++;
            return "@" + IdByte;
        }
    }
}

