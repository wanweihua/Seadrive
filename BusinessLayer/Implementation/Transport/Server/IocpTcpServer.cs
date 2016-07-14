using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using BusinessLayer.Concurrency.Transport;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Implementation.Transport.Server.Rooms;

namespace BusinessLayer.Implementation.Transport.Server
{
    public sealed class IocpTcpServer : TransportBaseThread, INetworkServer
    {
        private String _port = DefaultServerConfiguration.DefaultPort;
        private bool _noDelay = true;
        private int _maxSocketCount = SocketCount.Infinite;
        private TcpListener _listener = null;
        private ServerOperands _serverOperands = null;
        private INetworkServerCallback _callBackObj = null;
        private INetworkServerAcceptor _acceptor = null;
        private IRoomCallback _roomCallBackObj = null;
        private readonly Object _generalLock = new Object();
        private readonly Object _listLock = new Object();
        private readonly Object _roomLock = new Object();
        private readonly HashSet<IocpTcpSocket> _socketList = new HashSet<IocpTcpSocket>();
        private readonly Dictionary<string, Room> _roomMap = new Dictionary<string, Room>();

        OnServerStartedDelegate _onServerStarted = delegate { };
        OnServerAcceptedDelegate _onAccepted = delegate { };
        OnServerStoppedDelegate _onServerStopped = delegate { };

        public OnServerStartedDelegate OnServerStarted
        {
            get
            {
                return _onServerStarted;
            }
            set
            {
                if (value == null)
                {
                    _onServerStarted = delegate { };
                    if (CallBackObj != null)
                        _onServerStarted += CallBackObj.OnServerStarted;
                }
                else
                {
                    _onServerStarted = CallBackObj != null && CallBackObj.OnServerStarted != value ? CallBackObj.OnServerStarted + (value - CallBackObj.OnServerStarted) : value;
                }
            }
        }

        public OnServerAcceptedDelegate OnServerAccepted
        {
            get
            {
                return _onAccepted;
            }
            set
            {
                if (value == null)
                {
                    _onAccepted = delegate { };
                    if (CallBackObj != null)
                        _onAccepted += CallBackObj.OnServerAccepted;
                }
                else
                {
                    _onAccepted = CallBackObj != null && CallBackObj.OnServerAccepted != value ? CallBackObj.OnServerAccepted + (value - CallBackObj.OnServerAccepted) : value;
                }
            }
        }

        public OnServerStoppedDelegate OnServerStopped
        {
            get
            {
                return _onServerStopped;
            }
            set
            {
                if (value == null)
                {
                    _onServerStopped = delegate { };
                    if (CallBackObj != null)
                        _onServerStarted += CallBackObj.OnServerStarted;
                }
                else
                {
                    _onServerStopped = CallBackObj != null && CallBackObj.OnServerStopped != value ? CallBackObj.OnServerStopped + (value - CallBackObj.OnServerStopped) : value;
                }
            }
        }

        public IocpTcpServer()
            : base()
        {
        }

        public IocpTcpServer(IocpTcpServer b)
            : base(b)
        {
            _port = b._port;
            _serverOperands = b._serverOperands;
        }

        ~IocpTcpServer()
        {
            if (IsServerStarted)
                StopServer();
        }

        public String Port
        {
            get
            {
                lock (_generalLock)
                {
                    return _port;
                }

            }
            private set
            {
                lock (_generalLock)
                {
                    _port = value;
                }
            }
        }

        public INetworkServerAcceptor Acceptor
        {
            get
            {
                lock (_generalLock)
                {
                    return _acceptor;
                }
            }
            set
            {
                lock (_generalLock)
                {
                    if (value == null)
                        throw new NullReferenceException("Acceptor cannot be null!");
                    _acceptor = value;
                }

            }
        }

        public INetworkServerCallback CallBackObj
        {
            get
            {
                lock (_generalLock)
                {
                    return _callBackObj;
                }
            }
            set
            {
                lock (_generalLock)
                {
                    if (_callBackObj != null)
                    {
                        _onServerStarted -= _callBackObj.OnServerStarted;
                        _onServerStopped -= _callBackObj.OnServerStopped;
                    }
                    _callBackObj = value;
                    if (_callBackObj != null)
                    {
                        _onServerStarted += _callBackObj.OnServerStarted;
                        _onServerStopped += _callBackObj.OnServerStopped;
                    }
                }
            }
        }

