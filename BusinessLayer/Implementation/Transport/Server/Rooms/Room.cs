using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLayer.Entities.Transport;

namespace BusinessLayer.Implementation.Transport.Server.Rooms
{
    public sealed class Room : IRoom
    {
        /// <summary>
        /// socket list
        /// </summary>
        private readonly HashSet<INetworkSocket> _mSocketList = new HashSet<INetworkSocket>();

        /// <summary>
        /// name of the room
        /// </summary>
        private string _mRoomName;

        /// <summary>
        /// general lock
        /// </summary>
        private readonly Object _mGeneralLock = new Object();

        /// <summary>
        /// list lock
        /// </summary>
        private readonly Object _mListLock = new Object();

        /// <summary>
        /// callback object
        /// </summary>
        private IRoomCallback _mCallBackObj;

        /// <summary>
        /// OnCreated event
        /// </summary>
        OnRoomCreatedDelegate m_onCreated = delegate { };
        /// <summary>
        /// OnJoin event
        /// </summary>
        OnRoomJoinDelegate m_onJoin = delegate { };
        /// <summary>
        /// OnLeave event
        /// </summary>
        OnRoomLeaveDelegate m_onLeave = delegate { };
        /// <summary>
        /// OnBroadcast event
        /// </summary>
        OnRoomBroadcastDelegate m_onBroadcast = delegate { };
        /// <summary>
        /// OnDestroy event
        /// </summary>
        OnRoomDestroyDelegate m_onDestroy = delegate { };

        /// <summary>
        /// OnCreated event
        /// </summary>
        public OnRoomCreatedDelegate OnCreated
        {
            get
            {
                return m_onCreated;
            }
            set
            {
                if (value == null)
                {
                    m_onCreated = delegate { };
                    if (CallBackObj != null)
                        m_onCreated += CallBackObj.OnCreated;
                }
                else
                {
                    m_onCreated = CallBackObj != null && CallBackObj.OnCreated != value ? CallBackObj.OnCreated + (value - CallBackObj.OnCreated) : value;
                }
            }
        }
        /// <summary>
        /// OnJoin event
        /// </summary>
        public OnRoomJoinDelegate OnJoin
        {
            get
            {
                return m_onJoin;
            }
            set
            {
                if (value == null)
                {
                    m_onJoin = delegate { };
                    if (CallBackObj != null)
                        m_onJoin += CallBackObj.OnJoin;
                }
                else
                {
                    m_onJoin = CallBackObj != null && CallBackObj.OnJoin != value ? CallBackObj.OnJoin + (value - CallBackObj.OnJoin) : value;
                }
            }
        }
        /// <summary>
        /// OnLeave event
        /// </summary>
        public OnRoomLeaveDelegate OnLeave
        {
            get
            {
                return m_onLeave;
            }
            set
            {
                if (value == null)
                {
                    m_onLeave = delegate { };
                    if (CallBackObj != null)
                        m_onLeave += CallBackObj.OnLeave;
                }
                else
                {
                    m_onLeave = CallBackObj != null && CallBackObj.OnLeave != value ? CallBackObj.OnLeave + (value - CallBackObj.OnLeave) : value;
                }
            }
        }
        /// <summary>
        /// OnBroadcast event
        /// </summary>
        public OnRoomBroadcastDelegate OnBroadcast
        {
            get
            {
                return m_onBroadcast;
            }
            set
            {
                if (value == null)
                {
                    m_onBroadcast = delegate { };
                    if (CallBackObj != null)
                        m_onBroadcast += CallBackObj.OnBroadcast;
                }
                else
                {
                    m_onBroadcast = CallBackObj != null && CallBackObj.OnBroadcast != value ? CallBackObj.OnBroadcast + (value - CallBackObj.OnBroadcast) : value;
                }
            }
        }
        /// <summary>
        /// OnDestroy event
        /// </summary>
        public OnRoomDestroyDelegate OnDestroy
        {
            get
            {
                return m_onDestroy;
            }
            set
            {
                if (value == null)
                {
                    m_onDestroy = delegate { };
                    if (CallBackObj != null)
                        m_onDestroy += CallBackObj.OnDestroy;
                }
                else
                {
                    m_onDestroy = CallBackObj != null && CallBackObj.OnDestroy != value ? CallBackObj.OnDestroy + (value - CallBackObj.OnDestroy) : value;
                }
            }
        }

