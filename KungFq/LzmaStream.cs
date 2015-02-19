using System;
using System.IO;
using SevenZip;
using System.Threading;
using System.Runtime.Remoting;

namespace KungFq
{
    public class LzmaStream : Stream
    {
            
        #region LzmaStaticOptions
        private static Int32 dictionary = 1 << 21; //No dictionary
        private static Int32 posStateBits = 2;
        //lc - The number of literal context bits (high bits of previous literal).
        //It can be in the range from 0 to 8. The default value is 3.
        //Sometimes lc=4 gives the gain for big files.
        private static Int32 litContextBits = 3;   // for normal files  // UInt32 litContextBits = 0; // for 32-bit data
        //      lp - The number of literal pos bits (low bits of current position for literals).
        //      It can be in the range from 0 to 4. The default value is 0.
        //      The lp switch is intended for periodical data when the period is equal to 2^lp.
        //      For example, for 32-bit (4 bytes) periodical data you can use lp=2. Often it's
        //      better to set lc=0, if you change lp switch.
        private static Int32 litPosBits = 0;       // UInt32 litPosBits = 2; // for 32-bit data
        //algo = 0 means fast method
        //algo = 1 means normal method 
        //??
        private static Int32 algorithm = 2;
        private static Int32 numFastBytes = 128;
        private static bool eos = false;
        private static string mf = "bt4";
        
        private static CoderPropID[] propIDs =
        {
            CoderPropID.DictionarySize,
            CoderPropID.PosStateBits,
            CoderPropID.LitContextBits,
            CoderPropID.LitPosBits,
            CoderPropID.Algorithm,
            CoderPropID.NumFastBytes,
            CoderPropID.MatchFinder,
            CoderPropID.EndMarker
        };
        private static object[] properties =
        {
            (Int32)(dictionary),
            (Int32)(posStateBits),
            (Int32)(litContextBits),
            (Int32)(litPosBits),
            (Int32)(algorithm),
            (Int32)(numFastBytes),
            mf,
            eos
        };
        #endregion
        
        public LzmaStream(Stream stream, bool read) 
        {
            this.lowerStream = stream;
            readMode = read;
            if (read) {
                internalReadBuffer = new BinaryReader(new MemoryStream(BUFFER));
                ChargeInternalBuffer();
            } else {
                internalWriteBuffer = new BinaryWriter(new MemoryStream(BUFFER));
            }
        }
        
        Stream lowerStream;
        int readByte = 0;
        int readableByte = 0;
        static int BUFFER = 1048575;
        //static int BUFFER = 2;
        BinaryReader internalReadBuffer;   
        BinaryWriter internalWriteBuffer;
        bool readMode; 
        int writtenBytes;
        
        #region exploitStream
        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
        }
            
        [System.Obsolete("use NewMethod", true)] //TODO XXX
        protected override WaitHandle CreateWaitHandle ()
        {
            return base.CreateWaitHandle ();
        }
        
        public override void Flush ()
        {
            throw new NotImplementedException ();
        }
        
        public override long Seek (long offset, SeekOrigin origin)
        {
            throw new NotImplementedException ();
        }
        
        public override void SetLength (long value)
        {
            throw new NotImplementedException ();
        }
        
        public override IAsyncResult BeginRead (byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginRead (buffer, offset, count, callback, state);
        }
        
        public override IAsyncResult BeginWrite (byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginWrite (buffer, offset, count, callback, state);
        }
        
        public override int EndRead (IAsyncResult asyncResult)
        {
            return base.EndRead (asyncResult);
        }
        
        public override void EndWrite (IAsyncResult asyncResult)
        {
            base.EndWrite (asyncResult);
        }
              
        public override bool CanTimeout {
            get {
                return base.CanTimeout;
            }
        }
        
        public override long Length {
            get {
                throw new NotImplementedException ();
            }
        }
        
        public override long Position {
            get {
                throw new NotImplementedException ();
            }
            set {
                throw new NotImplementedException ();
            }
        }
        
        public override int ReadTimeout {
            get {
                return base.ReadTimeout;
            }
            set {
                base.ReadTimeout = value;
            }
        }
        
        public override int WriteTimeout {
            get {
                return base.WriteTimeout;
            }
            set {
                base.WriteTimeout = value;
            }
        }
        
        /*public void Dispose ()
        {
            throw new NotImplementedException ();
        }
        */
        
        public override ObjRef CreateObjRef (Type requestedType)
        {
            return base.CreateObjRef (requestedType);
        }
        
        /*public override object InitializeLifetimeService ()
        {
            return base.InitializeLifetimeService ();
        }
        
        protected override void Finalize ()
        {
            base.Finalize ();
        }
        */
        
        public override bool CanRead {
            get {
                if (readMode)
                    return true;
                else 
                    return false;  
            }
        }
        
        public override bool CanSeek {
            get {
                return false;
            }
        }
        
