using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpLocalServerResponsePacket : ISerializable
    {
        public List<string> GuidList;

        public IocpLocalServerResponsePacket()
        {
            GuidList = new List<string>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ilsrplocalSGl", GuidList, typeof(List<string>));
        }

        public IocpLocalServerResponsePacket(SerializationInfo info, StreamingContext context)
        {
            GuidList = (List<string>)info.GetValue("ilsrplocalSGl", typeof(List<string>));
        }
    }
}
