using System;

namespace BusinessLayer.Entities.IoCpPackets
{
    [Flags]
    public enum IocpTransportFlags
    {
        ClientToServer = 1,
        ServerToClient = 1 << 1,
        InitialPacket = 1 << 2,
        FileUpload = 1 << 3,
        ChunkUpload = 1 << 4,
        TerminateOperation = 1 << 5,
        InitialPacketResponse = 1 << 6,
        FileDownload = 1 << 7,
        TransportMetadata = 1 << 8,
        TransportData = 1 << 9,
        TransportDataFileResponse = 1 << 10,
        TransportRetransmissionPacket = 1 << 11,
        TransportRetransmissionResponsePacket = 1 << 12
    }
}
