﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.Utility;

namespace BusinessLayer.Implementation.rsync
{
    /// <summary>
    ///     Implements a streaming patching of data
    ///     The librsync delta format basically has two types of operations:
    ///     * Literal - these basically say "output these bytes"
    ///     * Copy - these provide a source offset and length from the input file, which should be copied to the output
    ///     This implements a readable stream interface to apply the delta to the input as you read the data
    ///     This basically buffers out the data from each command.
    /// </summary>
    internal class PatchedStream : Stream
    {
        private static readonly CommandFormat[] CommandFormatTable =
        {
            new CommandFormat(CommandKind.Literal, 0, 1, 0),
            new CommandFormat(CommandKind.Literal, 0, 2, 0),
            new CommandFormat(CommandKind.Literal, 0, 4, 0),
            new CommandFormat(CommandKind.Literal, 0, 8, 0),
            new CommandFormat(CommandKind.Copy, 0, 1, 1),
            new CommandFormat(CommandKind.Copy, 0, 1, 2),
            new CommandFormat(CommandKind.Copy, 0, 1, 4),
            new CommandFormat(CommandKind.Copy, 0, 1, 8),
            new CommandFormat(CommandKind.Copy, 0, 2, 1),
            new CommandFormat(CommandKind.Copy, 0, 2, 2),
            new CommandFormat(CommandKind.Copy, 0, 2, 4),
            new CommandFormat(CommandKind.Copy, 0, 2, 8),
            new CommandFormat(CommandKind.Copy, 0, 4, 1),
            new CommandFormat(CommandKind.Copy, 0, 4, 2),
            new CommandFormat(CommandKind.Copy, 0, 4, 4),
            new CommandFormat(CommandKind.Copy, 0, 4, 8),
            new CommandFormat(CommandKind.Copy, 0, 8, 1),
            new CommandFormat(CommandKind.Copy, 0, 8, 2),
            new CommandFormat(CommandKind.Copy, 0, 8, 4),
            new CommandFormat(CommandKind.Copy, 0, 8, 8)
        };

        private readonly List<CommandPosition> _commandSummary;
        private readonly Stream _delta;
        private readonly BinaryReader _deltaReader;
        private readonly Stream _input;

        /// <summary>
        ///     This represents the state of the current command.
        ///     Basically, we are always copying from one of the input streams.
        /// </summary>
        private StreamCopyHelper _currentCopyHelper;

        private long _outputPosition;

        public PatchedStream(Stream input, Stream delta)
        {
            _input = input;
            _delta = delta;
            _deltaReader = new BinaryReader(_delta);
            // read and check the header
            ReadHeader(_deltaReader);

            // Read in all of the commands from the delta to build a table which will tell us the length of the
            // resulting stream, as well as tell us how to seek into the file
            var currentPosition = _delta.Position;
            _commandSummary = ReadCommandSummary(_delta);
            _delta.Seek(currentPosition, SeekOrigin.Begin);

            // starting the current helper with 0 bytes left will force us to immediately read a new command
            _currentCopyHelper = new StreamCopyHelper(0, input);
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
            get
            {
                // if we add the length of all the commands, that will be the length of the output file
                return _commandSummary.Sum(c => c.Command.Length);
            }
        }

