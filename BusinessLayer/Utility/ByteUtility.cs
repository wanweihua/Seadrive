using System;
using System.Collections.Generic;
using System.Linq;

namespace BusinessLayer.Utility
{
    public class ByteUtility
    {
        public static bool ContainsSequence(byte[] toSearch, byte[] toFind)
        {
            for (var i = 0; i + toFind.Length < toSearch.Length; i++)
            {
                var allSame = !toFind.Where((t, j) => toSearch[i + j] != t).Any();

                if (allSame)
                {
                    return true;
                }
            }

            return false;
        }

        public static int CompareBytes(IList<byte> left, IList<byte> right)
        {
            var diff = 0;
            for (var i = 0; i < left.Count && i < right.Count; i++)
            {
                diff = left[i] - right[i];
                if (diff != 0)
                    break;
            }
            return diff;
        }

        public static byte[] ULongBigEndianBytes(ulong value)
        {
            byte[] retval = new byte[8];
            retval[0] = (byte)(value >> 56);
            retval[1] = (byte)(value >> 48);
            retval[2] = (byte)(value >> 40);
            retval[3] = (byte)(value >> 32);
            retval[4] = (byte)(value >> 24);
            retval[5] = (byte)(value >> 16);
            retval[6] = (byte)(value >> 8);
            retval[7] = (byte)(value);
            return retval;
        }

        public static byte[] Combine(byte[] sourceArray, byte[] appendingArray)
        {
            byte[] retval = new byte[sourceArray.Length + appendingArray.Length];
            Buffer.BlockCopy(sourceArray, 0, retval, 0, sourceArray.Length);
            Buffer.BlockCopy(appendingArray, 0, retval, sourceArray.Length, appendingArray.Length);
            return retval;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }


        /// <summary>
        /// Generic method to combine n number of bytes
        /// </summary>
        /// <param name="arrays"></param>
        /// <returns></returns>
        public byte[] CombineByteArrays(params byte[][] arrays)
        {
            int offset = 0;
            byte[] retval = new byte[arrays.Sum(a => a.Length)];
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, retval, offset, array.Length);
                offset += array.Length;
            }
            return retval;
        }

        public static int MatchLength(IList<byte> oldData, IList<byte> newData)
        {
            int i;
            for (i = 0; i < oldData.Count && i < newData.Count; i++)
            {
                if (oldData[i] != newData[i])
                    break;
            }

            return i;
        }
    }
}
