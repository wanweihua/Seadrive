using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets
{
    [Serializable]
    public class IocpRemoteFileUploadPacket : ISerializable
    {
        public int ChunkNum;
        public int Size;
        public byte[] Data;

        public IocpRemoteFileUploadPacket(int chunkNum, int size, byte[] data)
        {
            ChunkNum = chunkNum;
            Size = size;
            Data = data;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpRFUpacklsChNum", ChunkNum, typeof (int));
            info.AddValue("iocpRFUpacklsSize", Size, typeof (int));
            info.AddValue("iocpRFUpacklsData", Data, typeof(byte[]));
        }

        public IocpRemoteFileUploadPacket(SerializationInfo info, StreamingContext context)
        {
            ChunkNum = (int) info.GetValue("iocpRFUpacklsChNum", typeof (int));
            Size = (int) info.GetValue("iocpRFUpacklsSize", typeof (int));
            Data = (byte[]) info.GetValue("iocpRFUpacklsData", typeof (byte[]));
        }
    }
}