        public override long Position
        {
            get { return _outputPosition; }

            set { Seek(value, SeekOrigin.Begin); }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // if the current command can't supply any more data, then read a new command
            if (!_currentCopyHelper.MoreData)
            {
                // read the next command from the delta stream
                var command = ReadCommand(_deltaReader);
                // construct a copy helper to run that command
                _currentCopyHelper = ConstructCopyHelperForCommand(command, _input, _delta);
                if (_currentCopyHelper == null)
                {
                    // if it's null, that means we reached the end token
                    return 0;
                }
            }

            // now read the data based on the copy helper 
            var bytesRead = await _currentCopyHelper.ReadAsync(buffer, offset, count, cancellationToken);
            _outputPosition += bytesRead;
            return bytesRead;
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

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = StreamUtility.ComputeNewPosition(offset, origin, Length, Position);

            // search until we find the command just before this position
            long runningTotal = 0;
            int i;
            for (i = 0; i < _commandSummary.Count; i++)
            {
                // if this is the first command that takes us past the desired position
                var newTotal = runningTotal + _commandSummary[i].Command.Length;
                if (newTotal > newPosition)
                {
                    break;
                }

                runningTotal = newTotal;
            }

            if (i == _commandSummary.Count)
            {
                throw new ArgumentException("The specified offset is past the end of the stream");
            }

            // seek to the point that that command starts
            _delta.Seek(_commandSummary[i].DeltaStartPosition, SeekOrigin.Begin);

            // now read that command in
            var command = ReadCommand(_deltaReader);
            // construct a copy helper to run that command
            _currentCopyHelper = ConstructCopyHelperForCommand(command, _input, _delta);
            // finally seek into the copy helper for whatever bytes are left over
            _currentCopyHelper.SeekForward(newPosition - runningTotal);

            _outputPosition = newPosition;
            return newPosition;
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

        private static void ReadHeader(BinaryReader s)
        {
            var magic = StreamUtility.ReadBigEndianUint32(s);
            if (magic != (uint) MagicNumber.Delta)
            {
                throw new InvalidDataException(string.Format("Got magic number {0:x} instead of expected {1:x}", magic, (uint) MagicNumber.Delta));
            }
        }

        private static Command ReadCommand(BinaryReader s)
        {
            // the first byte indicates which kind and format it is
            var commandFormat = GetCommandFormat(s.ReadByte());

            // then we might have to read the parameters in
            var result = new Command();
            result.Kind = commandFormat.Kind;
            if (commandFormat.Length1 > 0)
            {
                result.Parameter1 = StreamUtility.ConvertFromBigEndian(s.ReadBytes(commandFormat.Length1));
            }
            else
            {
                // some of the commands encode the parameter directly into the command byte
                result.Parameter1 = commandFormat.ImmediateValue;
            }

            if (commandFormat.Length2 > 0)
            {
                result.Parameter2 = StreamUtility.ConvertFromBigEndian(s.ReadBytes(commandFormat.Length2));
            }

            return result;
        }

        private static CommandFormat GetCommandFormat(byte commandCode)
        {
            if (commandCode == 0)
            {
                return new CommandFormat(CommandKind.End, 0);
            }
            if (commandCode <= 64)
            {
                return new CommandFormat(CommandKind.Literal, commandCode);
            }
            if (commandCode < 85)
            {
                return CommandFormatTable[commandCode - 65];
            }

            return new CommandFormat(CommandKind.Reserved, commandCode);
        }

        private static StreamCopyHelper ConstructCopyHelperForCommand(Command command, Stream input, Stream delta)
        {
            switch (command.Kind)
            {
                case CommandKind.Literal:
                    // for a literal command, we copy bytes from the delta stream
                    return new StreamCopyHelper(command.Parameter1, delta);
                case CommandKind.Copy:
                    // for a copy command, we seek to the specified point and copy from the input stream
                    input.Seek(command.Parameter1, SeekOrigin.Begin);
                    return new StreamCopyHelper(command.Parameter2, input);
                case CommandKind.End:
                    return null;
                default:
                    throw new InvalidDataException(string.Format("Unknown command {0}", command.Parameter1));
            }
        }

        private static List<CommandPosition> ReadCommandSummary(Stream inputStream)
        {
            var result = new List<CommandPosition>();
            var inputReader = new BinaryReader(inputStream);
            var finished = false;
            while (!finished)
            {
                var position = inputStream.Position;
                var c = ReadCommand(inputReader);
                result.Add(new CommandPosition {Command = c, DeltaStartPosition = position});

                if (c.Kind == CommandKind.Literal)
                {
                    // if it's a literal, we have to jump over the bytes
                    inputStream.Seek(c.Length, SeekOrigin.Current);
                }
                else if (c.Kind == CommandKind.End)
                {
                    finished = true;
                }
            }

            return result;
        }

        private class StreamCopyHelper
        {
            private readonly Stream source;
            private long bytesLeft;

            public StreamCopyHelper(long bytes, Stream source)
            {
                bytesLeft = bytes;
                this.source = source;
            }

            public bool MoreData
            {
                get { return bytesLeft > 0; }
            }

            public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesToRead = (int) Math.Min(count, bytesLeft);
                var bytesActuallyRead = await source.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
                bytesLeft -= bytesActuallyRead;
                return bytesActuallyRead;
            }

            public void SeekForward(long bytes)
            {
                source.Seek(bytes, SeekOrigin.Current);
                bytesLeft -= bytes;
            }
        }

        public struct CommandFormat
        {
            public int ImmediateValue;
            public CommandKind Kind;
            public int Length1;
            public int Length2;

            public CommandFormat(CommandKind kind, int immediate, int length1 = 0, int length2 = 0)
            {
                Kind = kind;
                ImmediateValue = immediate;
                Length1 = length1;
                Length2 = length2;
            }
        }
    }

    public enum CommandKind
    {
        End,
        Literal,
        Copy,
        Reserved
    }

    public struct Command
    {
        public CommandKind Kind;
        public long Parameter1;
        public long Parameter2;

        public long Length
        {
            get
            {
                if (Kind == CommandKind.Literal)
                {
                    return Parameter1;
                }
                if (Kind == CommandKind.Copy)
                {
                    return Parameter2;
                }
                return 0;
            }
        }
    }

    /// <summary>
    ///     Represents the location of a command within the patched stream
    /// </summary>
    public struct CommandPosition
    {
        public Command Command;
        public long DeltaStartPosition;
    }
}