        /// <summary>
        /// Callback Object property
        /// </summary>
        public IRoomCallback CallBackObj
        {
            get
            {
                lock (_mGeneralLock)
                {
                    return _mCallBackObj;
                }
            }
            set
            {
                lock (_mGeneralLock)
                {
                    if (_mCallBackObj != null)
                    {
                        m_onCreated -= _mCallBackObj.OnCreated;
                        m_onJoin -= _mCallBackObj.OnJoin;
                        m_onLeave -= _mCallBackObj.OnLeave;
                        m_onBroadcast -= _mCallBackObj.OnBroadcast;
                        m_onDestroy -= _mCallBackObj.OnDestroy;
                    }
                    _mCallBackObj = value;
                    if (_mCallBackObj != null)
                    {
                        m_onCreated += _mCallBackObj.OnCreated;
                        m_onJoin += _mCallBackObj.OnJoin;
                        m_onLeave += _mCallBackObj.OnLeave;
                        m_onBroadcast += _mCallBackObj.OnBroadcast;
                        m_onDestroy += _mCallBackObj.OnDestroy;
                    }
                }
            }
        }


        /// <summary>
        /// Room name property
        /// </summary>
        public string RoomName
        {
            get
            {
                lock (_mGeneralLock)
                {
                    return _mRoomName;
                }
            }
            private set
            {
                lock (_mGeneralLock)
                {
                    _mRoomName = value;
                }
            }
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="roomName">name of the room</param>
        /// <param name="callbackObj">callback Obj</param>
        public Room(string roomName, IRoomCallback callbackObj = null)
        {
            RoomName = roomName;
            CallBackObj = callbackObj;
            Task t = new Task(delegate()
            {
                OnCreated(this);
            });
            t.Start();

        }

        ~Room()
        {
            Task t = new Task(delegate()
            {
                OnDestroy(this);
            });
            t.Start();
        }

        public void AddSocket(INetworkSocket socket)
        {
            lock (_mListLock)
            {
                _mSocketList.Add(socket);
            }

            Task t = new Task(delegate()
            {
                OnJoin(this, socket);
            });
            t.Start();

        }

        /// <summary>
        /// Return the client socket list
        /// </summary>
        /// <returns>the client socket list</returns>
        public List<INetworkSocket> GetSocketList()
        {
            lock (_mListLock)
            {
                return new List<INetworkSocket>(_mSocketList);
            }
        }

        /// <summary>
        /// Detach the given client from the server management
        /// </summary>
        /// <param name="clientSocket">the client to detach</param>
        /// <returns>the number of socket in the room</returns>
        public int DetachClient(INetworkSocket socket)
        {
            lock (_mListLock)
            {
                _mSocketList.Remove(socket);

                Task t = new Task(delegate()
                {
                    OnLeave(this, socket);
                });
                t.Start();

                return _mSocketList.Count;
            }
        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="sender">sender of the broadcast</param>
        /// <param name="packet">packet to broadcast</param>
        public void Broadcast(INetworkSocket sender, Packet packet)
        {
            List<INetworkSocket> list = GetSocketList();
            foreach (INetworkSocket socket in list)
            {
                if (socket != sender)
                    socket.Send(packet);
            }

            Task t = new Task(delegate()
            {
                OnBroadcast(this, sender, packet);
            });
            t.Start();

        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="sender">sender of the broadcast</param>
        /// <param name="data">data in byte array</param>
        /// <param name="offset">offset in bytes</param>
        /// <param name="dataSize">data size in bytes</param>
        public void Broadcast(INetworkSocket sender, byte[] data, int offset, int dataSize)
        {
            Broadcast(sender, new Packet(data, offset, dataSize, false));
        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="sender">sender of the broadcast</param>
        /// <param name="data">data in byte array</param>
        public void Broadcast(INetworkSocket sender, byte[] data)
        {
            Broadcast(sender, new Packet(data, 0, data.Count(), false));
        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="packet">packet to broadcast</param>
        public void Broadcast(Packet packet)
        {
            Broadcast(null, packet);
        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="data">data in byte array</param>
        /// <param name="offset">offset in bytes</param>
        /// <param name="dataSize">data size in bytes</param>
        public void Broadcast(byte[] data, int offset, int dataSize)
        {
            Broadcast(null, new Packet(data, offset, dataSize, false));
        }

        /// <summary>
        /// Broadcast the given packet to all the client, connected
        /// </summary>
        /// <param name="data">data in byte array</param>
        public void Broadcast(byte[] data)
        {
            Broadcast(null, new Packet(data, 0, data.Count(), false));
        }


    }
}
