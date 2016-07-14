using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpTransportPacket : ISerializable
    {
        private readonly IocpTransportFlags _messageType;
        private readonly int _dataLength;
        private readonly byte[] _data;

        public IocpTransportPacket(IocpTransportFlags messageType, int dataLength, byte[] data)
        {
            _messageType = messageType;
            _dataLength = dataLength;
            _data = data;
        }

        public IocpTransportFlags GetMessageType()
        {
            return _messageType;
        }

        public int GetDataLength()
        {
            return _dataLength;
        }

        public byte[] GetData()
        {
            return _data;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("msgType", _messageType, typeof(int));
            int typeWritten = (int) _messageType;
            info.AddValue("dataLen", _dataLength, typeof(int));
            info.AddValue("msgData", _data, typeof(byte[]));
        }

        public IocpTransportPacket(SerializationInfo info, StreamingContext context)
        {
            int messageType = (int)info.GetValue("msgType", typeof (int));
            _messageType = (IocpTransportFlags)messageType;
            _dataLength = (int)info.GetValue("dataLen", typeof(int));
            _data = (byte[])info.GetValue("msgData", typeof(byte[]));
        }
    }
}
