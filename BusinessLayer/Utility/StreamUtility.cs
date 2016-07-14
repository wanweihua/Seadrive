using System;
using System.IO;
using System.Linq;

namespace BusinessLayer.Utility
{
    public static class StreamUtility
    {
        public static void WriteBigEndian(BinaryWriter s, ulong value, int bytes = 4)
        {
            byte[] buffer = ByteUtility.ULongBigEndianBytes(value);
            s.Write(buffer, 8 - bytes, bytes);
        }

        public static uint ReadBigEndianUint32(BinaryReader s)
        {
            return (uint)ConvertFromBigEndian(s.ReadBytes(4));
        }

        public static long ConvertFromBigEndian(byte[] bytes)
        {
            return bytes.Aggregate<byte, long>(0, (current, t) => current << 8 | t);
        }

        public static long ComputeNewPosition(long offset, SeekOrigin origin, long length, long currentPosition)
        {
            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = currentPosition + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = length + offset;
                    break;
                default:
                    throw new ArgumentException("Invalid SeekOrigin");
            }

            return newPosition;
        }
    }
}
