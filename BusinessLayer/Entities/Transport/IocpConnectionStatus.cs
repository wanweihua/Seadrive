namespace BusinessLayer.Entities.Transport
{
    public enum ConnectStatus
    {
        Success = 0,
        FailTimeOut,
        FailAlreadyConnected,
        FailSocketError
    }

    public enum StartStatus
    {
        Success = 0,
        FailAlreadyStarted,
        FailSocketError
    }

    public enum SendStatus : uint
    {
        Success = 0,
        FailSocketError,
        FailNotConnected,
        FailInvalidPacket,
        FailConnectionClosing
    };
}
