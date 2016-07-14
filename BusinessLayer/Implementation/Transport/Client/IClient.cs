using System;
using System.Threading;
using BusinessLayer.Entities.Transport;

namespace BusinessLayer.Implementation.Transport.Client
{
    public sealed class ClientOperands
    {
        public INetworkClientCallback CallBackObj
        {
            get;
            set;
        }

        public String HostName
        {
            get;
            set;
        }

        public String Port
        {
            get;
            set;
        }

        public bool NoDelay
        {
            get;
            set;
        }

        public int ConnectionTimeOut
        {
            get;
            set;
        }

        public ClientOperands()
        {
            CallBackObj = null;
            HostName = DefaultServerConfiguration.DefaultHostname;
            Port = DefaultServerConfiguration.DefaultPort;
            NoDelay = true;
            ConnectionTimeOut = Timeout.Infinite;
        }

        public ClientOperands(INetworkClientCallback callBackObj, String hostName, String port, bool noDelay = true, int connectionTimeOut = Timeout.Infinite)
        {
            CallBackObj = callBackObj;
            HostName = hostName;
            Port = port;
            NoDelay = noDelay;
            ConnectionTimeOut = connectionTimeOut;
        }

        public static ClientOperands DefaultClientOperands = new ClientOperands();
    };

    public interface INetworkClient
    {
        String HostName { get; }
        String Port { get; }
        bool NoDelay
        {
            get;
        }
        int ConnectionTimeOut
        {
            get;
        }
        INetworkClientCallback CallBackObj
        {
            get;
            set;
        }
        OnClientConnectedDelegate OnConnected
        {
            get;
            set;
        }

        OnClientReceivedDelegate OnReceived
        {
            get;
            set;
        }

        OnClientSentDelegate OnSent
        {
            get;
            set;
        }

        OnClientDisconnectDelegate OnDisconnect
        {
            get;
            set;
        }

        void Connect(ClientOperands operands);
        void Disconnect();
        bool IsConnectionAlive { get; }
        void Send(Packet packet);
        void Send(byte[] data, int offset, int dataSize);
        void Send(byte[] data);


    }

    public delegate void OnClientConnectedDelegate(INetworkClient client, ConnectStatus status);
    public delegate void OnClientReceivedDelegate(INetworkClient client, Packet receivedPacket);
    public delegate void OnClientSentDelegate(INetworkClient client, SendStatus status, Packet sentPacket);
    public delegate void OnClientDisconnectDelegate(INetworkClient client);

    public interface INetworkClientCallback
    {
        void OnConnected(INetworkClient client, ConnectStatus status);
        void OnDataReceived(INetworkClient client, Packet receivedPacket);
        void OnSent(INetworkClient client, SendStatus status, Packet sentPacket);
        void OnDisconnect(INetworkClient client);
    };
}
