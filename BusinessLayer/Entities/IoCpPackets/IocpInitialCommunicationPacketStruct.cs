using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpInitialCommunicationPacketStruct : ISerializable
    {
        public string Guid;
        public byte[] Checksum;

        public IocpInitialCommunicationPacketStruct(string guid, byte[] checksum)
        {
            Guid = guid;
            Checksum = checksum;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("initcpsg", Guid, typeof(string));
            info.AddValue("initcpsc", Checksum, typeof(byte[]));
        }

        public IocpInitialCommunicationPacketStruct(SerializationInfo info, StreamingContext context)
        {
            Guid = (string) info.GetValue("initcpsg", typeof (string));
            Checksum = (byte[]) info.GetValue("initcpsc", typeof (byte[]));
        }
    }
}
