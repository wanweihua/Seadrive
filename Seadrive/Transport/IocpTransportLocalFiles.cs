using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Seadrive.Transport
{
    [Serializable]
    public class IocpTransportLocalFiles : ISerializable
    {
        public List<IocpTransportFile> IocpTransportFiles;

        public IocpTransportLocalFiles()
        {
            IocpTransportFiles = new List<IocpTransportFile>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("iocptlFiocptf", IocpTransportFiles, typeof(List<IocpTransportFile>));
        }

        public IocpTransportLocalFiles(SerializationInfo info, StreamingContext context)
        {
            IocpTransportFiles = (List<IocpTransportFile>) info.GetValue("iocptlFiocptf", typeof (List<IocpTransportFile>));
        }

    }
}
