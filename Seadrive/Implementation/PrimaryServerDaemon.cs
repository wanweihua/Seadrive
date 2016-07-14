using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using BusinessLayer.Entities.IoCpPackets;
using BusinessLayer.Entities.IoCpPackets.RemoteTransportPackets;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Implementation.Transport.Server;
using Data_Abstraction_Layer.Transport;
using Data_Abstraction_Layer.Transport.Models;

namespace PrimaryServer
{
    public class PrimaryServerDaemon : INetworkServerAcceptor, INetworkServerCallback, INetworkSocketCallback
    {
        private readonly INetworkServer _server = new IocpTcpServer();
        private readonly List<INetworkSocket> _activeSessions = new List<INetworkSocket>();
        private readonly Dictionary<string, IocpRemoteFileUploadMetadataPacket> _localServerState = new Dictionary<string, IocpRemoteFileUploadMetadataPacket>(); //HOLDS STATE OF CURRENT FILE! Important!
        private readonly TransportUnitOfWork _unitOfWork = new TransportUnitOfWork();
        List<ChunksReceived> _chunksReceivedNotProperSqlVersion = new List<ChunksReceived>(); 
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);

        public void StartServer(string portString)
        {
            ServerOperands operands = new ServerOperands(this, portString, this);
            _server.StartServer(operands);
            Console.WriteLine("Started server");

        }

