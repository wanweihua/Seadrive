using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets
{
    [Serializable]
    public class IocpRemoteRetransmissionResponsePacket : ISerializable
    {
        public string FileGuid;
        public List<int> SuccessfullyTransferredBlocksId;

        public IocpRemoteRetransmissionResponsePacket(string fileGuid, List<int> successfullyTransferredBlockIds)
        {
            FileGuid = fileGuid;
            SuccessfullyTransferredBlocksId = successfullyTransferredBlockIds;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocpRetransResPguid", FileGuid, typeof(string));
            info.AddValue("iocpRetransResPBid", SuccessfullyTransferredBlocksId, typeof(List<int>));
        }

        public IocpRemoteRetransmissionResponsePacket(SerializationInfo info, StreamingContext context)
        {
            FileGuid = (string) info.GetValue("iocpRetransResPguid", typeof (string));
            SuccessfullyTransferredBlocksId = (List<int>) info.GetValue("iocpRetransResPBid", typeof (List<int>));
        }
    }
}
