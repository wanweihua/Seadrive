using System;
using System.Diagnostics;

namespace BusinessLayer.Entities.Transport
{
    public sealed class Packet
    {
        private byte[] _packet;
        private int _packetSize;
        private int _packetOffset;
        private readonly Object _packetLock = new Object();

        public Packet(byte[] packet = null, int offset = 0, int byteSize = 0, bool shouldAllocate = true)
        {
            _packet = null;
            _packetSize = 0;
            IsAllocated = shouldAllocate;
            if (shouldAllocate)
            {
                if (byteSize <= 0) return;
                _packet = new byte[byteSize];
                if (packet != null)
                {
                    Array.Copy(packet, offset, _packet, 0, byteSize);
                }
                _packetOffset = 0;
                _packetSize = byteSize;
            }
            else
            {
                _packet = packet;
                _packetOffset = offset;
                _packetSize = byteSize;
            }
        }

        public Packet(Packet b)
        {
            lock (b._packetLock)
            {
                _packet = null;
                if (b.IsAllocated)
                {
                    if (b._packetSize > 0)
                    {
                        _packet = new byte[b._packetSize];
                        Array.Copy(b._packet, _packet, b._packetSize);
                    }
                }
                else
                {
                    _packet = b._packet;
                }
                _packetSize = b._packetSize;
                _packetOffset = b._packetOffset;
                IsAllocated = b.IsAllocated;
            }

        }

        public int PacketByteSize
        {
            get
            {
                lock (_packetLock)
                {
                    return _packetSize;
                }
            }
        }

        public int PacketOffset
        {
            get
            {
                lock (_packetLock)
                {
                    return _packetOffset;
                }
            }
        }

        public int AllocatedByteSize
        {
            get
            {
                lock (_packetLock)
                {
                    return _packet != null ? _packet.Length : 0;
                }
            }
        }

        public bool IsAllocated { get; private set; }
        public byte[] PacketRaw
        {
            get
            {
                lock (_packetLock)
                {
                    return _packet;
                }
            }
        }

        public void SetPacket(byte[] packet, int offset, int packetByteSize)
        {
            lock (_packetLock)
            {
                if (IsAllocated)
                {
                    if (_packet != null)
                    {
                        if (_packet.Length >= packetByteSize)
                        {
                            Array.Copy(packet, offset, _packet, 0, packetByteSize);
                            _packetSize = packetByteSize;
                            _packetOffset = 0;
                            return;
                        }
                    }
                    _packet = null;
                    if (packetByteSize > 0)
                    {
                        _packet = new byte[packetByteSize];
                        Debug.Assert(_packet != null);
                    }
                    if (packet != null)
                    {
                        Array.Copy(packet, offset, _packet, 0, packetByteSize);
                    }
                    _packetSize = packetByteSize;
                    _packetOffset = 0;

                }
                else
                {
                    _packet = packet;
                    _packetSize = packetByteSize;
                    _packetOffset = offset;
                }
            }
        }
    }
}
