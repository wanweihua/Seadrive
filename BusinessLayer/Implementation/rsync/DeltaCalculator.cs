using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BusinessLayer.Utility;

namespace BusinessLayer.Implementation.rsync
{
    internal static class DeltaCalculator
    {
        private const byte MinVariableLiteral = 65;
        private const byte MinCopyCommand = 69;
        private const int MaxBlockSize = 4*1024*1024;

        public static void WriteCommand(BinaryWriter s, OutputCommand command)
        {
            if (command.Kind == CommandKind.Literal)
            {
                if (command.Literal.Count <= 64)
                {
                    if (command.Literal.Count == 0)
                    {
                        throw new ArgumentException("Literal must have at least 1 byte");
                    }

                    // literals from 1..64 have a command code equal to the length
                    s.Write((byte) command.Literal.Count);
                }
                else
                {
                    // longer literals encode the length
                    var lengthBytes = GetSizeNeeded((ulong) command.Literal.Count);
                    int idx = SizeToIdx(lengthBytes);
                    var commandCode = (byte) (MinVariableLiteral + idx);
                    s.Write(commandCode);
                    StreamUtility.WriteBigEndian(s, (ulong) command.Literal.Count, lengthBytes);
                }

                s.Write(command.Literal.ToArray());
            }
            else if (command.Kind == CommandKind.Copy)
            {
                var positionBytes = GetSizeNeeded(command.Position);
                var lengthBytes = GetSizeNeeded(command.Length);
                var positionIdx = SizeToIdx(positionBytes);
                var lengthIdx = SizeToIdx(lengthBytes);

                var commandCode = (byte) (MinCopyCommand + positionIdx*4 + lengthIdx);
                s.Write(commandCode);
                StreamUtility.WriteBigEndian(s, command.Position, positionBytes);
                StreamUtility.WriteBigEndian(s, command.Length, lengthBytes);
            }
            else if (command.Kind == CommandKind.End)
            {
                s.Write(0); // command code for end
            }
        }

        private static int GetSizeNeeded(ulong value)
        {
            if (value <= byte.MaxValue)
            {
                return 1;
            }
            if (value <= ushort.MaxValue)
            {
                return 2;
            }
            if (value <= uint.MaxValue)
            {
                return 4;
            }
            return 8;
        }

        private static byte SizeToIdx(int size)
        {
            switch (size)
            {
                case 1:
                    return 0;
                case 2:
                    return 1;
                case 4:
                    return 2;
                case 8:
                    return 3;
            }

            throw new ArgumentOutOfRangeException();
        }

        public static IEnumerable<OutputCommand> ComputeCommands(
            BinaryReader inputStream,
            SignatureFile signatures)
        {
            var currentCommand = new OutputCommand {Kind = CommandKind.Reserved};
            Blake32RollingChecksum currentSum;
            Queue<byte> currentBlock;

            // Read the first block to initialize
            ReadNewBlock(inputStream, signatures.BlockLength, out currentBlock, out currentSum);
            while (currentBlock.Count > 0)
            {
                // if the block has a matching rolling sum to any contained in the signature, those are candidates
                var matchCandidates = signatures.BlockLookup[currentSum.Digest];
                var wasMatch = false;
                if (matchCandidates.Any())
                {
                    // now compute the strong sum and see if any of the signature blocks match it
                    var currentStrongSum = signatures.StrongSumMethod(currentBlock.ToArray()).Take(signatures.StrongSumLength).ToArray();
                    var matchingBlock = matchCandidates.FirstOrDefault(s => s.StrongSum.SequenceEqual(currentStrongSum));

                    // if one of the signature blocks was a match, we can generate a Copy command
                    if (matchingBlock != null)
                    {
                        if (currentCommand.Kind == CommandKind.Copy &&
                            currentCommand.Position + currentCommand.Length == matchingBlock.StartPos)
                        {
                            // in this case, we can just add this copy onto the previous one
                            currentCommand.Length = currentCommand.Length + (ulong) currentBlock.Count;
                        }
                        else
                        {
                            // if we can't append to the current command, then return the current command and start a new one
                            if (currentCommand.Kind != CommandKind.Reserved)
                            {
                                yield return currentCommand;
                            }

                            currentCommand = new OutputCommand
                            {
                                Kind = CommandKind.Copy,
                                Position = matchingBlock.StartPos,
                                Length = (ulong) currentBlock.Count
                            };
                        }

                        // if we found a match, then we read a whole new block and reset the sum to it's hash
                        wasMatch = true;
                        ReadNewBlock(inputStream, signatures.BlockLength, out currentBlock, out currentSum);
                    }
                }

                // if there was no match for the current block, the we have to output it's first byte (at least)
                // as a literal value, and try again starting at the next byte
                if (!wasMatch)
                {
                    // pull out the oldest byte
                    var oldestByte = currentBlock.Dequeue();

                    // if we're not at the end of the file
                    if (inputStream.BaseStream.Position != inputStream.BaseStream.Length)
                    {
                        // read a byte and add it in, hoping to find a match at the next spot
                        var nextByte = inputStream.ReadByte();
                        currentBlock.Enqueue(nextByte);
                        currentSum.Rotate(oldestByte, nextByte);
                    }
                    else
                    {
                        // if we are at the end of the file, then just rollout the oldestByte and see if we have a match for the partial block
                        // (this could happen if we match the last block in the original file)
                        currentSum.Rollout(oldestByte);
                    }

                    if (currentCommand.Kind == CommandKind.Literal && currentCommand.Literal.Count < MaxBlockSize)
                    {
                        // if we already have a literal command, just append a new byte on
                        currentCommand.Literal.Add(oldestByte);
                    }
                    else
                    {
                        // otherwise we have to emit the current command and start a new literal command
                        if (currentCommand.Kind != CommandKind.Reserved)
                        {
                            yield return currentCommand;
                        }

                        currentCommand = new OutputCommand
                        {
                            Kind = CommandKind.Literal,
                            Literal = new List<byte> {oldestByte}
                        };
                    }
                }
            }

            yield return currentCommand;
            yield return new OutputCommand
            {
                Kind = CommandKind.End
            };
        }

        private static void ReadNewBlock(BinaryReader inputStream, int blockLength, out Queue<byte> newBlock, out Blake32RollingChecksum newRollsum)
        {
            var newBlockBytes = inputStream.ReadBytes(blockLength);
            newBlock = new Queue<byte>(newBlockBytes);
            newRollsum = new Blake32RollingChecksum();
            newRollsum.Update(newBlockBytes);
        }
    }

    internal class BlockSignature
    {
        public ulong StartPos;
        public byte[] StrongSum;
        public int WeakSum;
    }

    internal struct OutputCommand
    {
        public CommandKind Kind;
        public ulong Length;
        public List<byte> Literal;
        public ulong Position;
    }
}