using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpRemoteProtocolIntialResponsePacket : ISerializable
    {
        public Dictionary<string, byte[]> GuidAndKnownVersions;

        public IocpRemoteProtocolIntialResponsePacket()
        {
            GuidAndKnownVersions = new Dictionary<string, byte[]>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpRpIRP", GuidAndKnownVersions, typeof(Dictionary<string, byte[]>));
        }

        public IocpRemoteProtocolIntialResponsePacket(SerializationInfo info, StreamingContext context)
        {
            GuidAndKnownVersions = (Dictionary<string, byte[]>) info.GetValue("iocpRpIRP", typeof (Dictionary<string, byte[]>));
        }
    }
}
