namespace BusinessLayer.Implementation.rsync
{
    internal class Blake32RollingChecksum
    {
        private const byte RsCharOffset = 31;
        private ulong _sumPart2;
        private ulong _sumPartOne;
        public ulong Count { get; private set; }

        public int Digest
        {
            get { return (int) ((_sumPart2 << 16) | (_sumPartOne & 0xffff)); }
        }

        public void Update(byte[] buf)
        {
            int i;
            var s1 = _sumPartOne;
            var s2 = _sumPart2;

            Count += (ulong) buf.Length;
            for (i = 0; i < (buf.Length - 4); i += 4)
            {
                s2 += 4*(s1 + buf[i]) + 3u*buf[i + 1] +
                      2u*buf[i + 2] + buf[i + 3] + 10*RsCharOffset;
                s1 += ((uint) buf[i + 0] + buf[i + 1] + buf[i + 2] + buf[i + 3] +
                       4*RsCharOffset);
            }
            for (; i < buf.Length; i++)
            {
                s1 += (ulong) (buf[i] + RsCharOffset);
                s2 += s1;
            }

            _sumPartOne = s1;
            _sumPart2 = s2;
        }

        /// <summary>
        ///     This transforms the rolling sum by removing byteOut from the beginning of the block and adding
        ///     byteIn to the end.
        ///     Thus if the data before was a checksum for buf[0..n], it becomes a checksum for
        ///     buf[1..n+1], assuming byteOut=buf[0] and byteIn = buf[n+1]
        /// </summary>
        public void Rotate(byte byteOut, byte byteIn)
        {
            _sumPartOne += (ulong) (byteIn - byteOut);
            _sumPart2 += _sumPartOne - Count*(ulong) (byteOut + RsCharOffset);
        }

        public void Rollout(byte byteOut)
        {
            _sumPartOne -= (ulong) (byteOut - RsCharOffset);
            _sumPart2 -= Count*(ulong) (byteOut*RsCharOffset);
            Count--;
        }
    }
}