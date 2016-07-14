namespace BusinessLayer.Entities.Chunking
{
    public class Chunk
    {
        private readonly byte[] _checksum;
        private readonly byte[] _contents;
        private readonly int _size;
        private readonly byte[] _fileChecksum;

        public Chunk(byte[] checksum, byte[] contents, int size, byte[] fileChecksum)
        {
            _checksum = checksum;
            _contents = contents;
            _size = size;
            _fileChecksum = fileChecksum;
        }

        public byte[] GetChecksum()
        {
            return _checksum;
        }

        public byte[] GetContents()
        {
            return _contents;
        }

        public int GetSize()
        {
            return _size;
        }

        public byte[] GetFileChecksum()
        {
            return _fileChecksum;
        }
    }
}
