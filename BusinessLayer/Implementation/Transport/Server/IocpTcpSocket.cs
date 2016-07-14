using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BusinessLayer.Concurrency.Transport;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Implementation.Transport.Server.Rooms;

namespace BusinessLayer.Implementation.Transport.Server
{
    public sealed class IocpTcpSocket : TransportBaseThread, INetworkSocket, IDisposable
    {
        private readonly TcpClient _client;
        private readonly INetworkServer _server;
        private readonly IPInfo _ipInfo;
        private readonly Object _generalLock = new Object();
        private readonly Object _sendLock = new Object();
        private readonly Object _sendQueueLock = new Object();
        private readonly Object _roomLock = new Object();
        private readonly Queue<PacketTransporter> _sendQueue = new Queue<PacketTransporter>();
        private INetworkSocketCallback _callBackObj;
        private DisposableBaseEvent _mSendDisposableBaseEvent = new DisposableBaseEvent();
        private readonly Packet _recvSizePacket = new Packet(null, 0, Preamble.PacketLength);
        private bool _isConnected;
        private bool _noDelay = true;

        private OnSocketNewConnectionDelegate _onNewConnection = delegate { };
        private OnSocketReceivedDelegate _onReceived = delegate { };
        private OnSocketSentDelegate _onSent = delegate { };
        private OnSocketDisconnectDelegate _onDisconnect = delegate { };


        public OnSocketNewConnectionDelegate OnNewConnection
        {
            get { return _onNewConnection; }
            set
            {
                if (value == null)
                {
                    _onNewConnection = delegate { };
                    if (CallBackObj != null)
                        _onNewConnection += CallBackObj.OnNewConnection;
                }
                else
                {
                    _onNewConnection = CallBackObj != null && CallBackObj.OnNewConnection != value ? CallBackObj.OnNewConnection + (value - CallBackObj.OnNewConnection) : value;
                }
            }
        }


        public OnSocketReceivedDelegate OnReceived
        {
            get { return _onReceived; }
            set
            {
                if (value == null)
                {
                    _onReceived = delegate { };
                    if (CallBackObj != null)
                        _onReceived += CallBackObj.OnReceived;
                }
                else
                {
                    _onReceived = CallBackObj != null && CallBackObj.OnReceived != value ? CallBackObj.OnReceived + (value - CallBackObj.OnReceived) : value;
                }
            }
        }

        public OnSocketSentDelegate OnSent
        {
            get { return _onSent; }
            set
            {
                if (value == null)
                {
                    _onSent = delegate { };
                    if (CallBackObj != null)
                        _onSent += CallBackObj.OnSent;
                }
                else
                {
                    _onSent = CallBackObj != null && CallBackObj.OnSent != value ? CallBackObj.OnSent + (value - CallBackObj.OnSent) : value;
                }
            }
        }

        public OnSocketDisconnectDelegate OnDisconnect
        {
            get { return _onDisconnect; }
            set
            {
                if (value == null)
                {
                    _onDisconnect = delegate { };
                    if (CallBackObj != null)
                        _onDisconnect += CallBackObj.OnDisconnect;
                }
                else
                {
                    _onDisconnect = CallBackObj != null && CallBackObj.OnDisconnect != value ? CallBackObj.OnDisconnect + (value - CallBackObj.OnDisconnect) : value;
                }
            }
        }

        private readonly Dictionary<string, Room> _roomMap = new Dictionary<string, Room>();
        public IocpTcpSocket(TcpClient client, INetworkServer server)
        {
            _client = client;
            _server = server;
            NoDelay = server.NoDelay;
            var remoteIpEndPoint = _client.Client.RemoteEndPoint as IPEndPoint;
            var localIpEndPoint = _client.Client.LocalEndPoint as IPEndPoint;
            if (remoteIpEndPoint != null)
            {
                var socketHostName = remoteIpEndPoint.Address.ToString();
                _ipInfo = new IPInfo(socketHostName, remoteIpEndPoint, IPEndPointType.REMOTE);
            }
            else if (localIpEndPoint != null)
            {
                var socketHostName = localIpEndPoint.Address.ToString();
                _ipInfo = new IPInfo(socketHostName, localIpEndPoint, IPEndPointType.LOCAL);
            }
        }

