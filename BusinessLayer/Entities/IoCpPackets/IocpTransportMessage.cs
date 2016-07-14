using System;
using System.Runtime.Serialization;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Serializable]
    public class IocpTransportMessage : ISerializable
    {
        private readonly int _messageType;
        private readonly int _dataLength;
        private readonly byte[] _data;

        public IocpTransportMessage(int messageType, int dataLength, byte[] data)
        {
            _messageType = messageType;
            _dataLength = dataLength;
            _data = data;
        }

        public int GetMessageType()
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
            info.AddValue("dataLen", _dataLength, typeof(int));
            info.AddValue("msgData", _data, typeof(byte[]));
        }

        public IocpTransportMessage(SerializationInfo info, StreamingContext context)
        {
            _messageType = (int) info.GetValue("msgType", typeof (int));
            _dataLength = (int)info.GetValue("dataLen", typeof(int));
            _data = (byte[])info.GetValue("msgData", typeof(byte[]));
        }
    }
}
