using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpInitialCommunicationPacket : ISerializable
    {
        public string ClientUuid;
        public List<IocpInitialCommunicationPacketStruct> InitialData;

        public IocpInitialCommunicationPacket()
        {
            
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("initcpCluuid", ClientUuid, typeof(string));
            info.AddValue("initcpPss", InitialData, typeof(List<IocpInitialCommunicationPacketStruct>));
        }

        public IocpInitialCommunicationPacket(SerializationInfo info, StreamingContext context)
        {
            ClientUuid = (string) info.GetValue("initcpCluuid", typeof (string));
            InitialData = (List<IocpInitialCommunicationPacketStruct>) info.GetValue("initcpPss", typeof (List<IocpInitialCommunicationPacketStruct>));
        }
    }
}
