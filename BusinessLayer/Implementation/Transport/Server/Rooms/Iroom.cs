using System.Collections.Generic;
using BusinessLayer.Entities.Transport;

namespace BusinessLayer.Implementation.Transport.Server.Rooms
{
    public interface IRoom
    {
        string RoomName
        {
            get;
        }

        IRoomCallback CallBackObj
        {
            get;
            set;
        }

        List<INetworkSocket> GetSocketList();
        void Broadcast(Packet packet);
        void Broadcast(byte[] data, int offset, int dataSize);
        void Broadcast(byte[] data);

        OnRoomCreatedDelegate OnCreated
        {
            get;
            set;
        }

        OnRoomJoinDelegate OnJoin
        {
            get;
            set;
        }

        OnRoomLeaveDelegate OnLeave
        {
            get;
            set;
        }

        OnRoomBroadcastDelegate OnBroadcast
        {
            get;
            set;
        }

        OnRoomDestroyDelegate OnDestroy
        {
            get;
            set;
        }

    }

    public delegate void OnRoomCreatedDelegate(IRoom room);
    public delegate void OnRoomJoinDelegate(IRoom room, INetworkSocket socket);
    public delegate void OnRoomLeaveDelegate(IRoom room, INetworkSocket socket);
    public delegate void OnRoomBroadcastDelegate(IRoom room, INetworkSocket sender, Packet packet);
    public delegate void OnRoomDestroyDelegate(IRoom room);

    public interface IRoomCallback
    {
        void OnCreated(IRoom room);
        void OnJoin(IRoom room, INetworkSocket socket);
        void OnLeave(IRoom room, INetworkSocket socket);
        void OnBroadcast(IRoom room, INetworkSocket sender, Packet packet);
        void OnDestroy(IRoom room);
    }
}
