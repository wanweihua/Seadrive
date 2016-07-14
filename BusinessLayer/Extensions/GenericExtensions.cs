using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BusinessLayer.Extensions
{
    public static class GenericExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this IList<byte> b)
        {
            long y = b[7] & 0x7F;
            y <<= 8; y += b[6];
            y <<= 8; y += b[5];
            y <<= 8; y += b[4];
            y <<= 8; y += b[3];
            y <<= 8; y += b[2];
            y <<= 8; y += b[1];
            y <<= 8; y += b[0];

            return (b[7] & 0x80) != 0 ? -y : y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLong(this IList<byte> b, long y)
        {
            if (y < 0)
            {
                y = -y;

                b[0] = (byte)y;
                b[1] = (byte)(y >>= 8);
                b[2] = (byte)(y >>= 8);
                b[3] = (byte)(y >>= 8);
                b[4] = (byte)(y >>= 8);
                b[5] = (byte)(y >>= 8);
                b[6] = (byte)(y >>= 8);
                b[7] = (byte)((y >> 8) | 0x80);
            }
            else
            {
                b[0] = (byte)y;
                b[1] = (byte)(y >>= 8);
                b[2] = (byte)(y >>= 8);
                b[3] = (byte)(y >>= 8);
                b[4] = (byte)(y >>= 8);
                b[5] = (byte)(y >>= 8);
                b[6] = (byte)(y >>= 8);
                b[7] = (byte)(y >> 8);
            }
        }
    }
}
