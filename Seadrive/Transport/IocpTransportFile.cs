using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BusinessLayer.Entities.Chunking;
using Data_Abstraction_Layer.Deduplication.Models;

namespace Seadrive.Transport
{
    [Serializable]
    public class IocpTransportFile : ISerializable
    {
        private readonly Guid _guid;
        private readonly byte[] _fileChecksum;
        private readonly int _numChunks;
        private readonly List<CompressedChunk> _chunks;
        private readonly string _filename;

        public IocpTransportFile(Guid guid, byte[] fileChecksum, int numChunks, List<CompressedChunk> chunks, string filename)
        {
            _guid = guid;
            _fileChecksum = fileChecksum;
            _numChunks = numChunks;
            _chunks = chunks;
            _filename = filename;
        }

        //Construct from cache
        public IocpTransportFile(Guid guid, string filename, IEnumerable<CacheEntries> data)
        {
            _guid = guid;
            _filename = filename;
            _chunks = new List<CompressedChunk>();
            foreach (CompressedChunk chunk in data.Select(entry => new CompressedChunk(entry.FileChecksum, entry.BlockChecksum, entry.ZippedData, entry.BlockNum)))
            {
                _chunks.Add(chunk);
            }
            _numChunks = _chunks.Count();
            _fileChecksum = _chunks[0].GetFileChecksum();

        }

        public byte[] GetFileChecksum()
        {
            return _fileChecksum;
        }

        public string GetGuidAsString()
        {
            return _guid.ToString();
        }

        public string GetFilename()
        {
            return _filename;
        }

        public byte[] GetCurrentFile()
        {
            return _fileChecksum;
        }

        public int GetNumChunks()
        {
            return _numChunks;
        }

        public List<CompressedChunk> GetChunks()
        {
            return _chunks;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iotfFileCheck", _fileChecksum, typeof(byte[]));
            info.AddValue("iotfNumChunks", _numChunks, typeof(int));
            info.AddValue("iotfCChunks", _chunks, typeof(List<CompressedChunk>));
            info.AddValue("iotfFName", _filename, typeof(string));
        }

        public IocpTransportFile(SerializationInfo info, StreamingContext context)
        {
            _fileChecksum = (byte[]) info.GetValue("iotfFileCheck", typeof (byte[]));
            _numChunks = (int) info.GetValue("iotfNumChunks", typeof (int));
            _chunks = (List<CompressedChunk>) info.GetValue("iotfCChunks", typeof (List<CompressedChunk>));
            _filename = (string) info.GetValue("iotfFName", typeof (string));
        }
    }
}
