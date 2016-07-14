using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpInitialPacket : ISerializable
    { 
        public List<byte[]> Checksums;

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {       
            info.AddValue("initChsums", Checksums, typeof(List<byte[]>));
        }

        public IocpInitialPacket(SerializationInfo info, StreamingContext context)
        {
            Checksums = (List<byte[]>)info.GetValue("initChsums", typeof(List<byte[]>));
        }
    }
}
