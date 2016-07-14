using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.Utility;

namespace BusinessLayer.Implementation.rsync
{
    internal class SignatureStream : Stream
    {
        private const int BlocksToBuffer = 100;
        private const long HeaderLength = 12;
        private readonly BinaryReader _inputReader;
        private readonly Stream _inputStream;
        private MemoryStream _bufferStream;
        private long _currentPosition;
        private SignatureJobSettings _settings;

        public SignatureStream(Stream inputStream, SignatureJobSettings settings)
        {
            _inputStream = inputStream;
            _inputReader = new BinaryReader(inputStream);
            _settings = settings;

            // initialize the buffer with the header
            InitializeHeader();
            _currentPosition = 0;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get
            {
                var blockCount = (_inputStream.Length + _settings.BlockLength - 1)/_settings.BlockLength;
                return HeaderLength + blockCount*(4 + _settings.StrongSumLength);
            }
        }

        public override long Position
        {
            get { return _currentPosition; }

            set { Seek(value, SeekOrigin.Begin); }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_bufferStream.Position == _bufferStream.Length)
            {
                FillBuffer();
            }

            var length = await _bufferStream.ReadAsync(buffer, offset, count, cancellationToken);
            _currentPosition += length;
            return length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).Result;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return ReadAsync(buffer, offset, count).ToApm(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return ((Task<int>) asyncResult).Result;
        }

        private void FillBuffer()
        {
            _bufferStream = new MemoryStream();
            var writer = new BinaryWriter(_bufferStream);
            for (var i = 0; i < BlocksToBuffer; i++)
            {
                var block = _inputReader.ReadBytes(_settings.BlockLength);
                if (block.Length != 0)
                {
                    SignatureHelpers.WriteBlock(writer, block, _settings);
                }
            }

            writer.Flush();
            _bufferStream.Seek(0, SeekOrigin.Begin);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = StreamUtility.ComputeNewPosition(offset, origin, Length, Position);

            if (newPosition < HeaderLength)
            {
                InitializeHeader();
                _bufferStream.Seek(newPosition, SeekOrigin.Begin);
            }
            else
            {
                var adjustedPosition = newPosition - HeaderLength;
                var blockSize = (4 + _settings.StrongSumLength);
                long remainderBytes;
                var blockNumber = Math.DivRem(adjustedPosition, blockSize, out remainderBytes);

                _inputStream.Seek(blockNumber*_settings.BlockLength, SeekOrigin.Begin);
                FillBuffer();
                _bufferStream.Seek(remainderBytes, SeekOrigin.Begin);
            }

            _currentPosition = newPosition;
            return _currentPosition;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private void InitializeHeader()
        {
            _bufferStream = new MemoryStream();
            var writer = new BinaryWriter(_bufferStream);
            SignatureHelpers.WriteHeader(writer, _settings);
            writer.Flush();
            _bufferStream.Seek(0, SeekOrigin.Begin);
        }
    }
}