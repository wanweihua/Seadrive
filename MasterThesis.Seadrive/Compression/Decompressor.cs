using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using SeaDrive.Cache.Models;
using SeaDrive.Chunking;

namespace SeaDrive.Compression
{
    public class Decompressor
    {
        private readonly List<CompressedChunk> _chunks;
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);
        public Decompressor(List<CompressedChunk> chunks)
        {
            _chunks = chunks;
        }

        public static List<Chunk> DecompressCachedEntries(List<CacheEntries> entries)
        {
            List<Chunk> retval = new List<Chunk>();
            entries.Sort((x, y) => x.BlockNum.CompareTo(y.BlockNum));
            for (int i = 0; i < entries.Count; i++)
            {
                if (i != entries[i].BlockNum)
                {
                    throw new Exception("Cannot decompress Cached Entries");
                }

                using (var sha1 = SHA1.Create())
                {
                    byte[] chunkBytes = Decompress(entries[i].ZippedData);
                    Chunk chunk = new Chunk(sha1.ComputeHash(chunkBytes), chunkBytes, ChunkSize, entries[i].FileChecksum);
                    retval.Add(chunk);
                }
            }
            return retval;
        } 

        public List<Chunk> Decompress(byte[] fileChecksum, string filename)
        {
            //Async send might have recieved them out of order
            _chunks.Sort((x, y) => x.GetChunkNum().CompareTo(y.GetChunkNum()));
            List<Chunk> retval = new List<Chunk>();
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (i != _chunks[i].GetChunkNum())
                {
                    throw new Exception("Cannot compress file: " + filename);
                }
                                  
                using (var sha1 = SHA1.Create())
                {
                    byte[] chunkBytes = Decompress(_chunks[i].GetCompressedChunk());
                    Chunk chunk = new Chunk(sha1.ComputeHash(chunkBytes), chunkBytes, ChunkSize, fileChecksum);
                    retval.Add(chunk);
                }
            }
            return retval;
        }

        private static byte[] Decompress(byte[] data)
        {
            var buf = new byte[ChunkSize];

            using (var memoryStream = new MemoryStream(data))
            {
                using (var gunZip = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gunZip.Read(buf, 0, ChunkSize);
                }
            }
            return buf;
        }
    }
}
