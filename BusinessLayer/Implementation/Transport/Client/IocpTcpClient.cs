using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using BusinessLayer.Concurrency.Transport;
using BusinessLayer.Entities.Transport;

namespace BusinessLayer.Implementation.Transport.Client
{
    public sealed class IocpTcpClient : TransportBaseThread, INetworkClient, IDisposable
    {
        private TcpClient _client = new TcpClient();
        private ClientOperands _clientOperands = null;
        private readonly Object _generalLock = new Object();
        private readonly Object _sendLock = new Object();
        private readonly Object _sendQueueLock = new Object();
        private readonly Queue<PacketTransporter> _mSendQueue = new Queue<PacketTransporter>();
        private INetworkClientCallback _callBackObj = null;
        private String _hostName;
        private String _port;
        private bool _noDelay;
        private int _connectionTimeOut;

        private DisposableBaseEvent _mTimeOutDisposableBaseEvent = new DisposableBaseEvent(false, System.Threading.EventResetMode.AutoReset);
        private DisposableBaseEvent _mSendDisposableBaseEvent = new DisposableBaseEvent();
        private readonly Packet _mRecvSizePacket = new Packet(null, 0, Preamble.PacketLength);
        private bool _mIsConnected = false;
        OnClientConnectedDelegate _onConnected = delegate { };
        OnClientReceivedDelegate _onReceived = delegate { };
        OnClientSentDelegate _onSent = delegate { };
        OnClientDisconnectDelegate _onDisconnect = delegate { };

        public OnClientConnectedDelegate OnConnected
        {
            get
            {
                return _onConnected;
            }
            set
            {
                if (value == null)
                {
                    _onConnected = delegate { };
                    if (CallBackObj != null)
                        _onConnected += CallBackObj.OnConnected;
                }
                else
                {
                    _onConnected = CallBackObj != null && CallBackObj.OnConnected != value ? CallBackObj.OnConnected + (value - CallBackObj.OnConnected) : value;
                }
            }
        }

        public OnClientReceivedDelegate OnReceived
        {
            get
            {
                return _onReceived;
            }
            set
            {
                if (value == null)
                {
                    _onReceived = delegate { };
                    if (CallBackObj != null)
                        _onReceived += CallBackObj.OnDataReceived;
                }
                else
                {
                    _onReceived = CallBackObj != null && CallBackObj.OnDataReceived != value ? CallBackObj.OnDataReceived + (value - CallBackObj.OnDataReceived) : value;
                }
            }
        }