        public override bool CanWrite {
            get {
                return !CanRead;
            }
        }
        
        public override bool Equals (object obj)
        {
            return base.Equals (obj);
        }
        
        public override int GetHashCode ()
        {
            return base.GetHashCode ();
        }
        public override string ToString ()
        {
            return string.Format ("[LzmaStream]");
        }
        
        
        #endregion
        
        #region ReadMode
        public override int Read(byte[] buffer, int offset, int count)
        {
            byte[] read = ReadBytes(count);
            for (int i = 0; i < read.Length; i++) {
                buffer[offset+i] = read[i];   
            }
            return read.Length;
        }
        /* ArgumentException     

        The sum of offset and count is larger than the buffer length.
        ArgumentNullException   
        
        buffer is null.
        ArgumentOutOfRangeException     
        
        offset or count is negative.
        IOException     
        
        An I/O error occurs.
        NotSupportedException   
        
        The stream does not support reading.
        ObjectDisposedException     
        
        Methods were called after the stream was closed. */
                
        public override int ReadByte()
        {
            byte[] read = ReadBytes(1);
            if (read.Length != 1) {
                return -1; //EOF
            }
            return (int) read[0];
        }
        
        
        byte[] ReadBytes(int count) 
        {
            if (readByte + count > readableByte) {
                byte[] res = new byte[count];
                int read = internalReadBuffer.Read(res, 0, readableByte - readByte);
                count -= read;
                while (count != 0) { //BUGGY TODO
                    ChargeInternalBuffer();
                    int readable = readableByte - readByte; //BUFFER
                    int toGet = (count < readable) ? count : readable; 
                    count -= toGet;
                    read += internalReadBuffer.Read(res, read, toGet); //read or 0? read!
                    readByte += toGet;
                }
                return res;
            } else {
                readByte += count;
                //return internalReadBuffer.ReadBytes(count);
                byte[] res = internalReadBuffer.ReadBytes(count);
                return res;
            }
        }
        
        void ChargeInternalBuffer()
        {
            //XXX TODO
            internalReadBuffer = new BinaryReader(new MemoryStream(BUFFER));
            byte[] properties = new byte[5];
            if (lowerStream.Read(properties, 0, 5) != 5)
                throw (new Exception("input .lzma is too short"));
            SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();
            decoder.SetDecoderProperties(properties);
            long outSize = 0;
            if (BitConverter.IsLittleEndian) {
                byte[] header = new byte[8];
                outSize = lowerStream.Read(header, 0, 8);
                outSize = BitConverter.ToInt64(header, 0);
            } else {
                throw new Exception("Lzma compression not implemented for big endian machines.");  
            }
            long compressedSize = 5; //seems unused in the code!
            
            
            decoder.Code(lowerStream, internalReadBuffer.BaseStream, compressedSize, outSize, null);
            internalReadBuffer.BaseStream.Seek(0, SeekOrigin.Begin);
            //readableByte += (int) outSize;
            readableByte = (int) outSize;
            readByte = 0;
            
        }
        
        public BinaryReader Reader
        {
            get
            {
                return internalReadBuffer;
            }
        }
        
        #endregion
        
        public override void Close() 
        {
            if (!readMode) {
                WriteBuffer();   
                internalWriteBuffer.Close();
            } else {
                internalReadBuffer.Close();   
            }
            lowerStream.Close();
        }
                
        #region WriteMode
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (writtenBytes >= BUFFER) {
                WriteBuffer();   
            }
            
            int spaceLeft = BUFFER - writtenBytes;
            int written = (count < spaceLeft) ? count : spaceLeft;
            internalWriteBuffer.Write(buffer, offset, written);
            writtenBytes += written;
            count -= written;
            while (count != 0) {
                WriteBuffer();
                spaceLeft = BUFFER - writtenBytes; //could use BUFFER directly
                int toWrite = (count < spaceLeft) ? count : spaceLeft;
                count -= toWrite;
                internalWriteBuffer.Write(buffer, written, toWrite); //i put -1! stIupid!
                written += toWrite;
                writtenBytes += toWrite;
            }     
        }

        public override void WriteByte(byte value)
        {
            if (writtenBytes >= BUFFER) {
                WriteBuffer();   
            }
            internalWriteBuffer.Write(value);
            writtenBytes++;
        }
        
        void WriteBuffer()
        {
            internalWriteBuffer.Flush();
            internalWriteBuffer.BaseStream.Seek(0, SeekOrigin.Begin);
            SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(lowerStream);
            byte[] LengthHeader = BitConverter.GetBytes(internalWriteBuffer.BaseStream.Length); 
            lowerStream.Write(LengthHeader, 0, LengthHeader.Length);
            encoder.Code(internalWriteBuffer.BaseStream, lowerStream, -1, -1, null);
            writtenBytes = 0;
            internalWriteBuffer = new BinaryWriter(new MemoryStream(BUFFER));
        }
        
        #endregion
        
    }
}

