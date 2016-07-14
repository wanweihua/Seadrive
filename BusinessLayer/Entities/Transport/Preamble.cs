using System;
using System.IO;
using System.Linq;

namespace BusinessLayer.Entities.Transport
{
    public class Preamble
    {
        public const int PacketLength = 16;
        // ReSharper disable once InconsistentNaming Same name as class, cant follow regular c# coding standards
        private const ulong preamble = 0x00F0F0F0F0F0F0F8;

        public static byte[] ToPreamblePacket(int shouldReceive)
        {
            if (shouldReceive < 0)
                return null;
            byte[] byteArr = new byte[PacketLength];
            using (MemoryStream stream = new MemoryStream(byteArr))
            {
                stream.Write(BitConverter.GetBytes(preamble), 0, 8);
                stream.Write(BitConverter.GetBytes(shouldReceive), 0, 4);
                stream.Write(BitConverter.GetBytes(0), 0, 4);
                return byteArr;
            }
        }

        public static int ToShouldReceive(byte[] preamblePacket)
        {
            ulong curPreamble = BitConverter.ToUInt64(preamblePacket, 0);
            int shouldReceive = BitConverter.ToInt32(preamblePacket, 8);
            if (preamble != curPreamble || shouldReceive < 0)
                return -1;
            return shouldReceive;

        }

        public static int CheckPreamble(byte[] preamblePacket)
        {
            byte[] correctPreamble = BitConverter.GetBytes(preamble);
            int preTrav;
            for (preTrav = 0; preTrav < preamblePacket.Length; preTrav++)
            {
                bool contains = !correctPreamble.TakeWhile((t, idx) => idx + preTrav < preamblePacket.Length).Where((t, idx) => t != preamblePacket[preTrav + idx]).Any();
                if (contains)
                    break;

            }
            return preTrav;
        }
    }
}
