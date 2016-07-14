using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets
{
    [Serializable]
    public class IocpRemoteFileUploadMetadataPacket : ISerializable
    {
        public string LocalServerUuid;
        public string FileUuid;
        public string FileRelativePath;
        public byte[] FileChecksum;
        public int NumChunks;

        public IocpRemoteFileUploadMetadataPacket(string localServerUuid, string fileUuid, string fileRelativePath, byte[] fileChecksum, int numChunks)
        {
            LocalServerUuid = localServerUuid;
            FileUuid = fileUuid;
            FileRelativePath = fileRelativePath;
            FileChecksum = fileChecksum;
            NumChunks = numChunks;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpRFUpacklsUUid", LocalServerUuid, typeof(string));
            info.AddValue("iocpRFUpacklsFiUuid", FileUuid, typeof(string));
            info.AddValue("iocpRFUpacklsFRpath", FileRelativePath, typeof(string));
            info.AddValue("iocpRFUpacklsFCh", FileChecksum, typeof(byte[]));
            info.AddValue("iocpRFUpacklsNumChunks", NumChunks, typeof(int));
        }

        public IocpRemoteFileUploadMetadataPacket(SerializationInfo info, StreamingContext context)
        {
            LocalServerUuid = (string) info.GetValue("iocpRFUpacklsUUid", typeof (string));
            FileUuid = (string)info.GetValue("iocpRFUpacklsFiUuid", typeof(string));
            FileRelativePath = (string)info.GetValue("iocpRFUpacklsFRpath", typeof(string));
            FileChecksum = (byte[]) info.GetValue("iocpRFUpacklsFCh", typeof (byte[]));
            NumChunks = (int) info.GetValue("iocpRFUpacklsNumChunks", typeof (int));
        }

        public override string ToString()
        {
            return "LocalServer UUID :" + LocalServerUuid + "\n" + "FileUUid: " + FileUuid + "\n" + "NumChunks: " + NumChunks;
        }
    }
}
