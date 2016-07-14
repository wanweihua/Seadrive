using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using BusinessLayer.Entities.Chunking;

namespace BusinessLayer.Implementation.Chunking
{
    public class VariableLengthChunker : IChunker
    {
        //Normally we select from different implementations, for simplicty show adler 32
        private readonly Adler32RollingChecksum _checksumAlgorithm = new Adler32RollingChecksum();
        private short _chunkSize;
        //Two-divisor, lets not go out of bounds
        public static readonly short MinimumChunkSize = 128;
        public static readonly short DefaultChunkSize = 2048;
        public static readonly short MaximumChunkSize = 31 * 1024;

        public short ChunkSize
        {
            get { return _chunkSize; }
            set
            {
                Debug.Assert(value < MinimumChunkSize);
                Debug.Assert(value > MaximumChunkSize);
                _chunkSize = value;
            }
        }

        public List<Chunk> ChunkFile(string fileName)
        {
            List<Chunk> retval = new List<Chunk>();
            int offset = 0;
            byte[] fileContents = File.ReadAllBytes(fileName);
            byte[] fileChecksum;
            using (var sha1 = SHA1.Create())
            {
                fileChecksum = sha1.ComputeHash(fileContents);
            }
             while (offset < fileContents.Length)
            {
                byte[] chunkBytes = new byte[ChunkSize];
                int chunkLength = Math.Min(ChunkSize, fileContents.Length - offset);
                Buffer.BlockCopy(fileContents, offset, chunkBytes, 0, chunkLength);
                offset += chunkLength;
                Chunk chunk = new Chunk(BitConverter.GetBytes(_checksumAlgorithm.Calculate(chunkBytes, offset, chunkLength)), chunkBytes, ChunkSize, fileChecksum);
                    retval.Add(chunk);
            }
            return retval;
        }
    }
}
