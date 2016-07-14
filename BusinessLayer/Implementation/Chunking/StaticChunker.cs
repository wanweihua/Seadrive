using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using BusinessLayer.Entities.Chunking;
using BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets;

namespace BusinessLayer.Implementation.Chunking
{
    public class StaticChunker : IChunker
    {
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);

        public List<Chunk> ChunkFile(string fileName)
        {
            List<Chunk> retval = new List<Chunk>();
            byte[] fileContents = File.ReadAllBytes(fileName);
            byte[] fileChecksum;
            using (var sha1 = SHA1.Create())
            {
                fileChecksum = sha1.ComputeHash(fileContents);
            }

            int offset = 0;
            while (offset < fileContents.Length)
            {
                byte[] chunkBytes = new byte[ChunkSize];
                int chunkLength = Math.Min(ChunkSize, fileContents.Length - offset);
                Buffer.BlockCopy(fileContents, offset, chunkBytes, 0, chunkLength);
                offset += chunkLength;
                using (var sha1 = SHA1.Create())
                {
                    Chunk chunk = new Chunk(sha1.ComputeHash(chunkBytes), chunkBytes, ChunkSize, fileChecksum);
                    retval.Add(chunk);
                }
            }

#if DEBUG
            const bool shouldPerformDebug = false; //Double, we dont want just debug run to start this integrity check
            if (shouldPerformDebug)
            {
                int off = 0;
                foreach (Chunk chunk in retval)
                {
                    byte[] chunkContets = chunk.GetContents();
                    for (int i = 0; i < chunk.GetContents().Length; i++)
                    {
                        if (off == fileContents.Length)
                        {
                            break;
                        }
                        if (chunkContets[i] != fileContents[off])
                        {
                            Console.WriteLine("Messed UP FILE IN CHUNKER");
                        }
                        off++;
                    }
                }
            }
#endif
            return retval;
        }

        public List<IocpRemoteFileUploadPacket> ChunkPatch(byte[] patch)
        {
            List<IocpRemoteFileUploadPacket> retval = new List<IocpRemoteFileUploadPacket>();
            int offset = 0;
            int chunkNum = 0;
            while (offset < patch.Length)
            {
                chunkNum++;
                byte[] chunkBytes = new byte[ChunkSize];
                int chunkLength = Math.Min(ChunkSize, patch.Length - offset);
                Buffer.BlockCopy(patch, offset, chunkBytes, 0, chunkLength);
                offset += chunkLength;
                IocpRemoteFileUploadPacket packet = new IocpRemoteFileUploadPacket(chunkNum, chunkLength, chunkBytes);
                retval.Add(packet);
            }

            return retval;
        } 
    }
}
