using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpInitialResponsePacket : ISerializable
    {
        public List<byte[]> Checksums;

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("initresSums", Checksums, typeof(List<byte[]>));
        }

        public IocpInitialResponsePacket(SerializationInfo info, StreamingContext context)
        {
            Checksums = (List<byte[]>)info.GetValue("initresSums", typeof(List<byte[]>));
        }
    }
}
