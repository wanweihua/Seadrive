namespace BusinessLayer.Entities.IoCpPackets
{
    public static class IocpTransportProtocolConstants
    {
        public const int PacketTypeInitialPacket = 1;
        public const int PacketTypeInitialResponsePacket = 2;
        public const int PacketTypeFileResponsePacket = 3;
        public const int PacketTypeTransportFilePacket = 4;
        public const int PacketTypeFileDiffPacket = 5;
        public const int PacketTypeFileDiffResponse = 6;
        public const int PacketTypeCompressedChunk = 7;
    }
}
