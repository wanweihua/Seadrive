using System;

namespace BusinessLayer.Extensions
{
    public static class ArrayExtensions
    {
        public static ArraySegment<T> Slice<T>(this T[] buf, int offset, int count = -1)
        {
            return new ArraySegment<T>(buf, offset, count < 0 ? buf.Length - offset : count);
        }

        public static ArraySegment<T> Slice<T>(this ArraySegment<T> segment, int offset, int count = -1)
        {
            return segment.Array.Slice(offset, count);
        }

        public static void WriteLongAt(this byte[] pb, int offset, long y)
        {
            pb.Slice(offset, sizeof(long)).WriteLong(y);
        }


        public static long ReadLongAt(this byte[] buf, int offset)
        {
            return buf.Slice(offset, sizeof(long)).ReadLong();
        }
    }
}