        public OnClientSentDelegate OnSent
        {
            get
            {
                return _onSent;
            }
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

        public OnClientDisconnectDelegate OnDisconnect
        {
            get
            {
                return _onDisconnect;
            }
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

        public IocpTcpClient()
            : base()
        {

        }

        public IocpTcpClient(IocpTcpClient b)
            : base(b)
        {
            _clientOperands = b._clientOperands;
        }
        ~IocpTcpClient()
        {
            Dispose(false);
        }

        public String HostName
        {
            get
            {
                lock (_generalLock)
                {
                    return _hostName;
                }
            }
            private set
            {
                lock (_generalLock)
                {
                    _hostName = value;
                }
            }
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

        public bool NoDelay
        {

            get
            {
                lock (_generalLock)
                {
                    return _noDelay;
                }
            }
            private set
            {
                lock (_generalLock)
                {
                    _noDelay = value;
                }
            }
        }

        public int ConnectionTimeOut
        {
            get
            {
                lock (_generalLock)
                {
                    return _connectionTimeOut;
                }
            }
            private set
            {
                lock (_generalLock)
                {
                    _connectionTimeOut = value;
                }
            }
        }

        public INetworkClientCallback CallBackObj
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
                        _onConnected -= _callBackObj.OnConnected;
                        _onSent -= _callBackObj.OnSent;
                        _onReceived -= _callBackObj.OnDataReceived;
                        _onDisconnect -= _callBackObj.OnDisconnect;
                    }
                    _callBackObj = value;
                    if (_callBackObj != null)
                    {
                        _onConnected += _callBackObj.OnConnected;
                        _onSent += _callBackObj.OnSent;
                        _onReceived += _callBackObj.OnDataReceived;
                        _onDisconnect += _callBackObj.OnDisconnect;
                    }
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
            ConnectStatus status = ConnectStatus.Success;
            try
            {
                lock (_generalLock)
                {
                    if (IsConnectionAlive)
                    {
                        status = ConnectStatus.FailAlreadyConnected;
                        throw new CallbackException();
                    }

                    CallBackObj = _clientOperands.CallBackObj;
                    HostName = _clientOperands.HostName;
                    Port = _clientOperands.Port;
                    NoDelay = _clientOperands.NoDelay;
                    ConnectionTimeOut = _clientOperands.ConnectionTimeOut;


                    if (string.IsNullOrEmpty(HostName))
                    {
                        HostName = DefaultServerConfiguration.DefaultHostname;
                    }

                    if (string.IsNullOrEmpty(Port))
                    {
                        Port = DefaultServerConfiguration.DefaultPort;
                    }

                    _client = new TcpClient { NoDelay = NoDelay };

                    _client.Client.BeginConnect(HostName, Convert.ToInt32(Port), new AsyncCallback(IocpTcpClient.onConnected), this);
                    if (_mTimeOutDisposableBaseEvent.WaitForEvent(ConnectionTimeOut))
                    {
                        if (!_client.Connected)
                        {
                            status = ConnectStatus.FailSocketError;
                            throw new CallbackException();
                        }
                        IsConnectionAlive = true;
                        Task t = new Task(() => OnConnected(this, ConnectStatus.Success));
                        t.Start();


                    }
                    else
                    {
                        try
                        {
                            _client.Client.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        }
                        _client.Close();
                        status = ConnectStatus.FailTimeOut;
                        throw new CallbackException();
                    }
                }
            }
            catch (CallbackException)
            {
                Task t = new Task(() => OnConnected(this, status));
                t.Start();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                Task t = new Task(() => OnConnected(this, ConnectStatus.FailSocketError));
                t.Start();
                return;
            }
            startReceive();

        }

        public void Connect(ClientOperands operands)
        {
            if (operands == null)
                operands = ClientOperands.DefaultClientOperands;
            lock (_generalLock)
            {
                _clientOperands = operands;
            }
            Start();

        }

        private static void onConnected(IAsyncResult result)
        {
            IocpTcpClient tcpclient = result.AsyncState as IocpTcpClient;

            try { tcpclient._client.Client.EndConnect(result); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                tcpclient._mTimeOutDisposableBaseEvent.SetEvent();
                return;
            }
            tcpclient._mTimeOutDisposableBaseEvent.SetEvent();
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

            lock (_sendQueueLock)
            {
                _mSendQueue.Clear();
            }

            Task t = new Task(() => OnDisconnect(this));
            t.Start();

        }

        public bool IsConnectionAlive
        {
            get
            {
                lock (_generalLock)
                {
                    return _mIsConnected;
                }
            }
            private set
            {
                lock (_generalLock)
                {
                    _mIsConnected = value;
                }
            }

        }

        public void Send(Packet packet)
        {

            if (!IsConnectionAlive)
            {

                Task t = new Task(() => OnSent(this, SendStatus.FailNotConnected, packet));
                t.Start();

                return;
            }
            if (packet.PacketByteSize <= 0)
            {

                Task t = new Task(() => OnSent(this, SendStatus.FailInvalidPacket, packet));
                t.Start();
                return;
            }

            lock (_sendLock)
            {
                Packet sendSizePacket = new Packet(null, 0, Preamble.PacketLength, false);
                PacketTransporter transport = new PacketTransporter(PacketType.SIZE, sendSizePacket, 0, Preamble.PacketLength, this, packet);
                sendSizePacket.SetPacket(Preamble.ToPreamblePacket(packet.PacketByteSize), 0, Preamble.PacketLength);
                if (_mSendDisposableBaseEvent.TryLock())
                {
                    try { _client.Client.BeginSend(sendSizePacket.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, new AsyncCallback(IocpTcpClient.onSent), transport); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        OnSent(this, SendStatus.FailSocketError, packet);
                        Disconnect();
                        return;
                    }
                }
                else
                {
                    lock (_sendQueueLock)
                    {
                        _mSendQueue.Enqueue(transport);
                    }
                }
            }


        }

        public void Send(byte[] data, int offset, int dataSize)
        {
            Packet sendPacket = null;
            sendPacket = new Packet(data, offset, dataSize, false);
            Send(sendPacket);

        }

        public void Send(byte[] data)
        {
            Send(data, 0, data.Count());
        }

        private enum PacketType
        {
            SIZE = 0,
            DATA
        }

        private class PacketTransporter
        {
            public Packet Packet;
            public readonly Packet DataPacket;
            public int Offset;
            public int Size;
            public readonly IocpTcpClient IocpTcpClient;
            public PacketType PacketType;

            public PacketTransporter(PacketType packetType, Packet packet, int offset, int size, IocpTcpClient iocpTcpClient, Packet dataPacket = null)
            {
                PacketType = packetType;
                Packet = packet;
                Offset = offset;
                Size = size;
                IocpTcpClient = iocpTcpClient;
                DataPacket = dataPacket;
            }
        }

        private void startReceive()
        {
            PacketTransporter transport = new PacketTransporter(PacketType.SIZE, _mRecvSizePacket, 0, Preamble.PacketLength, this);
            try { _client.Client.BeginReceive(_mRecvSizePacket.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, new AsyncCallback(IocpTcpClient.onReceived), transport); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                Disconnect(); return;
            }

        }

        private static void onReceived(IAsyncResult result)
        {
            PacketTransporter transport = result.AsyncState as PacketTransporter;
            Socket socket = transport.IocpTcpClient._client.Client;

            int readSize = 0;
            try { readSize = socket.EndReceive(result); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                transport.IocpTcpClient.Disconnect(); return;
            }
            if (readSize == 0)
            {
                transport.IocpTcpClient.Disconnect();
                return;
            }
            if (readSize < transport.Size)
            {
                transport.Offset = transport.Offset + readSize;
                transport.Size = transport.Size - readSize;
                try { socket.BeginReceive(transport.Packet.PacketRaw, transport.Offset, transport.Size, SocketFlags.None, new AsyncCallback(IocpTcpClient.onReceived), transport); }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                    transport.IocpTcpClient.Disconnect(); return;
                }
            }
            else
            {
                if (transport.PacketType == PacketType.SIZE)
                {
                    int shouldReceive = Preamble.ToShouldReceive(transport.Packet.PacketRaw);
                    if (shouldReceive < 0)
                    {
                        int preambleOffset = Preamble.CheckPreamble(transport.Packet.PacketRaw);
                        transport.Size = preambleOffset;
                        try
                        {
                            Buffer.BlockCopy(transport.Packet.PacketRaw, preambleOffset, transport.Packet.PacketRaw, 0, transport.Packet.PacketByteSize - preambleOffset);
                            socket.BeginReceive(transport.Packet.PacketRaw, transport.Offset, transport.Size, SocketFlags.None, new AsyncCallback(IocpTcpClient.onReceived), transport);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                            transport.IocpTcpClient.Disconnect(); return;
                        }
                        return;
                    }
                    Packet recvPacket = new Packet(null, 0, shouldReceive);
                    PacketTransporter dataTransport = new PacketTransporter(PacketType.DATA, recvPacket, 0, shouldReceive, transport.IocpTcpClient);
                    try { socket.BeginReceive(recvPacket.PacketRaw, 0, shouldReceive, SocketFlags.None, new AsyncCallback(IocpTcpClient.onReceived), dataTransport); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.IocpTcpClient.Disconnect(); return;
                    }
                }
                else
                {
                    PacketTransporter sizeTransport = new PacketTransporter(PacketType.SIZE, transport.IocpTcpClient._mRecvSizePacket, 0, Preamble.PacketLength, transport.IocpTcpClient);
                    try { socket.BeginReceive(sizeTransport.Packet.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, new AsyncCallback(IocpTcpClient.onReceived), sizeTransport); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.IocpTcpClient.Disconnect(); return;
                    }
                    transport.IocpTcpClient.OnReceived(transport.IocpTcpClient, transport.Packet);
                }
            }
        }