        public void OnReceived(INetworkSocket socket, Packet receivedPacket)
        {
            Console.WriteLine("Got " + receivedPacket.PacketByteSize + " bytes from: " + socket.IPInfo.IPAddress);
            PacketSerializer<IocpTransportPacket> transportMessage = new PacketSerializer<IocpTransportPacket>(receivedPacket.PacketRaw, 0,
                 receivedPacket.PacketByteSize);
            IocpTransportPacket cpMessage = transportMessage.ClonePacketObj();
            IocpTransportFlags flag = cpMessage.GetMessageType();
            Console.WriteLine("CURRENT FLAGS: " + flag);
            //Local server from vessel sending TO the primary server
            if (!flag.HasFlag(IocpTransportFlags.ClientToServer)) return;
            //First packet sent from local server to primary
            if (flag.HasFlag(IocpTransportFlags.InitialPacket))
            {
                PacketSerializer<IocpInitialCommunicationPacket> cpStruct = new PacketSerializer<IocpInitialCommunicationPacket>(cpMessage.GetData(), 0,
                    cpMessage.GetDataLength());
                IocpInitialCommunicationPacket data = cpStruct.ClonePacketObj();
                IocpRemoteProtocolIntialResponsePacket response = new IocpRemoteProtocolIntialResponsePacket();
                Dictionary<string, byte[]> responseData = new Dictionary<string, byte[]>();

                //Register client!
                string uuid = data.ClientUuid;
                if (uuid == null)
                {
                    return;
                }
                _unitOfWork.LocalServerIdSessionRepository.Insert(new LocalServerIdSession {LocalServerId = uuid});

                //Create response!
                foreach (var mStruct in data.InitialData)
                {
                    //add the files and current version if any
                    VirtualFile currentEntry = _unitOfWork.VirtualFileRepository.GetById(mStruct.Guid);
                    if (currentEntry != null && currentEntry.FileChecksum == mStruct.Checksum)
                    {
                        //Have file, and checksum a-ok! Do nothing
                    }
                    else
                    {
                        //File is either changed or does not exist. For now, send Guid and checksum of current version if exist
                        responseData.Add(mStruct.Guid, currentEntry == null ? null : currentEntry.FileChecksum);
                    }
                }

                //Response dict built, so we need to serialize the response for transmission and wrap it into the transport packet
                response.GuidAndKnownVersions = responseData;
                PacketSerializer<IocpRemoteProtocolIntialResponsePacket> responseSerializer = new PacketSerializer<IocpRemoteProtocolIntialResponsePacket>(response);
                byte[] message = responseSerializer.PacketRaw;
                IocpTransportPacket msg = new IocpTransportPacket(IocpTransportFlags.ServerToClient | IocpTransportFlags.InitialPacketResponse,
                    message.Length, message);

                Console.WriteLine("I need: " +  response.GuidAndKnownVersions.Keys.Count +"  files");

                //Finalize serialization of packet
                PacketSerializer<IocpTransportPacket> newSerializer = new PacketSerializer<IocpTransportPacket>(msg);
                byte[] packetData = newSerializer.PacketRaw;
                Packet packet = new Packet(packetData, 0, packetData.Length, false);
                socket.Send(packet);
            }

            if (flag.HasFlag(IocpTransportFlags.TransportRetransmissionPacket))
            {
                //Retransport from a previously connected client! Fetch
                // _unitOfWork.LocalServerIdSessionRepository.Insert(new LocalServerIdSession {LocalServerId = uuid});
                PacketSerializer<IocpRemoteRetransmissionPacket> cpStruct = new PacketSerializer<IocpRemoteRetransmissionPacket>(cpMessage.GetData(), 0,
                    cpMessage.GetDataLength());
                IocpRemoteRetransmissionPacket data = cpStruct.ClonePacketObj();

                //Find missing chunks
                LocalServerIdSession previousSession = _unitOfWork.LocalServerIdSessionRepository.GetById(data.LocalServerUuid);
                //List<ChunksReceived> chunksReceived = _unitOfWork.ChunksReceivedRepository.Get(chunks => chunks.FileChecksum.SequenceEqual(previousSession.CurrentFileInTransit)).ToList();
                List<ChunksReceived> chunksReceived = _chunksReceivedNotProperSqlVersion;
                List<int> chunksReceivedIndexes = chunksReceived.Select(chunk => chunk.ChunkNum).ToList();
                    
                //Locate the previous version of the sender
                IocpRemoteFileUploadMetadataPacket metadata = null;
                string oldSocket = null;
                foreach (var key in _localServerState.Keys)
                {
                    metadata = _localServerState[key];
                    if (!metadata.LocalServerUuid.Equals(data.LocalServerUuid)) continue;
                    //Located
                    oldSocket = key;
                    break;
                }

                if (oldSocket != null) //Entry located
                {
                    //_localServerState.Remove(oldSocket); //Remove entry
                    //Add new! With the session as it was
                    _localServerState.Add(socket.IPInfo.IPAddress, metadata);
                }

                //Signal local server that we are ready for retransmission, dispatch retransmission packet and leave retransmission state
                IocpRemoteRetransmissionResponsePacket response = new IocpRemoteRetransmissionResponsePacket(metadata.FileRelativePath, chunksReceivedIndexes);
                PacketSerializer<IocpRemoteRetransmissionResponsePacket> responseSerializer = new PacketSerializer<IocpRemoteRetransmissionResponsePacket>(response);
                byte[] message = responseSerializer.PacketRaw;
                IocpTransportPacket msg = new IocpTransportPacket(IocpTransportFlags.ServerToClient | IocpTransportFlags.TransportRetransmissionResponsePacket,
                    message.Length, message);

                //Finalize serialization of packet
                PacketSerializer<IocpTransportPacket> newSerializer = new PacketSerializer<IocpTransportPacket>(msg);
                byte[] packetData = newSerializer.PacketRaw;
                Packet packet = new Packet(packetData, 0, packetData.Length, false);
                socket.Send(packet);
            }

            // Local server is transmitting file to primary server
            if (flag.HasFlag(IocpTransportFlags.FileUpload))
            {
                if (flag.HasFlag(IocpTransportFlags.TransportMetadata))
                {
                    Console.WriteLine("Parsing metadata packet");
                    PacketSerializer<IocpRemoteFileUploadMetadataPacket> cpStruct = new PacketSerializer<IocpRemoteFileUploadMetadataPacket>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());
                    IocpRemoteFileUploadMetadataPacket data = cpStruct.ClonePacketObj();
                    Console.WriteLine("new file! " + data.FileUuid);
                    if (_localServerState.ContainsKey(socket.IPInfo.IPAddress))
                    {
                        _localServerState[socket.IPInfo.IPAddress] = data;
                    }
                    else
                    {
                        _localServerState.Add(socket.IPInfo.IPAddress, data);    
                    }

                    return;
                }

                //If we got here, it means the local server is uploading data to a file
                if (flag.HasFlag(IocpTransportFlags.TransportData))
                {     
                    Console.WriteLine("Parsing transportData");
                    //Fetch UUID OF SENDER
                    IocpRemoteFileUploadMetadataPacket metadata = _localServerState[socket.IPInfo.IPAddress];
                    if (metadata == null)
                    {
                        //Request metadata Re-Transmission
                        Console.WriteLine("Could not find metadata! Panic");
                        return;
                    }

                    //Deserialize info
                    PacketSerializer<IocpRemoteFileUploadPacket> cpStruct = new PacketSerializer<IocpRemoteFileUploadPacket>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());
                    IocpRemoteFileUploadPacket data = cpStruct.ClonePacketObj();
                    ChunksReceived receivedChunk = new ChunksReceived
                    {
                        ChunkNum = data.ChunkNum,
                        FileChecksum = metadata.FileChecksum,
                        LocalServerId = metadata.LocalServerUuid,
                        Size = data.Size
                    };
                    _chunksReceivedNotProperSqlVersion.Add(receivedChunk);
                    //Add data to current list of chunks COMMENTED OUT BECAUSE My virtual server for satelite links does not support parallel execution
                    //Its a sql express

                    // _unitOfWork.ChunksReceivedRepository.Insert(receivedChunk);
                    // _unitOfWork.Save();

                    //Check if we've received all chunks

                    //int chunkCount = _unitOfWork.ChunksReceivedRepository.Get(a => a.FileChecksum == metadata.FileChecksum).ToList().Count;
                    int chunkCount = _chunksReceivedNotProperSqlVersion.Count;

                    Console.WriteLine("Chunk count is: " + chunkCount + "Should be: " + metadata.NumChunks);
                    if (chunkCount == metadata.NumChunks)
                    {
                        Console.WriteLine("Full file received!");
                        //File fully received! Retrieve all chunks and patch the file! 
                        //List<ChunksReceived> fileChunks = _unitOfWork.ChunksReceivedRepository.Get(a => a.FileChecksum == metadata.FileChecksum).ToList();
                        //TODO: PATCH FILE!

                        const bool patchSuccess = true;
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (patchSuccess) //For testing, if patch is true
                        {
                            //Update local session!
                            LocalServerIdSession session = _unitOfWork.LocalServerIdSessionRepository.GetById(metadata.LocalServerUuid);
                            Packet packet = CreateIocpRemoteFileUploadFileCompletedPacket(true, metadata.FileUuid);

                            session.CurrentFileInTransit = null;
                            _unitOfWork.LocalServerIdSessionRepository.Update(session);
                            _localServerState[socket.IPInfo.IPAddress] = null;
                            //_unitOfWork.ChunksReceivedRepository.DeleteList(fileChunks);
                            _chunksReceivedNotProperSqlVersion = new List<ChunksReceived>();

                            if (flag.HasFlag(IocpTransportFlags.TerminateOperation)) // SENDER has sent ALL files
                            {
                                //WE here normally write the files that local server does not have, removed for testing of transport protocol.
                            }
                            else
                            {
                                socket.Send(packet);
                                return;
                            }

                        }
                        else
                        {
                            //Could not patch, we fucked this up Send chunks checksums.
                            Packet packet = CreateIocpRemoteFileUploadFileCompletedPacket(false, metadata.LocalServerUuid);
                            //SEnd packets
                            //For the master thesis, assume TCP/IP corruption has not occured, the possiblity is extremely small, and actually not increased on the satelite based netowrks
                            //So we ignore tcp corruption for now (only way we get here!)
                        }
                    }
                    else
                    {
                        //Keep receiving packets!
                    }
                }
            }
        }

        public void StopServer()
        {
            if (_server.IsServerStarted)
            {
                _server.StopServer();
            }
        }

        public void OnServerAccepted(INetworkServer server, INetworkSocket socket)
        {
            Console.WriteLine("Accepted");
        }

        public void OnServerStopped(INetworkServer server)
        {
            Console.WriteLine("Server is stopped");
        }

        public void OnNewConnection(INetworkSocket socket)
        {
            _activeSessions.Add(socket);
            Console.WriteLine("Got new user with IP-addr: " + socket.IPInfo.IPAddress);

        }

        public bool OnAccept(INetworkServer server, IPInfo ipInfo)
        {
            Console.WriteLine("Accepted connection from: " + ipInfo.IPAddress);
            return true;
        }

        public INetworkSocketCallback GetSocketCallback()
        {
            return this;
        }

        public void OnServerStarted(INetworkServer server, StartStatus status)
        {
            if (status == StartStatus.FailAlreadyStarted || status == StartStatus.Success)
            {
                Console.WriteLine("Server started callback. Startstatus == Success");
            }
            else
            {
                Console.WriteLine("Could not connect to server with status: " + status);
            }
        }

        private static IocpTransportPacket CreateIocpTransportMessage(byte[] data, IocpTransportFlags flags)
        {
            return new IocpTransportPacket(flags, data.Length, data);
        }

        private static Packet CreateIocpRemoteFileUploadFileCompletedPacket(bool success, string uuid)
        {
            IocpRemoteFileUploadFileCompletedPacket response = new IocpRemoteFileUploadFileCompletedPacket(uuid, success);
            PacketSerializer<IocpRemoteFileUploadFileCompletedPacket> serializer = new PacketSerializer<IocpRemoteFileUploadFileCompletedPacket>(response);
            byte[] message = serializer.PacketRaw;

            IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (IocpTransportFlags.ServerToClient | IocpTransportFlags.TransportDataFileResponse));
            PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);
            byte[] packetData = finalSerializer.PacketRaw;
            return new Packet(packetData, 0, packetData.Length, false);
        }

        public void OnSent(INetworkSocket socket, SendStatus status, Packet sentPacket)
        {
            switch (status)
            {
                case SendStatus.Success:
                    Console.WriteLine("SEND Success");
                    break;
                case SendStatus.FailConnectionClosing:
                    Console.WriteLine("SEND failed due to connection closing");
                    break;
                case SendStatus.FailInvalidPacket:
                    Console.WriteLine("SEND failed due to invalid socket");
                    break;
                case SendStatus.FailNotConnected:
                    Console.WriteLine("SEND failed due to no connection");
                    break;
                case SendStatus.FailSocketError:
                    Console.WriteLine("SEND Socket Error");
                    break;
            }
        }

        public void OnDisconnect(INetworkSocket socket)
        {
            _activeSessions.Remove(socket);
            _localServerState.Remove(socket.IPInfo.IPAddress);
        }

    }
}
