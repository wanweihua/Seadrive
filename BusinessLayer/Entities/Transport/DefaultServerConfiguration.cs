using System;

namespace BusinessLayer.Entities.Transport
{
    public class DefaultServerConfiguration
    {
        public const String DefaultHostname = "localhost";
        public const String DefaultPort = "8080";
    }

    public static class SocketCount
    {
        public const int Infinite = 0;
    }
}