        ~IocpTcpSocket()
        {
            Dispose(false);
        }

        public IPInfo IPInfo
        {
            get
            {
                lock (_generalLock)
                {
                    return _ipInfo;
                }
            }
        }

        public INetworkServer Server
        {
            get
            {
                lock (_generalLock)
                {
                    return _server;
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
                    _client.NoDelay = _noDelay;
                }
            }
        }

        public INetworkSocketCallback CallBackObj
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
                        _onNewConnection -= _callBackObj.OnNewConnection;
                        _onSent -= _callBackObj.OnSent;
                        _onReceived -= _callBackObj.OnReceived;
                        _onDisconnect -= _callBackObj.OnDisconnect;
                    }
                    _callBackObj = value;
                    if (_callBackObj != null)
                    {
                        _onNewConnection += _callBackObj.OnNewConnection;
                        _onSent += _callBackObj.OnSent;
                        _onReceived += _callBackObj.OnReceived;
                        _onDisconnect += _callBackObj.OnDisconnect;
                    }
                }
            }
        }

        protected override void Execute()
        {
            IsConnectionAlive = true;
            startReceive();
            OnNewConnection(this);
        }

        public void Disconnect()
        {
            lock (_generalLock)
            {
                if (!IsConnectionAlive)
                    return;
                try
                {
                    _client.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                }
                _client.Close();
                IsConnectionAlive = false;
            }
            _server.DetachClient(this);

            var roomList = Rooms;
            foreach (var room in roomList)
            {
                ((IocpTcpServer) _server).Leave(this, room.RoomName);
            }
            lock (_roomLock)
            {
                _roomMap.Clear();
            }

            lock (_sendQueueLock)
            {
                _sendQueue.Clear();
            }
            var t = new Task(() => OnDisconnect(this));
            t.Start();
        }


        public bool IsConnectionAlive
        {
            get
            {
                lock (_generalLock)
                {
                    return _isConnected;
                }
            }
            private set
            {
                lock (_generalLock)
                {
                    _isConnected = value;
                }
            }
        }


        public void Send(Packet packet)
        {
            if (!IsConnectionAlive)
            {
                var t = new Task(() => OnSent(this, SendStatus.FailNotConnected, packet));
                t.Start();

                return;
            }
            if (packet.PacketByteSize <= 0)
            {
                var t = new Task(() => OnSent(this, SendStatus.FailInvalidPacket, packet));
                t.Start();

                return;
            }

            lock (_sendLock)
            {
                var sendSizePacket = new Packet(null, 0, Preamble.PacketLength, false);
                var transport = new PacketTransporter(PacketType.SIZE, sendSizePacket, 0, Preamble.PacketLength, this, packet);


                sendSizePacket.SetPacket(Preamble.ToPreamblePacket(packet.PacketByteSize), 0, Preamble.PacketLength);
                if (_mSendDisposableBaseEvent.TryLock())
                {
                    try
                    {
                        _client.Client.BeginSend(sendSizePacket.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, onSent, transport);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        Disconnect();
                    }
                }
                else
                {
                    lock (_sendQueueLock)
                    {
                        _sendQueue.Enqueue(transport);
                    }
                }
            }
        }


        public void Send(byte[] data, int offset, int dataSize)
        {
            var sendPacket = new Packet(data, offset, dataSize, false);


            Send(sendPacket);
        }


        public void Send(byte[] data)
        {
            Send(data, 0, data.Count());
        }


        public void Broadcast(Packet packet)
        {
            ((IocpTcpServer) Server).Broadcast(this, packet);
        }


        public void Broadcast(byte[] data, int offset, int dataSize)
        {
            ((IocpTcpServer) Server).Broadcast(this, data, offset, dataSize);
        }


        public void Broadcast(byte[] data)
        {
            ((IocpTcpServer) Server).Broadcast(this, data);
        }


        private enum PacketType
        {
            SIZE = 0,


            DATA
        }


        private class PacketTransporter
        {
            public Packet m_packet;


            public readonly Packet m_dataPacket;


            public int m_offset;


            public int m_size;


            public readonly IocpTcpSocket m_iocpTcpClient;


            public PacketType m_packetType;


            public PacketTransporter(PacketType packetType, Packet packet, int offset, int size, IocpTcpSocket iocpTcpClient, Packet dataPacket = null)
            {
                m_packetType = packetType;
                m_packet = packet;
                m_offset = offset;
                m_size = size;
                m_iocpTcpClient = iocpTcpClient;
                m_dataPacket = dataPacket;
            }
        }


        private void startReceive()
        {
            var transport = new PacketTransporter(PacketType.SIZE, _recvSizePacket, 0, Preamble.PacketLength, this);
            try
            {
                _client.Client.BeginReceive(_recvSizePacket.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, onReceived, transport);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                Disconnect();
            }
        }


        private static void onReceived(IAsyncResult result)
        {
            var transport = result.AsyncState as PacketTransporter;
            var socket = transport.m_iocpTcpClient._client.Client;

            var readSize = 0;
            try
            {
                if (socket != null)
                    readSize = socket.EndReceive(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                transport.m_iocpTcpClient.Disconnect();
                return;
            }
            if (readSize == 0)
            {
                transport.m_iocpTcpClient.Disconnect();
                return;
            }
            if (readSize < transport.m_size)
            {
                transport.m_offset = transport.m_offset + readSize;
                transport.m_size = transport.m_size - readSize;
                try
                {
                    socket.BeginReceive(transport.m_packet.PacketRaw, transport.m_offset, transport.m_size, SocketFlags.None, onReceived, transport);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                    transport.m_iocpTcpClient.Disconnect();
                }
            }
            else
            {
                if (transport.m_packetType == PacketType.SIZE)
                {
                    var shouldReceive = Preamble.ToShouldReceive(transport.m_packet.PacketRaw);


                    if (shouldReceive < 0)
                    {
                        var preambleOffset = Preamble.CheckPreamble(transport.m_packet.PacketRaw);

                        transport.m_offset = transport.m_packet.PacketByteSize - preambleOffset;

                        transport.m_size = preambleOffset;
                        try
                        {
                            Buffer.BlockCopy(transport.m_packet.PacketRaw, preambleOffset, transport.m_packet.PacketRaw, 0, transport.m_packet.PacketByteSize - preambleOffset);

                            socket.BeginReceive(transport.m_packet.PacketRaw, transport.m_offset, transport.m_size, SocketFlags.None, onReceived, transport);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                            transport.m_iocpTcpClient.Disconnect();
                            return;
                        }
                        return;
                    }
                    var recvPacket = new Packet(null, 0, shouldReceive);
                    var dataTransport = new PacketTransporter(PacketType.DATA, recvPacket, 0, shouldReceive, transport.m_iocpTcpClient);
                    try
                    {
                        socket.BeginReceive(recvPacket.PacketRaw, 0, shouldReceive, SocketFlags.None, onReceived, dataTransport);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.m_iocpTcpClient.Disconnect();
                    }
                }
                else
                {
                    var sizeTransport = new PacketTransporter(PacketType.SIZE, transport.m_iocpTcpClient._recvSizePacket, 0, Preamble.PacketLength, transport.m_iocpTcpClient);
                    try
                    {
                        socket.BeginReceive(sizeTransport.m_packet.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, onReceived, sizeTransport);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.m_iocpTcpClient.Disconnect();
                        return;
                    }
                    transport.m_iocpTcpClient.OnReceived(transport.m_iocpTcpClient, transport.m_packet);
                }
            }
        }


        private static void onSent(IAsyncResult result)
        {
            var transport = result.AsyncState as PacketTransporter;
            var socket = transport.m_iocpTcpClient._client.Client;

            var sentSize = 0;
            try
            {
                sentSize = socket.EndSend(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                transport.m_iocpTcpClient.Disconnect();
                transport.m_iocpTcpClient.OnSent(transport.m_iocpTcpClient, SendStatus.FailSocketError, transport.m_dataPacket);
                return;
            }
            if (sentSize == 0)
            {
                transport.m_iocpTcpClient.Disconnect();
                transport.m_iocpTcpClient.OnSent(transport.m_iocpTcpClient, SendStatus.FailConnectionClosing, transport.m_dataPacket);
                return;
            }
            if (sentSize < transport.m_size)
            {
                transport.m_offset = transport.m_offset + sentSize;
                transport.m_size = transport.m_size - sentSize;
                try
                {
                    socket.BeginSend(transport.m_packet.PacketRaw, transport.m_offset, transport.m_size, SocketFlags.None, onSent, transport);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                    transport.m_iocpTcpClient.Disconnect();
                    transport.m_iocpTcpClient.OnSent(transport.m_iocpTcpClient, SendStatus.FailSocketError, transport.m_dataPacket);
                }
            }
            else
            {
                if (transport.m_packetType == PacketType.SIZE)
                {
                    transport.m_packet = transport.m_dataPacket;
                    transport.m_offset = transport.m_dataPacket.PacketOffset;
                    transport.m_packetType = PacketType.DATA;
                    transport.m_size = transport.m_dataPacket.PacketByteSize;
                    try
                    {
                        socket.BeginSend(transport.m_packet.PacketRaw, transport.m_offset, transport.m_size, SocketFlags.None, onSent, transport);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.m_iocpTcpClient.Disconnect();
                        transport.m_iocpTcpClient.OnSent(transport.m_iocpTcpClient, SendStatus.FailSocketError, transport.m_dataPacket);
                    }
                }
                else
                {
                    PacketTransporter delayedTransport = null;
                    lock (transport.m_iocpTcpClient._sendQueueLock)
                    {
                        var sendQueue = transport.m_iocpTcpClient._sendQueue;
                        if (sendQueue.Count > 0)
                        {
                            delayedTransport = sendQueue.Dequeue();
                        }
                    }
                    if (delayedTransport != null)
                    {
                        try
                        {
                            socket.BeginSend(delayedTransport.m_packet.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, onSent, delayedTransport);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                            delayedTransport.m_iocpTcpClient.Disconnect();
                            delayedTransport.m_iocpTcpClient.OnSent(delayedTransport.m_iocpTcpClient, SendStatus.FailSocketError, delayedTransport.m_dataPacket);
                            return;
                        }
                    }
                    else
                    {
                        transport.m_iocpTcpClient._mSendDisposableBaseEvent.Unlock();
                    }
                    transport.m_iocpTcpClient.OnSent(transport.m_iocpTcpClient, SendStatus.Success, transport.m_dataPacket);
                }
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


        public void Join(string roomName)
        {
            lock (_roomLock)
            {
                var curRoom = ((IocpTcpServer) _server).Join(this, roomName);
                if (!_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName] = curRoom;
                }
            }
        }


        public void Leave(string roomName)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    var numSocketLeft = ((IocpTcpServer) _server).Leave(this, roomName);
                    if (numSocketLeft == 0)
                    {
                        _roomMap.Remove(roomName);
                    }
                }
            }
        }


        public void BroadcastToRoom(string roomName, Packet packet)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(this, packet);
                }
            }
        }


        public void BroadcastToRoom(string roomName, byte[] data, int offset, int dataSize)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(this, data, offset, dataSize);
                }
            }
        }


        public void BroadcastToRoom(string roomName, byte[] data)
        {
            lock (_roomLock)
            {
                if (_roomMap.ContainsKey(roomName))
                {
                    _roomMap[roomName].Broadcast(this, data);
                }
            }
        }


        private bool IsDisposed { get; set; }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }


        private void Dispose(bool isDisposing)
        {
            try
            {
                if (!IsDisposed)
                {
                    if (IsConnectionAlive)
                        Disconnect();
                    if (isDisposing)
                    {
                        if (_mSendDisposableBaseEvent != null)
                        {
                            _mSendDisposableBaseEvent.Dispose();
                            _mSendDisposableBaseEvent = null;
                        }
                    }
                }
            }
            finally
            {
                IsDisposed = true;
            }
        }
    }
}
