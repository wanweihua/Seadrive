using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BusinessLayer.Entities.Chunking;

//SOLELY FOR EXISTINGFILES
namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpInitialResponsePacketStruct : ISerializable
    {
        public string Guid;
        public List<CompressedChunk> Chunks; 
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("initRpSG", Guid, typeof(string));
            info.AddValue("initRpScC", Chunks, typeof(List<CompressedChunk>));
        }

        public IocpInitialResponsePacketStruct(SerializationInfo info, StreamingContext context)
        {
            Guid = (string) info.GetValue("initRpSG", typeof (string));
            Chunks = (List<CompressedChunk>) info.GetValue("initRpScC", typeof (List<CompressedChunk>));
        }
    }
}
