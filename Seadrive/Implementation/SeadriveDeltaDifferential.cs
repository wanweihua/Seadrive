/*-
 * The underlying BSDIFF implementation was originally implemented in C by Colin Percival
 * 
 * https://github.com/mendsley/bsdiff/blob/master/bsdiff.c
 * Copyright 2003-2005 Colin Percival
 * Copyright 2012 Matthew Endsley
 * All rights reserved
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted providing that the following conditions 
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
 * OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
 * IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */


using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using BusinessLayer.Extensions;
using BusinessLayer.Implementation.DeltaDifferential;
using BusinessLayer.Utility;
using ICSharpCode.SharpZipLib.BZip2;

namespace Seadrive.Implementation
{
    public static class SeadriveDeltaDifferential
    {
        internal const int HeaderSize = 32;
        internal const long Signature = 0x3034464649445342; //"BSDIFF40"

        public delegate Stream OpenPatchStream(long offset, long length);

        public static void CreatePatch(byte[] oldData, byte[] newData, Stream output)
        {
            // check arguments
            Contract.Requires<ArgumentNullException>(oldData != null, "oldData cannot be null.");
            Contract.Requires<ArgumentNullException>(newData != null, "newData cannot be null.");
            Contract.Requires<ArgumentNullException>(output != null, "output cannot be null.");
            if (!output.CanSeek && !output.CanWrite)
            {
                throw new Exception("Stream needs to be seekable and writable");
            }

            /* Header is
                0	8	 "BSDIFF40"
                8	8	length of bzip2ed ctrl block
                16	8	length of bzip2ed diff block
                24	8	length of new file
               File is
                0	32	Header
                32	??	Bzip2ed ctrl block
                ??	??	Bzip2ed diff block
                ??	??	Bzip2ed extra block */
            var header = new byte[HeaderSize];
            WriteHeader(newData, header);

            var startPosition = output.Position;
            output.Write(header, 0, header.Length);

            var suffixArray = Sais.SuffixSort(oldData);

            using (var msControl = new MemoryStream())
            using (var msDiff = new MemoryStream())
            using (var msExtra = new MemoryStream())
            {
                using (var ctrlStream = GetEncodingStream(msControl, true))
                using (var diffStream = GetEncodingStream(msDiff, true))
                using (var extraStream = GetEncodingStream(msExtra, true))
                {
                    var scan = 0;
                    var pos = 0;
                    var len = 0;
                    var lastscan = 0;
                    var lastpos = 0;
                    var lastoffset = 0;

                    while (scan < newData.Length)
                    {
                        var oldscore = 0;

                        for (var scsc = scan += len; scan < newData.Length; scan++)
                        {
                            len = Search(suffixArray, oldData, newData.Slice(scan), 0, oldData.Length, out pos);

                            for (; scsc < scan + len; scsc++)
                            {
                                if ((scsc + lastoffset < oldData.Length) && (oldData[scsc + lastoffset] == newData[scsc]))
                                    oldscore++;
                            }

                            if ((len == oldscore && len != 0) || (len > oldscore + 8))
                                break;

                            if ((scan + lastoffset < oldData.Length) && (oldData[scan + lastoffset] == newData[scan]))
                                oldscore--;
                        }

                        if (len != oldscore || scan == newData.Length)
                        {
                            var s = 0;
                            var sf = 0;
                            var lenf = 0;
                            for (var i = 0; (lastscan + i < scan) && (lastpos + i < oldData.Length); )
                            {
                                if (oldData[lastpos + i] == newData[lastscan + i])
                                    s++;
                                i++;
                                if (s * 2 - i > sf * 2 - lenf)
                                {
                                    sf = s;
                                    lenf = i;
                                }
                            }

                            var lenb = 0;
                            if (scan < newData.Length)
                            {
                                s = 0;
                                var sb = 0;
                                for (var i = 1; (scan >= lastscan + i) && (pos >= i); i++)
                                {
                                    if (oldData[pos - i] == newData[scan - i])
                                        s++;
                                    if (s * 2 - i > sb * 2 - lenb)
                                    {
                                        sb = s;
                                        lenb = i;
                                    }
                                }
                            }

                            if (lastscan + lenf > scan - lenb)
                            {
                                var overlap = (lastscan + lenf) - (scan - lenb);
                                s = 0;
                                var ss = 0;
                                var lens = 0;
                                for (var i = 0; i < overlap; i++)
                                {
                                    if (newData[lastscan + lenf - overlap + i] == oldData[lastpos + lenf - overlap + i])
                                        s++;
                                    if (newData[scan - lenb + i] == oldData[pos - lenb + i])
                                        s--;
                                    if (s > ss)
                                    {
                                        ss = s;
                                        lens = i + 1;
                                    }
                                }

                                lenf += lens - overlap;
                                lenb -= lens;
                            }

                            WriteDiff(oldData, newData, lenf, diffStream, lastscan, lastpos);

                            var extraLength = (scan - lenb) - (lastscan + lenf);
                            if (extraLength > 0)
                            {
                                extraStream.Write(newData, lastscan + lenf, extraLength);
                            }

                            var buf = new byte[8];

                            lastpos = WriteCOntrolBlock(buf, lenf, ctrlStream, extraLength, pos, lenb, lastpos, scan, ref lastscan, ref lastoffset);
                        }
                    }
                }

                //write compressed ctrl data
                msControl.Seek(0, SeekOrigin.Begin);
                msControl.CopyTo(output);

                // compute size of compressed ctrl data
                header.WriteLongAt(8, msControl.Length);

                // write compressed diff data
                msDiff.Seek(0, SeekOrigin.Begin);
                msDiff.CopyTo(output);

                // compute size of compressed diff data
                header.WriteLongAt(16, msDiff.Length);

                // write compressed extra data
                msExtra.Seek(0, SeekOrigin.Begin);
                msExtra.CopyTo(output);
            }

            // seek to the beginning, write the header, then seek back to end
            var endPosition = output.Position;
            output.Position = startPosition;
            output.Write(header, 0, header.Length);
            output.Position = endPosition;
        }

