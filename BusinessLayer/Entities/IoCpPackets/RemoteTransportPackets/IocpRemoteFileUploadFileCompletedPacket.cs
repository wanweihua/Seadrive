using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets
{
    [Serializable]
    public class IocpRemoteFileUploadFileCompletedPacket : ISerializable
    {
        public string Uuid;
        public bool Ok;

        public IocpRemoteFileUploadFileCompletedPacket(string localServerUuid, bool ok)
        {
            Uuid = localServerUuid;
            Ok = ok;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpFUFCpU", Uuid, typeof(string));
            info.AddValue("iocpFUFCpOk", Ok, typeof(bool));
        }

        public IocpRemoteFileUploadFileCompletedPacket(SerializationInfo info, StreamingContext context)
        {
            Uuid = (string) info.GetValue("iocpFUFCpU", typeof (string));
            Ok = (bool) info.GetValue("iocpFUFCpOk", typeof (bool));
        }
    }
}
