using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BusinessLayer.Entities.Chunking;
using BusinessLayer.Entities.IoCpPackets;
using BusinessLayer.Entities.Transport;
using BusinessLayer.Extensions;
using BusinessLayer.Implementation.Transport.Server;
using Data_Abstraction_Layer.Deduplication;
using Data_Abstraction_Layer.Deduplication.Models;
using Seadrive.Transport;
using SeaDrive.Compression;
using SeaDrive.Utility;

namespace Seadrive.Implementation
{
    public class LocalServer : INetworkServerAcceptor, INetworkServerCallback, INetworkSocketCallback
    {
        private readonly INetworkServer _server = new IocpTcpServer();
        private readonly List<INetworkSocket> _socketList = new List<INetworkSocket>();
        private readonly UnitOfWork _unitOfWork = new UnitOfWork();
        private static string _syncFolder = "";
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);

        public void StartServer(string portString, string syncFolder)
        {
            _syncFolder = syncFolder;
            ServerOperands operands = new ServerOperands(this, portString, this);
            _server.StartServer(operands);
            Console.WriteLine("Local Server operational");
        }

        public void StopServer()
        {
            if (_server.IsServerStarted)
            {
                _server.StopServer();
            }
        }

        public bool OnAccept(INetworkServer server, IPInfo ipInfo)
        {
            Debug.WriteLine("Accepted connection from: " + ipInfo.IPAddress);
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
                Debug.WriteLine("Server started callback. Startstatus == Success");
            }
            else
            {
                Debug.WriteLine("Could not connect to server with status: " + status);
            }
        }

        public void OnServerAccepted(INetworkServer server, INetworkSocket socket)
        {
            Debug.WriteLine("Accepted");
        }

        public void OnServerStopped(INetworkServer server)
        {
            Debug.WriteLine("Server is stopped");
        }

        public void OnNewConnection(INetworkSocket socket)
        {
            _socketList.Add(socket);
            Debug.WriteLine("Got new user with IP-addr: " + socket.IPInfo.IPAddress);

        }

        public void OnReceived(INetworkSocket socket, Packet receivedPacket)
        {
            Debug.WriteLine("Got " + receivedPacket.PacketByteSize + " bytes from: " + socket.IPInfo.IPAddress);
            PacketSerializer<IocpTransportPacket> transportMessage = new PacketSerializer<IocpTransportPacket>(receivedPacket.PacketRaw, 0
                , receivedPacket.PacketByteSize);
            IocpTransportPacket cpMessage = transportMessage.ClonePacketObj();
            IocpTransportFlags flag = cpMessage.GetMessageType();

            //Client uploading to local hostspot 
            if (flag.HasFlag(IocpTransportFlags.ClientToServer) && !flag.HasFlag(IocpTransportFlags.TerminateOperation))
            {
                if (flag.HasFlag(IocpTransportFlags.InitialPacket))
                {

                    PacketSerializer<IocpInitialCommunicationPacket> cpStruct = new PacketSerializer<IocpInitialCommunicationPacket>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());
                    IocpInitialCommunicationPacket data = cpStruct.ClonePacketObj();
                    IocpLocalServerResponsePacket response = new IocpLocalServerResponsePacket();
                    foreach (var mStruct in data.InitialData)
                    {
                        FileDirectory currentEntry = _unitOfWork.FilesystemEntryRepository.GetById(new Guid(mStruct.Guid));
                        if (currentEntry != null && currentEntry.Checksum == mStruct.Checksum)
                        {
                            //Have file, and checksum a-ok! Do nothing
                        }
                        else
                        {
                            //File changes or w/e. High latency connection, no reason to do anything fancy, request entire file. 
                            response.GuidList.Add(mStruct.Guid);
                        }
                    }
                    PacketSerializer<IocpLocalServerResponsePacket> responseSerializer = new PacketSerializer<IocpLocalServerResponsePacket>(response);
                    byte[] message = responseSerializer.PacketRaw;
                    IocpTransportPacket msg = new IocpTransportPacket(IocpTransportFlags.ServerToClient & IocpTransportFlags.InitialPacketResponse,
                        message.Length, message);

                    //Finalize serialization of packet
                    PacketSerializer<IocpTransportPacket> newSerializer = new PacketSerializer<IocpTransportPacket>(msg);
                    byte[] packetData = newSerializer.PacketRaw;
                    Packet packet = new Packet(packetData, 0, packetData.Length, false);
                    socket.Send(packet);
                }

                if (flag.HasFlag(IocpTransportFlags.FileUpload))
                {
                    //Receive the file in question
                    PacketSerializer<IocpTransportLocalFiles> cpFile = new PacketSerializer<IocpTransportLocalFiles>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());
                    IocpTransportLocalFiles files = cpFile.ClonePacketObj();
                   
                    //Since we removed the channels and the crazy difficult parallel env, lets do it easier!
                    foreach (var file in files.IocpTransportFiles)
                    {
                        List<CompressedChunk> compressedChunks = file.GetChunks();
                        Decompressor decompressor = new Decompressor(compressedChunks);
                        List<Chunk> chunks = decompressor.Decompress(file.GetFileChecksum(), file.GetFilename());

                        //FIND FILE
                        string pathToFile = _syncFolder + file.GetFilename();
                        MemoryStream memStream = new MemoryStream();

                        //Get EXACT new contents of new file
                        foreach (Chunk chunk in chunks)
                        {
                            if (chunk.GetSize() != ChunkSize)
                            {
                                byte[] contents = chunk.GetContents();
                                for (int j = 0; j < chunk.GetSize(); j++)
                                {
                                    memStream.Append(contents[j]);
                                }
                            }
                            else
                            {
                                memStream.Append(chunk.GetContents());
                            }
                        }

                        byte[] newFileContents = memStream.GetBuffer();
                        //Create Patch!

                        //Add patch to sql

                        //Replace file with new version
                        File.WriteAllBytes(_syncFolder + pathToFile,newFileContents);
                    }
                }
                //File sends GUID of missing files from the initial communication to the server which contains the GUID of the requested files
                if (flag.HasFlag(IocpTransportFlags.FileDownload))
                {
                    PacketSerializer<IocpLocalServerResponsePacket> requestedFilesSerializer = new PacketSerializer<IocpLocalServerResponsePacket>(cpMessage.GetData(), 0,
                        cpMessage.GetDataLength());
                    IocpLocalServerResponsePacket files = requestedFilesSerializer.ClonePacketObj();
                    List<IocpTransportFile> retval = (from guid in files.GuidList select _unitOfWork.FilesystemEntryRepository.GetById(new Guid(guid)) into fDir let currentEntries = _unitOfWork.CacheEntryRepository.Get(m => m.FileChecksum == fDir.Checksum).ToList() let chunks = MiscUtility.ExtractCompressedChunksFromCacheEntries(currentEntries) select new IocpTransportFile(fDir.Id, fDir.Checksum, chunks.Count, chunks, fDir.Filename)).ToList();

                    //Send the files
                    IocpTransportLocalFiles cpFiles = new IocpTransportLocalFiles { IocpTransportFiles = retval };
                    PacketSerializer<IocpTransportLocalFiles> cpFilesSerializer = new PacketSerializer<IocpTransportLocalFiles>(cpFiles);
                    byte[] message = cpFilesSerializer.PacketRaw;
                    IocpTransportPacket transportPacket = CreateIocpTransportMessage(message, (IocpTransportFlags.ClientToServer & IocpTransportFlags.FileUpload));
                    PacketSerializer<IocpTransportPacket> finalSerializer = new PacketSerializer<IocpTransportPacket>(transportPacket);
                    byte[] packetData = finalSerializer.PacketRaw;
                    Packet packet = new Packet(packetData, 0, packetData.Length, false);
                    socket.Send(packet);
                }
            }

            //Server to server communication, we've removed land-based entities for the thesis, therefore this is empty in the delivery
            if (flag.HasFlag(IocpTransportFlags.ServerToClient) && !flag.HasFlag(IocpTransportFlags.TerminateOperation))
            {
                //Server sends file to localserver
                if (flag.HasFlag(IocpTransportFlags.FileUpload))
                {

                }

                //Server request file from localserver
                if (flag.HasFlag(IocpTransportFlags.FileDownload))
                {

                }
            }
            if (flag.HasFlag(IocpTransportFlags.TerminateOperation))
            {
                Debug.WriteLine("Terminate");
                //Flush etc
            }

        }

        private static IocpTransportPacket CreateIocpTransportMessage(byte[] data, IocpTransportFlags flags)
        {
            return new IocpTransportPacket(flags, data.Length, data);
        }

        public void OnSent(INetworkSocket socket, SendStatus status, Packet sentPacket)
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

        public void OnDisconnect(INetworkSocket socket)
        {
            _socketList.Remove(socket);
        }
    }
}