        private static void onSent(IAsyncResult result)
        {
            PacketTransporter transport = result.AsyncState as PacketTransporter;
            Socket socket = transport.IocpTcpClient._client.Client;

            int sentSize = 0;
            try { sentSize = socket.EndSend(result); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                transport.IocpTcpClient.Disconnect();
                transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.FailSocketError, transport.DataPacket);
                return;
            }
            if (sentSize == 0)
            {
                transport.IocpTcpClient.Disconnect();
                transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.FailConnectionClosing, transport.DataPacket);
                return;
            }
            if (sentSize < transport.Size)
            {
                transport.Offset = transport.Offset + sentSize;
                transport.Size = transport.Size - sentSize;
                try { socket.BeginSend(transport.Packet.PacketRaw, transport.Offset, transport.Size, SocketFlags.None, new AsyncCallback(IocpTcpClient.onSent), transport); }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                    transport.IocpTcpClient.Disconnect();
                    transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.FailSocketError, transport.DataPacket);
                    return;
                }
            }
            else
            {
                if (transport.PacketType == PacketType.SIZE)
                {
                    transport.Packet = transport.DataPacket;
                    transport.Offset = transport.DataPacket.PacketOffset; ;
                    transport.PacketType = PacketType.DATA;
                    transport.Size = transport.DataPacket.PacketByteSize;
                    try { socket.BeginSend(transport.Packet.PacketRaw, transport.Offset, transport.Size, SocketFlags.None, new AsyncCallback(IocpTcpClient.onSent), transport); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                        transport.IocpTcpClient.Disconnect();
                        transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.FailSocketError, transport.DataPacket);
                        return;
                    }
                }
                else
                {
                    PacketTransporter delayedTransport = null;
                    lock (transport.IocpTcpClient._sendQueueLock)
                    {
                        Queue<PacketTransporter> sendQueue = transport.IocpTcpClient._mSendQueue;
                        if (sendQueue.Count > 0)
                        {
                            delayedTransport = sendQueue.Dequeue();
                        }
                    }
                    if (delayedTransport != null)
                    {
                        try { socket.BeginSend(delayedTransport.Packet.PacketRaw, 0, Preamble.PacketLength, SocketFlags.None, new AsyncCallback(IocpTcpClient.onSent), delayedTransport); }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >" + ex.StackTrace);
                            transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.Success, transport.DataPacket);
                            delayedTransport.IocpTcpClient.Disconnect();
                            delayedTransport.IocpTcpClient.OnSent(delayedTransport.IocpTcpClient, SendStatus.FailSocketError, delayedTransport.DataPacket);
                            return;
                        }
                    }
                    else
                    {
                        transport.IocpTcpClient._mSendDisposableBaseEvent.Unlock();
                    }
                    transport.IocpTcpClient.OnSent(transport.IocpTcpClient, SendStatus.Success, transport.DataPacket);
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
                if (!this.IsDisposed)
                {
                    if (IsConnectionAlive)
                        Disconnect();
                    if (isDisposing)
                    {
                        if (_client != null)
                        {
                            _client.Close();
                            _client = null;
                        }
                        if (_mTimeOutDisposableBaseEvent != null)
                        {
                            _mTimeOutDisposableBaseEvent.Dispose();
                            _mTimeOutDisposableBaseEvent = null;
                        }
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
                this.IsDisposed = true;
            }
        }

    }
}
