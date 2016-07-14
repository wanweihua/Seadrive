using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    //TODO: FIXME, MAKE PRIVATE FIELDS, WRAP IN SMART STUFF, GETTERS SETTERS MAKE SURE LIST AINT EMPTY
    [Serializable]
    public class IocpInitialCommunicationResponsePacket : ISerializable
    {
        public List<string> Guids; //Missing files
        public List<IocpInitialResponsePacketStruct> DiffFiles; //diff files

        public IocpInitialCommunicationResponsePacket()
        {
            Guids = new List<string>();
            DiffFiles = new List<IocpInitialResponsePacketStruct>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpinitCRPG", Guids, typeof(List<string>));
            info.AddValue("iocpinitCPRDF", DiffFiles, typeof(List<IocpInitialResponsePacketStruct>));
        }

        public IocpInitialCommunicationResponsePacket(SerializationInfo info, StreamingContext context)
        {
            Guids = (List<string>) info.GetValue("iocpinitCRPG", typeof (List<string>));
            DiffFiles = (List<IocpInitialResponsePacketStruct>) info.GetValue("iocpinitCPRDF", typeof (List<IocpInitialResponsePacketStruct>));
        }
    }
}