        private static void WriteHeader(IReadOnlyCollection<byte> newData, byte[] header)
        {
            header.WriteLong(Signature);
            header.WriteLongAt(24, newData.Count);
        }

        private static void WriteDiff(IReadOnlyList<byte> oldData, IReadOnlyList<byte> newData, int lenf, Stream diffStream, int lastscan, int lastpos)
        {
            for (var i = 0; i < lenf; i++)
            {
                diffStream.WriteByte((byte)(newData[lastscan + i] - oldData[lastpos + i]));
            }
        }

        private static int WriteCOntrolBlock(byte[] buf, int lenf, Stream ctrlStream, int extraLength, int pos, int lenb, int lastpos, int scan, ref int lastscan,
            ref int lastoffset)
        {
            buf.WriteLong(lenf);
            ctrlStream.Write(buf, 0, 8);

            buf.WriteLong(extraLength);
            ctrlStream.Write(buf, 0, 8);

            buf.WriteLong((pos - lenb) - (lastpos + lenf));
            ctrlStream.Write(buf, 0, 8);

            lastscan = scan - lenb;
            lastpos = pos - lenb;
            lastoffset = pos - scan;
            return lastpos;
        }

        internal static Stream GetEncodingStream(Stream stream, bool output)
        {
            if (output)
                return new BZip2OutputStream(stream) { IsStreamOwner = false };
            return new BZip2InputStream(stream);
        }

        private static int Search(IList<int> I, byte[] oldData, IList<byte> newData, int start, int end, out int pos)
        {
            while (true)
            {
                if (end - start < 2)
                {
                    var startLength = ByteUtility.MatchLength(oldData.Slice(I[start]), newData);
                    var endLength = ByteUtility.MatchLength(oldData.Slice(I[end]), newData);

                    if (startLength > endLength)
                    {
                        pos = I[start];
                        return startLength;
                    }

                    pos = I[end];
                    return endLength;
                }

                var midPoint = start + (end - start) / 2;
                if (ByteUtility.CompareBytes(oldData.Slice(I[midPoint]), newData) < 0)
                {
                    start = midPoint;
                    continue;
                }

                end = midPoint;
            }
        }

