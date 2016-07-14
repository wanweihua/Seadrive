namespace BusinessLayer.Entities.Transport
{
    public class DirtyFile
    {
        public string Guid { get; set; }
        public string RelativePath { get; set; }
        public byte[] FileChecksum { get; set; }
        public byte[] Patch { get; set; }
        public byte[] OldChecksum { get; set; }
    }
}
