using System;
using System.Collections.Generic;
using System.IO;
namespace BusinessLayer.Extensions
{
    public static class StreamExtensions
    {
        public static void Append(this MemoryStream stream, byte value)
        {
            stream.Append(new[] { value });
        }

        public static void Append(this MemoryStream stream, byte[] values)
        {
            stream.Write(values, 0, values.Length);
        }

        public static long ReadLong(this Stream stream)
        {
            var buf = new byte[sizeof(long)];
            if (stream.Read(buf, 0, sizeof(long)) != sizeof(long))
                throw new InvalidOperationException("Could not read long from stream");

            return buf.ReadLong();
        }

        public static IEnumerable<byte[]> BufferedRead(this Stream stream, long count, int bufferSize = 0x1000)
        {
            var readLength = (int)count;
            if (readLength <= 0) yield break;

            using (var reader = new BinaryReader(stream))
            {
                for (; readLength > 0; readLength -= bufferSize)
                {
                    yield return reader.ReadBytes(Math.Min(readLength, bufferSize));
                }
            }
        }
    }
}
