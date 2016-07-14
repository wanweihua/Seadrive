using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace BusinessLayer.Entities.Transport
{
    public sealed class PacketSerializer<TPacketStruct> : IDisposable where TPacketStruct : class,ISerializable
    {
        private MemoryStream _memoryStream;
        private readonly BinaryFormatter _formatter = new BinaryFormatter();

        sealed class AllowAllAssemblyVersionDeserializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                String currentAssembly = Assembly.GetAssembly(typeof(TPacketStruct)).FullName;
                assemblyName = currentAssembly;
                var typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeof(TPacketStruct).FullName, assemblyName));
                return typeToDeserialize;
            }
        }


        public SerializerMode Mode
        {
            get;
            set;
        }

        public PacketSerializer(TPacketStruct packet = null, SerializerMode serializerMode = SerializerMode.Default)
        {
            _formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;

            _memoryStream = new MemoryStream();
            Mode = serializerMode;
            switch (Mode)
            {
                case SerializerMode.Default:
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                    _formatter.Serialize(_memoryStream, packet);
                    break;
            }

        }

        public PacketSerializer(byte[] rawData, SerializerMode serializerMode = SerializerMode.Default)
        {
            _formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
            Mode = serializerMode;
            switch (Mode)
            {
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                    _formatter.Binder = new AllowAllAssemblyVersionDeserializationBinder();
                    break;
                case SerializerMode.Default:
                    break;
            }
            _memoryStream = new MemoryStream(rawData);
        }

        public PacketSerializer(byte[] rawData, int offset, int count, SerializerMode serializerMode = SerializerMode.Default)
        {
            _formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
            Mode = serializerMode;
            switch (Mode)
            {
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                    _formatter.Binder = new AllowAllAssemblyVersionDeserializationBinder();
                    break;
                case SerializerMode.Default:
                    break;
            }
            _memoryStream = new MemoryStream(rawData, offset, count);
        }

        public PacketSerializer(PacketSerializer<TPacketStruct> orig)
        {
            _formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
            Mode = orig.Mode;
            switch (Mode)
            {
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                    _formatter.Binder = new AllowAllAssemblyVersionDeserializationBinder();
                    break;
                case SerializerMode.Default:
                    break;
            }
            _memoryStream = orig._memoryStream;
        }

        public byte[] PacketRaw
        {
            get
            {
                return _memoryStream.GetBuffer();
            }
        }

        public long PacketByteSize
        {
            get
            {
                return _memoryStream.Length;
            }
        }

        public TPacketStruct ClonePacketObj()
        {
            TPacketStruct retPacketObj = null;
            _memoryStream.Seek(0, SeekOrigin.Begin);
            switch (Mode)
            {
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                case SerializerMode.Default:
                    retPacketObj = (TPacketStruct)_formatter.Deserialize(_memoryStream);
                    break;
            }
            return retPacketObj;
        }

        public void SetPacket(TPacketStruct obj)
        {
            _memoryStream = new MemoryStream();
            switch (Mode)
            {
                case SerializerMode.Default:
                case SerializerMode.AllowAllAssemblyVersionDeserialization:
                    _formatter.Serialize(_memoryStream, obj);
                    break;
            }
        }

        public void SetPacket(byte[] rawData)
        {
            _memoryStream = new MemoryStream(rawData);

        }

        public void SetPacket(byte[] rawData, int offset, int count)
        {
            _memoryStream = new MemoryStream(rawData, offset, count);
        }

        private bool IsDisposed { get; set; }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (IsDisposed) return;
                if (!isDisposing) return;
                if (_memoryStream == null) return;
                _memoryStream.Dispose();
                _memoryStream = null;
            }
            finally
            {
                IsDisposed = true;
            }
        }

        ~PacketSerializer() { Dispose(false); }

        public enum SerializerMode
        {
            Default,
            AllowAllAssemblyVersionDeserialization
        }
    }
}
