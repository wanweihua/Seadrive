using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BusinessLayer.Entities.Chunking;

namespace SeaDrive.Compression
{
    public class Compressor
    {
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);
        private readonly List<Chunk> _chunks;

        public Compressor(List<Chunk> chunks)
        {
            _chunks = chunks;
        }

        public List<CompressedChunk> GetCompressedChunksSlow(CompressionMode mode)
        {
            return _chunks.Select((t, i) => new CompressedChunk(t.GetFileChecksum(), t.GetChecksum(),CompressChunkContents(t.GetContents(), mode), i)).ToList();
        }

        public List<CompressedChunk> GetCompressedChunks(CompressionMode mode)
        {
            List<CompressedChunk> retval = new List<CompressedChunk>();
            using (var compressedDataInMemoryStream = new MemoryStream())
            {
                using (var gunZip = new BufferedStream(new GZipStream(compressedDataInMemoryStream, mode), ChunkSize))
                {
                    for (int i = 0; i < _chunks.Count; i++)
                    {
                        gunZip.Write(_chunks[i].GetContents(), 0, ChunkSize);
                        retval.Add(new CompressedChunk(_chunks[i].GetFileChecksum(), _chunks[i].GetChecksum(),compressedDataInMemoryStream.ToArray(), i));
                        compressedDataInMemoryStream.Position = 0;
                    }
                }
            }
            return retval;
        }

        private static byte[] CompressChunkContents(byte[] content, CompressionMode mode)
        {
            byte[] retval;
            using (var compressedDataInMemoryStream = new MemoryStream())
            {
                using (var gunZip = new BufferedStream(new GZipStream(compressedDataInMemoryStream, mode)))
                {
                    gunZip.Write(content, 0 , content.Length);
                }
                retval = compressedDataInMemoryStream.ToArray();
            }
            return retval;
        }
    }
}