        public IRoomCallback RoomCallBackObj
        {
            get
            {
                lock (_generalLock)
                {
                    return _roomCallBackObj;
                }
            }
            set
            {
                lock (_generalLock)
                {
                    _roomCallBackObj = value;
                }
            }
        }

        public bool NoDelay
        {
            get
            {
                lock (_generalLock)
                {
                    return _noDelay;
                }
            }
            set
            {
                lock (_generalLock)
                {
                    _noDelay = value;
                }
            }
        }

        public int MaxSocketCount
        {
            get
            {
                lock (_generalLock)
                {
                    return _maxSocketCount;
                }
            }
            set
            {
                lock (_generalLock)
                {
                    _maxSocketCount = value;
                }
            }
        }

        [Serializable]
        private class CallbackException : Exception
        {
            public CallbackException()
                : base()
            {

            }

            public CallbackException(String message)
                : base(message)
            {

            }
        }

        protected override void Execute()
        {
            StartStatus status = StartStatus.FailSocketError;
            try
            {
                lock (_generalLock)
                {
                    if (IsServerStarted)
                    {
                        status = StartStatus.FailAlreadyStarted;
                        throw new CallbackException();
                    }
                    Acceptor = _serverOperands.Acceptor;
                    CallBackObj = _serverOperands.CallBackObj;
                    RoomCallBackObj = _serverOperands.RoomCallBackObj;
                    NoDelay = _serverOperands.NoDelay;
                    Port = _serverOperands.Port;
                    MaxSocketCount = _serverOperands.MaxSocketCount;

                    if (Port == null || Port.Length == 0)
                    {
                        Port = DefaultServerConfiguration.DefaultPort;
                    }
                    lock (_listLock)
                    {
                        _socketList.Clear();
                    }
                    lock (_roomLock)
                    {
                        _roomMap.Clear();
                    }

                    IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                    IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
                    if (ipAddress == null)
                    {
                        throw new Exception("No Ipv4 address found for the server");
                    }

                    int usablePort = Convert.ToInt32(_port);

                    Console.WriteLine("Starting up seadrive server on ipAddr: " + ipAddress );

                    _listener = new TcpListener(ipAddress, Convert.ToInt32(_port));
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _listener.Start();
                    _listener.BeginAcceptTcpClient(new AsyncCallback(IocpTcpServer.onAccept), this);
                }

            }
            catch (CallbackException)
            {
                OnServerStarted(this, status);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                if (_listener != null)
                    _listener.Stop();
                _listener = null;
                OnServerStarted(this, StartStatus.FailSocketError);
                return;
            }
            OnServerStarted(this, StartStatus.Success);
        }


