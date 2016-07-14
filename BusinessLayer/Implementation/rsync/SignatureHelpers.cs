using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blake2Sharp;
using BusinessLayer.Utility;

namespace BusinessLayer.Implementation.rsync
{
    public enum MagicNumber
    {
        Blake2Signature = 0x72730137,
        Md4MagicNumber = 0x72730136,
        Delta = 0x72730236
    }

    internal static class SignatureHelpers
    {
        public const int DefaultBlockLength = 2*1024;
        public const int DefaultStrongSumLength = 32;

        public static void WriteHeader(BinaryWriter s, SignatureJobSettings settings)
        {
            StreamUtility.WriteBigEndian(s, (uint) settings.MagicNumber);
            StreamUtility.WriteBigEndian(s, (uint) settings.BlockLength);
            StreamUtility.WriteBigEndian(s, (uint) settings.StrongSumLength);
        }

        public static void WriteBlock(BinaryWriter s, byte[] block, SignatureJobSettings settings)
        {
            var weakSum = CalculateWeakSum(block);
            byte[] strongSum;

            if (settings.MagicNumber == MagicNumber.Blake2Signature)
            {
                strongSum = CalculateBlake2StrongSum(block);
            }
            else
            {
                throw new NotImplementedException("Non-blake2 hashes aren't supported");
            }

            StreamUtility.WriteBigEndian(s, (ulong) weakSum, 4);
            s.Write(strongSum, 0, settings.StrongSumLength);
        }

        private static int CalculateWeakSum(byte[] buf)
        {
            var sum = new Blake32RollingChecksum();
            sum.Update(buf);
            return sum.Digest;
        }

        private static byte[] CalculateBlake2StrongSum(byte[] block)
        {
            return Blake2B.ComputeHash(block, new Blake2BConfig {OutputSizeInBytes = DefaultStrongSumLength});
        }

        public static SignatureFile ParseSignatureFile(Stream s)
        {
            var result = new SignatureFile();
            var r = new BinaryReader(s);
            var magicNumber = StreamUtility.ReadBigEndianUint32(r);
            if (magicNumber == (uint) MagicNumber.Blake2Signature)
            {
                result.StrongSumMethod = CalculateBlake2StrongSum;
            }
            else
            {
                throw new InvalidDataException(string.Format("Unknown magic number {0}", magicNumber));
            }

            result.BlockLength = (int) StreamUtility.ReadBigEndianUint32(r);
            result.StrongSumLength = (int) StreamUtility.ReadBigEndianUint32(r);

            var signatures = new List<BlockSignature>();
            ulong i = 0;
            while (true)
            {
                var weakSumBytes = r.ReadBytes(4);
                if (weakSumBytes.Length == 0)
                {
                    // we're at the end of the file
                    break;
                }

                var weakSum = (int) StreamUtility.ConvertFromBigEndian(weakSumBytes);
                var strongSum = r.ReadBytes(result.StrongSumLength);
                signatures.Add(new BlockSignature
                {
                    StartPos = (ulong) result.BlockLength*i,
                    WeakSum = weakSum,
                    StrongSum = strongSum
                });

                i++;
            }

            result.BlockLookup = signatures.ToLookup(sig => sig.WeakSum);
            return result;
        }
    }

    public struct SignatureJobSettings
    {
        public int BlockLength;
        public MagicNumber MagicNumber;
        public int StrongSumLength;
    }

    internal struct SignatureFile
    {
        public int BlockLength;
        public ILookup<int, BlockSignature> BlockLookup;
        public int StrongSumLength;
        public Func<byte[], byte[]> StrongSumMethod;
    }
}