        public static void ApplyPatch(byte[] input, byte[] diff, Stream output)
        {
            OpenPatchStream openPatchStream = (uOffset, uLength) =>
            {
                var offset = (int)uOffset;
                var length = (int)uLength;
                return new MemoryStream(diff, offset,
                    uLength > 0
                        ? length
                        : diff.Length - offset);
            };

            Stream controlStream, diffStream, extraStream;
            var newSize = CreatePatchStreams(openPatchStream, out controlStream, out diffStream, out extraStream);

            // prepare to read three parts of the patch in parallel
            ApplyInternal(newSize, new MemoryStream(input), controlStream, diffStream, extraStream, output);
        }

        public static void ApplyPatch(Stream input, OpenPatchStream openPatchStream, Stream output)
        {
            Stream controlStream, diffStream, extraStream;
            var newSize = CreatePatchStreams(openPatchStream, out controlStream, out diffStream, out extraStream);

            // prepare to read three parts of the patch in parallel
            ApplyInternal(newSize, input, controlStream, diffStream, extraStream, output);
        }

        private static long CreatePatchStreams(OpenPatchStream openPatchStream, out Stream ctrl, out Stream diff, out Stream extra)
        {
            // read header
            long controlLength, diffLength, newSize;
            using (var patchStream = openPatchStream(0, HeaderSize))
            {
                // check patch stream capabilities
                if (!patchStream.CanRead || !patchStream.CanSeek)
                {
                    throw new Exception("patch stream must be read and seekable");
                }

                var header = new byte[HeaderSize];
                patchStream.Read(header, 0, HeaderSize);

                var signature = header.ReadLong();
                ValidateSignature(signature);

                controlLength = header.ReadLongAt(8);
                diffLength = header.ReadLongAt(16);
                newSize = header.ReadLongAt(24);

                if (controlLength < 0 || diffLength < 0 || newSize < 0)
                    throw new InvalidOperationException("Corrupt patch");
            }

            // prepare to read three parts of the patch in parallel
            Stream
                compressedControlStream = openPatchStream(HeaderSize, controlLength),
                compressedDiffStream = openPatchStream(HeaderSize + controlLength, diffLength),
                compressedExtraStream = openPatchStream(HeaderSize + controlLength + diffLength, 0);

            // decompress each part (to read it)
            ctrl = GetEncodingStream(compressedControlStream, false);
            diff = GetEncodingStream(compressedDiffStream, false);
            extra = GetEncodingStream(compressedExtraStream, false);

            return newSize;
        }

        private static void ValidateSignature(long signature)
        {
            if (signature != Signature)
            {
                throw new InvalidOperationException("Corrupt patch");
            }
        }

        private static void ApplyInternal(long newSize, Stream input, Stream ctrl, Stream diff, Stream extra, Stream output)
        {
            if (!input.CanRead || !input.CanSeek || !input.CanWrite)
            {
                throw new Exception("Inputstream must be readable, writeable and seekable");
            }

            using (ctrl)
            using (diff)
            using (extra)
            using (var inputReader = new BinaryReader(input))
                while (output.Position < newSize)
                {
                    var addSize = ctrl.ReadLong();
                    var copySize = ctrl.ReadLong();
                    var seekAmount = ctrl.ReadLong();

                    // sanity-check
                    if (output.Position + addSize > newSize)
                        throw new InvalidOperationException("Corrupt patch");

                    // read diff string in chunks
                    foreach (var newData in diff.BufferedRead(addSize))
                    {
                        var inputData = inputReader.ReadBytes(newData.Length);

                        // add old data to diff string
                        for (var i = 0; i < newData.Length; i++)
                            newData[i] += inputData[i];

                        output.Write(newData, 0, newData.Length);
                    }

                    // sanity-check
                    if (output.Position + copySize > newSize)
                        throw new InvalidOperationException("Corrupt patch");

                    // read extra string in chunks
                    foreach (var extraData in extra.BufferedRead(copySize))
                    {
                        output.Write(extraData, 0, extraData.Length);
                    }

                    // adjust position
                    input.Seek(seekAmount, SeekOrigin.Current);
                }
        }
    }
}