        private static void onAccept(IAsyncResult result)
        {
            IocpTcpServer server = result.AsyncState as IocpTcpServer;
            TcpClient client = null;
            try
            {
                if (server._listener != null)
                {
                    client = server._listener.EndAcceptTcpClient(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                if (client != null)
                {
                    try
                    {
                        client.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + " >" + e.StackTrace);
                    }
                    client.Close();
                    client = null;
                }
            }

            try
            {
                if (server._listener != null)
                    server._listener.BeginAcceptTcpClient(new AsyncCallback(IocpTcpServer.onAccept), server);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                if (client != null)
                    client.Close();
                server.StopServer();
                return;
            }

            if (client != null)
            {
                IocpTcpSocket socket = new IocpTcpSocket(client, server);
                lock (server._listLock)
                {
                    if (server.MaxSocketCount != SocketCount.Infinite && server._socketList.Count > server.MaxSocketCount)
                    {
                        socket.Disconnect();
                        return;
                    }
                }
                if (server.CallBackObj == null)
                {
                    socket.Disconnect();
                    return;
                }

                if (!server.Acceptor.OnAccept(server, socket.IPInfo))
                {
                    socket.Disconnect();
                }
                else
                {
                    INetworkSocketCallback socketCallbackObj = server.Acceptor.GetSocketCallback();
                    socket.CallBackObj = socketCallbackObj;
                    socket.Start();
                    lock (server._listLock)
                    {
                        server._socketList.Add(socket);
                    }
                    server.OnServerAccepted(server, socket);
                }
            }


        }

        public void StartServer(ServerOperands operands)
        {
            if (operands == null)
                operands = ServerOperands.DefaultServerOperands;
            if (operands.Acceptor == null)
                throw new NullReferenceException("acceptor cannot be null!");
            lock (_generalLock)
            {
                _serverOperands = operands;
            }
            Start();
        }

        public void StopServer()
        {
            lock (_generalLock)
            {
                if (!IsServerStarted)
                    return;

                _listener.Stop();
                _listener = null;
            }
            ShutdownAllClient();

            OnServerStopped(this);
        }

        public bool IsServerStarted
        {
            get
            {
                lock (_generalLock)
                {
                    if (_listener != null)
                        return true;
                    return false;
                }
            }
        }

        public void ShutdownAllClient()
        {
            lock (_listLock)
            {
                List<IocpTcpSocket> socketList = GetClientSocketList();
                foreach (IocpTcpSocket socket in socketList)
                {
                    socket.Disconnect();
                }
            }
        }

        public void Broadcast(INetworkSocket sender, Packet packet)
        {
            List<IocpTcpSocket> socketList = GetClientSocketList();

            foreach (IocpTcpSocket socket in socketList)
            {
                if (socket != sender)
                    socket.Send(packet);
            }
        }

        public void Broadcast(INetworkSocket sender, byte[] data, int offset, int dataSize)
        {
            List<IocpTcpSocket> socketList = GetClientSocketList();

            foreach (IocpTcpSocket socket in socketList)
            {
                if (socket != sender)
                    socket.Send(data, offset, dataSize);
            }
        }

        public void Broadcast(INetworkSocket sender, byte[] data)
        {
            List<IocpTcpSocket> socketList = GetClientSocketList();

            foreach (IocpTcpSocket socket in socketList)
            {
                if (socket != sender)
                    socket.Send(data);
            }
        }

        public void Broadcast(Packet packet)
        {
            Broadcast(null, packet);
        }

        public void Broadcast(byte[] data, int offset, int dataSize)
        {
            Broadcast(null, data, offset, dataSize);
        }

        public void Broadcast(byte[] data)
        {
            Broadcast(null, data);
        }

        public List<IocpTcpSocket> GetClientSocketList()
        {
            lock (_listLock)
            {
                return new List<IocpTcpSocket>(_socketList);
            }
        }

        public bool DetachClient(IocpTcpSocket clientSocket)
        {
            lock (_listLock)
            {
                return _socketList.Remove(clientSocket);
            }
        }

        public IRoom GetRoom(string roomName)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                    return _roomMap[roomName];
                return null;
            }
        }

        public List<string> RoomNames
        {
            get
            {
                lock (_roomLock)
                {
                    return new List<string>(_roomMap.Keys);
                }
            }
        }

        public List<IRoom> Rooms
        {
            get
            {
                lock (_roomLock)
                {
                    return new List<IRoom>(_roomMap.Values);
                }
            }
        }

        public Room Join(INetworkSocket socket, string roomName)
        {
            lock (_roomLock)
            {
                Room curRoom = null;
                if (_roomMap.ContainsKey(roomName))
                {
                    curRoom = _roomMap[roomName];
                }
                else
                {
                    curRoom = new Room(roomName, RoomCallBackObj);
                    _roomMap[roomName] = curRoom;
                }
                curRoom.AddSocket(socket);
                return curRoom;
            }
        }

        public int Leave(INetworkSocket socket, string roomName)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    int numSocketLeft = _roomMap[roomName].DetachClient(socket);
                    if (numSocketLeft == 0)
                    {
                        _roomMap.Remove(roomName);
                    }
                    return numSocketLeft;
                }
                return 0;
            }
        }

        public void BroadcastToRoom(string roomName, Packet packet)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(packet);
                }
            }
        }

        public void BroadcastToRoom(string roomName, byte[] data, int offset, int dataSize)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(data, offset, dataSize);
                }
            }
        }

        public void BroadcastToRoom(string roomName, byte[] data)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(data);
                }
            }
        }


    }
}
