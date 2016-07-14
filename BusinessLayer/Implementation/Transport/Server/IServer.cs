using System;
using System.Collections.Generic;
using System.Net;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Implementation.Transport.Server.Rooms;

namespace BusinessLayer.Implementation.Transport.Server
{
    public sealed class ServerOperands
    {
        public INetworkServerAcceptor Acceptor
        {
            get;
            set;
        }

        public INetworkServerCallback CallBackObj
        {
            get;
            set;
        }

        public IRoomCallback RoomCallBackObj
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

        public int MaxSocketCount
        {
            get;
            set;
        }

        public ServerOperands()
        {
            Acceptor = null;
            CallBackObj = null;
            RoomCallBackObj = null;
            Port = DefaultServerConfiguration.DefaultPort;
            NoDelay = true;
            MaxSocketCount = SocketCount.Infinite;

        }

        public ServerOperands(INetworkServerAcceptor acceptor, String port, INetworkServerCallback callBackObj = null, IRoomCallback roomCallBackObj = null, bool noDelay = true, int socketCount = SocketCount.Infinite)
        {
            this.Port = port;
            this.Acceptor = acceptor;
            this.CallBackObj = callBackObj;
            this.RoomCallBackObj = roomCallBackObj;
            this.NoDelay = noDelay;
            this.MaxSocketCount = socketCount;
        }

        public static ServerOperands DefaultServerOperands = new ServerOperands();
    };

    public interface INetworkServer
    {
        String Port { get; }
        INetworkServerAcceptor Acceptor
        {
            get;
            set;
        }

        INetworkServerCallback CallBackObj
        {
            get;
            set;
        }

        IRoomCallback RoomCallBackObj
        {
            get;
            set;
        }

        bool NoDelay
        {
            get;
            set;
        }

        void StartServer(ServerOperands operands);
        void StopServer();
        bool IsServerStarted { get; }
        void ShutdownAllClient();
        void Broadcast(Packet packet);
        void Broadcast(byte[] data, int offset, int dataSize);
        void Broadcast(byte[] data);
        List<IocpTcpSocket> GetClientSocketList();
        bool DetachClient(IocpTcpSocket clientSocket);
        IRoom GetRoom(string roomName);
        List<string> RoomNames { get; }
        List<IRoom> Rooms { get; }
        void BroadcastToRoom(string roomName, Packet packet);
        void BroadcastToRoom(string roomName, byte[] data, int offset, int dataSize);
        void BroadcastToRoom(string roomName, byte[] data);
        OnServerStartedDelegate OnServerStarted
        {
            get;
            set;
        }

        OnServerAcceptedDelegate OnServerAccepted
        {
            get;
            set;
        }

        OnServerStoppedDelegate OnServerStopped
        {
            get;
            set;
        }

    }

    public delegate void OnServerStartedDelegate(INetworkServer server, StartStatus status);
    public delegate void OnServerAcceptedDelegate(INetworkServer server, INetworkSocket socket);
    public delegate void OnServerStoppedDelegate(INetworkServer server);

    public interface INetworkServerCallback
    {
        void OnServerStarted(INetworkServer server, StartStatus status);
        void OnServerAccepted(INetworkServer server, INetworkSocket socket);
        void OnServerStopped(INetworkServer server);
    };

    public interface INetworkServerAcceptor
    {
        bool OnAccept(INetworkServer server, IPInfo ipInfo);
        INetworkSocketCallback GetSocketCallback();
    };

    public interface INetworkSocket
    {
        void Disconnect();
        bool IsConnectionAlive { get; }
        void Send(Packet packet);
        void Send(byte[] data, int offset, int dataSize);
        void Send(byte[] data);
        void Broadcast(Packet packet);
        void Broadcast(byte[] data, int offset, int dataSize);
        void Broadcast(byte[] data);
        IPInfo IPInfo { get; }
        INetworkServer Server { get; }
        bool NoDelay { get; set; }

        INetworkSocketCallback CallBackObj
        {
            get;
            set;
        }

        OnSocketNewConnectionDelegate OnNewConnection
        {
            get;
            set;
        }

        OnSocketReceivedDelegate OnReceived
        {
            get;
            set;
        }

        OnSocketSentDelegate OnSent
        {
            get;
            set;
        }

        OnSocketDisconnectDelegate OnDisconnect
        {
            get;
            set;
        }

        IRoom GetRoom(string roomName);
        List<string> RoomNames { get; }
        List<IRoom> Rooms { get; }
        void Join(string roomName);
        void Leave(string roomName);
        void BroadcastToRoom(string roomName, Packet packet);
        void BroadcastToRoom(string roomName, byte[] data, int offset, int dataSize);
        void BroadcastToRoom(string roomName, byte[] data);
    }


    public delegate void OnSocketNewConnectionDelegate(INetworkSocket socket);
    public delegate void OnSocketReceivedDelegate(INetworkSocket socket, Packet receivedPacket);
    public delegate void OnSocketSentDelegate(INetworkSocket socket, SendStatus status, Packet sentPacket);
    public delegate void OnSocketDisconnectDelegate(INetworkSocket socket);


    public interface INetworkSocketCallback
    {
        void OnNewConnection(INetworkSocket socket);
        void OnReceived(INetworkSocket socket, Packet receivedPacket);
        void OnSent(INetworkSocket socket, SendStatus status, Packet sentPacket);
        void OnDisconnect(INetworkSocket socket);
    };

    public enum IPEndPointType
    {
        LOCAL = 0,
        REMOTE
    }

    public sealed class IPInfo
    {
        readonly String _mIpAddress;
        readonly IPEndPoint _mIpEndPoint;
        readonly IPEndPointType _mIpEndPointType;

        public IPInfo(String ipAddress, IPEndPoint ipEndPoint, IPEndPointType ipEndPointType)
        {
            _mIpAddress = ipAddress;
            _mIpEndPoint = ipEndPoint;
            _mIpEndPointType = ipEndPointType;
        }

        public String IPAddress
        {
            get
            {
                return _mIpAddress;
            }

        }

        public IPEndPoint IPEndPoint
        {
            get
            {
                return _mIpEndPoint;
            }
        }

        public IPEndPointType IPEndPointType
        {
            get
            {
                return _mIpEndPointType;
            }
        }
    }
}
