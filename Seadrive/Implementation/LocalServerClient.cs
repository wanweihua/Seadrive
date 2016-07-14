using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using BusinessLayer.Entities.IoCpPackets;
using BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Implementation.Chunking;
using BusinessLayer.Implementation.Transport.Client;
using Data_Abstraction_Layer.Deduplication;

namespace Seadrive.Implementation
{
    public class Client : INetworkClientCallback
    {
        private readonly INetworkClient _client = new IocpTcpClient();
        private UnitOfWork _unitOfWork = new UnitOfWork();
        private List<DirtyFile> _dirtyFiles;
        private readonly string _localServerUuid;
        private Dictionary<string, byte[]> _filesMissingAtRemoteServer; 
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);

        public Client(string localServerUuid)
        {
            _localServerUuid = localServerUuid;
        }

        public void InitiateConnection(List<DirtyFile> dirtyFiles)
        {
            Debug.WriteLine("Inside Initiate Connection");
            _dirtyFiles = dirtyFiles;
            if (!(_dirtyFiles != null && _dirtyFiles.Count != 0))
            {
                return;
            }

            List<IocpInitialCommunicationPacketStruct> packetStructs = dirtyFiles.Select(file => new IocpInitialCommunicationPacketStruct(file.Guid, file.FileChecksum)).ToList();
            IocpInitialCommunicationPacket initialPacket = new IocpInitialCommunicationPacket {ClientUuid = _localServerUuid, InitialData = packetStructs};
            PacketSerializer<IocpInitialCommunicationPacket> serializer = new PacketSerializer<IocpInitialCommunicationPacket>(initialPacket);

            byte[] message = serializer.PacketRaw;
            IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (IocpTransportFlags.ClientToServer | IocpTransportFlags.InitialPacket));
            Debug.WriteLine("flags: " + ((IocpTransportFlags.ClientToServer | IocpTransportFlags.InitialPacket)));
            PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);

            byte[] packetData = finalSerializer.PacketRaw;
            Packet packet = new Packet(packetData, 0, packetData.Length, false);
            Debug.WriteLine("Pre send!");
            _client.Send(packet);
            Debug.WriteLine("Send initial packet, calculate diffs");
        }

        public void StartRetransmission()
        {
            if (_dirtyFiles.Count <= 0) return;
            IocpRemoteRetransmissionPacket retransmissionPacket = new IocpRemoteRetransmissionPacket(_localServerUuid);
            Packet packet = EncodeIocpRemoteFileRetransmissionPacket(retransmissionPacket);
            _client.Send(packet);
        }

        //Removed for simplicity
        public void SendMergeMessage()
        {

        }

        private static IocpTransportPacket CreateIocpTransportMessage(byte[] data, IocpTransportFlags flags)
        {
            return new IocpTransportPacket(flags, data.Length, data);
        }

        public void Connect()
        {
            //string hostname = ServerConfiguration.DefaultHostname;
            string hostname = "10.218.113.67"; //SINTEF SERVER
            //string hostname = "192.168.5.37"; //Satelite service provider
            string port = "8050";
            ClientOperands operands = new ClientOperands(this, hostname, port);
            _client.Connect(operands);
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }

        public void OnConnected(INetworkClient client, ConnectStatus status)
        {
            Debug.WriteLine("Connected to server");
        }

        public void OnDataReceived(INetworkClient client, Packet receivedPacket)
        {
            PacketSerializer<IocpTransportPacket> transportMessage = new PacketSerializer<IocpTransportPacket>(receivedPacket.PacketRaw, 0
                , receivedPacket.PacketByteSize);
            IocpTransportPacket cpMessage = transportMessage.ClonePacketObj();
            IocpTransportFlags flag = cpMessage.GetMessageType();

            Debug.WriteLine("Recieved data from server: ");            
            if (flag.HasFlag(IocpTransportFlags.InitialPacketResponse)) //Respond to the initial response by uploading all files requested
            {
                PacketSerializer<IocpRemoteProtocolIntialResponsePacket> responsePacketSerializer = new PacketSerializer<IocpRemoteProtocolIntialResponsePacket>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());

                //Send missing files
                IocpRemoteProtocolIntialResponsePacket responsePacket = responsePacketSerializer.ClonePacketObj();
                IocpRemoteFileUploadMetadataPacket metadataPacket;
                Dictionary<string, byte[]> guidAndKnownVersions = responsePacket.GuidAndKnownVersions;
                _filesMissingAtRemoteServer = guidAndKnownVersions;
                //Purge dirty files
                List<int> indexesToPurge = new List<int>();
                for (int i = _dirtyFiles.Count - 1; i >= 0; i--)
                {
                    if (!(_filesMissingAtRemoteServer.ContainsKey(_dirtyFiles[i].Guid)))
                    {
                        indexesToPurge.Add(i);
                    }
                }

                foreach (var index in indexesToPurge)
                {
                    _dirtyFiles.RemoveAt(index);
                }
                Console.WriteLine("I need to send: " + _dirtyFiles.Count + " files");

                if (_dirtyFiles.Count <= 0)
                {
                    Debug.WriteLine("Remote server had all new versions... Check for error");
                    return;
                }

                //Check if server requested from previous version
                byte[] oldChecksum = guidAndKnownVersions[_dirtyFiles[0].Guid];
                if (oldChecksum.SequenceEqual(_dirtyFiles[0].OldChecksum))
                {   
                    //Send patch
                    bool onlyOneDirtyFile = _dirtyFiles.Count <= 1;
                    StaticChunker staticChunker = new StaticChunker();
                    List<IocpRemoteFileUploadPacket> packets = staticChunker.ChunkPatch(_dirtyFiles[0].Patch);
                    metadataPacket = new IocpRemoteFileUploadMetadataPacket(_localServerUuid, _dirtyFiles[0].Guid, _dirtyFiles[0].RelativePath, oldChecksum, packets.Count);
                    Packet transmissionMetadataPacket = EncodeIocpRemoteMetadataPacket(metadataPacket);
                    Console.WriteLine("Sending metadata packet: ");
                    Console.WriteLine(metadataPacket.ToString());
                    client.Send(transmissionMetadataPacket);

                    foreach (Packet transportPacket in packets.Select(packet => EncodeIocpRemoteFileUploadPacket(packet, onlyOneDirtyFile)))
                    {
                        client.Send(transportPacket);
                    }
                    return;
                }
                    //removed to reduce complexity for artifact showing the protocol, but normally in a long-running system here we
                    //Check database for older versions
                    //_unitofWork.osv
                    //if found in data
                    //Send patch from prev version
                    //Else
                    //Fall back to rsync
            }

            //Server has fully received a file, it is either sucess or failure
            if (flag.HasFlag(IocpTransportFlags.TransportDataFileResponse))
            {
                PacketSerializer<IocpRemoteFileUploadFileCompletedPacket> responsePacketSerializer = new PacketSerializer<IocpRemoteFileUploadFileCompletedPacket>(cpMessage.GetData(), 0,
                    cpMessage.GetDataLength());
                IocpRemoteFileUploadFileCompletedPacket responsePacket = responsePacketSerializer.ClonePacketObj();
                Debug.WriteLine("File response for file: " + responsePacket.Uuid);
                if (!responsePacket.Ok)
                {
                    Debug.WriteLine("Not ok!");
                    //Renegotiation!
                    //await renegotiation packet
                    return;
                }
                    _dirtyFiles.RemoveAll(element => element.Guid == responsePacket.Uuid);
                    DispatchNextDirtyFileToRemoteServer(client);
            }

            //Begin final step in retransmission phase
            if (flag.HasFlag(IocpTransportFlags.TransportRetransmissionResponsePacket))
            {
                PacketSerializer<IocpRemoteRetransmissionResponsePacket> responsePacketSerializer = new PacketSerializer<IocpRemoteRetransmissionResponsePacket>(cpMessage.GetData(), 0,
                    cpMessage.GetDataLength());
                IocpRemoteRetransmissionResponsePacket message = responsePacketSerializer.ClonePacketObj();

                //Chunk patch
                StaticChunker staticChunker = new StaticChunker();
                List<IocpRemoteFileUploadPacket> packets = staticChunker.ChunkPatch(_dirtyFiles[0].Patch);
                List<IocpRemoteFileUploadPacket> purgedPackets = new List<IocpRemoteFileUploadPacket>();
                foreach (var chunkIndex in message.SuccessfullyTransferredBlocksId)
                {
                    purgedPackets.AddRange(packets.Where(missingChunk => missingChunk.ChunkNum == chunkIndex));
                }

                //Send missing chunks!
                bool isLast = _dirtyFiles.Count <= 1;
                foreach (Packet packet in purgedPackets.Select(purgedPacket => EncodeIocpRemoteFileUploadPacket(purgedPacket, isLast)))
                {
                    client.Send(packet);
                }
            }
        }

        private void DispatchNextDirtyFileToRemoteServer(INetworkClient client)
        {
            if (_dirtyFiles.Count <= 0)
            {
                Console.WriteLine("Test completed");
            }
            byte[] oldChecksum = _filesMissingAtRemoteServer[_dirtyFiles[0].Guid];
            if (!oldChecksum.SequenceEqual(_dirtyFiles[0].OldChecksum)) return;

            bool onlyOneDirtyFile = _dirtyFiles.Count <= 1;
            StaticChunker staticChunker = new StaticChunker();
            List<IocpRemoteFileUploadPacket> packets = staticChunker.ChunkPatch(_dirtyFiles[0].Patch);
            //Generate metadata
            IocpRemoteFileUploadMetadataPacket metadataPacket = new IocpRemoteFileUploadMetadataPacket(_localServerUuid, _dirtyFiles[0].Guid, _dirtyFiles[0].RelativePath, oldChecksum, packets.Count);
            Packet transmissionMetadataPacket = EncodeIocpRemoteMetadataPacket(metadataPacket);
            Debug.WriteLine("Sending metadatapacket for: " + metadataPacket.FileUuid);
            client.Send(transmissionMetadataPacket);

            System.Threading.Thread.Sleep(800);
            //Send data
            Console.WriteLine("Dispatching chunks for file: " + _dirtyFiles[0].Guid);
            foreach (Packet transportPacket in packets.Select(packet => EncodeIocpRemoteFileUploadPacket(packet, onlyOneDirtyFile)))
            {
                client.Send(transportPacket);
            }
        }

        private static Packet EncodeIocpRemoteMetadataPacket(IocpRemoteFileUploadMetadataPacket packet)
        {

            const IocpTransportFlags remoteFlags = IocpTransportFlags.ClientToServer | IocpTransportFlags.TransportMetadata | IocpTransportFlags.FileUpload;

            PacketSerializer<IocpRemoteFileUploadMetadataPacket> serializer = new PacketSerializer<IocpRemoteFileUploadMetadataPacket>(packet);
            byte[] message = serializer.PacketRaw;

            IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (remoteFlags));
            PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);
            byte[] packetData = finalSerializer.PacketRaw;
            return new Packet(packetData, 0, packetData.Length, false);
        }


        private static Packet EncodeIocpRemoteFileRetransmissionPacket(IocpRemoteRetransmissionPacket packet)
        {

            const IocpTransportFlags remoteFlags = IocpTransportFlags.ClientToServer | IocpTransportFlags.TransportRetransmissionPacket | IocpTransportFlags.TransportData;

            PacketSerializer<IocpRemoteRetransmissionPacket> serializer = new PacketSerializer<IocpRemoteRetransmissionPacket>(packet);
            byte[] message = serializer.PacketRaw;

            IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (remoteFlags));
            PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);
            byte[] packetData = finalSerializer.PacketRaw;
            return new Packet(packetData, 0, packetData.Length, false);
        }

        private static Packet EncodeIocpRemoteFileUploadPacket(IocpRemoteFileUploadPacket packet, bool isLastFile)
        {
            IocpTransportFlags remoteFlags;
            if (isLastFile)
            {
                remoteFlags = IocpTransportFlags.ClientToServer | IocpTransportFlags.FileUpload | IocpTransportFlags.TransportData | IocpTransportFlags.TerminateOperation;
            }
            else
            {
                remoteFlags = IocpTransportFlags.ClientToServer | IocpTransportFlags.FileUpload | IocpTransportFlags.TransportData;
            }

            PacketSerializer<IocpRemoteFileUploadPacket> serializer = new PacketSerializer<IocpRemoteFileUploadPacket>(packet);
            byte[] message = serializer.PacketRaw;

            IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (remoteFlags));
            PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);
            byte[] packetData = finalSerializer.PacketRaw;
            return new Packet(packetData, 0, packetData.Length, false);
        }

        public void OnSent(INetworkClient client, SendStatus status, Packet sentPacket)
        {
            switch (status)
            {
                case SendStatus.Success:
                    Debug.WriteLine("SEND Success");
                    break;
                case SendStatus.FailConnectionClosing:
                    Debug.WriteLine("SEND failed due to connection closing");
                    break;
                case SendStatus.FailInvalidPacket:
                    Debug.WriteLine("SEND failed due to invalid socket");
                    break;
                case SendStatus.FailNotConnected:
                    Debug.WriteLine("SEND failed due to no connection");
                    break;
                case SendStatus.FailSocketError:
                    Debug.WriteLine("SEND Socket Error");
                    break;
            }
        }

        public void OnDisconnect(INetworkClient client)
        {
            Debug.WriteLine("Disconnected from server");
        }
    }
}
