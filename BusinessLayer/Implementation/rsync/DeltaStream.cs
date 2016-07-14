using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.Utility;

namespace BusinessLayer.Implementation.rsync
{
    internal class DeltaStream : Stream
    {
        private readonly IEnumerator<OutputCommand> _commandsToOutput;
        private MemoryStream _currentCommandStream;

        public DeltaStream(Stream signatureStream, Stream inputStream)
        {
            var signature = SignatureHelpers.ParseSignatureFile(signatureStream);
            var inputReader = new BinaryReader(inputStream);
            var commands = DeltaCalculator.ComputeCommands(inputReader, signature);
            _commandsToOutput = commands.GetEnumerator();

            _currentCommandStream = new MemoryStream();
            var writer = new BinaryWriter(_currentCommandStream);
            StreamUtility.WriteBigEndian(writer, (uint) MagicNumber.Delta);
            writer.Flush();
            _currentCommandStream.Seek(0, SeekOrigin.Begin);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }

            set { throw new NotImplementedException(); }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_currentCommandStream.Position == _currentCommandStream.Length)
            {
                if (!_commandsToOutput.MoveNext())
                {
                    return 0;
                }

                _currentCommandStream = new MemoryStream();
                var writer = new BinaryWriter(_currentCommandStream);
                DeltaCalculator.WriteCommand(writer, _commandsToOutput.Current);
                writer.Flush();
                _currentCommandStream.Seek(0, SeekOrigin.Begin);
            }

            return await _currentCommandStream.ReadAsync(buffer, offset, count);
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

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
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
    }
}