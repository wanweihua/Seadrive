using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets
{
    [Serializable]
    public class IocpRemoteRetransmissionPacket : ISerializable
    {
        public string LocalServerUuid;

        public IocpRemoteRetransmissionPacket(string localServerUuid)
        {
            LocalServerUuid = localServerUuid;
        }


        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpRRetranspacklsUUid", LocalServerUuid, typeof(string));
        }

        public IocpRemoteRetransmissionPacket(SerializationInfo info, StreamingContext context)
        {
            LocalServerUuid = (string) info.GetValue("iocpRRetranspacklsUUid", typeof (string));
        }
    }
}
