using System.Collections.Generic;
using BusinessLayer.Entities.Chunking;

namespace BusinessLayer.Implementation.Chunking
{
    public interface IChunker
    {
        List<Chunk> ChunkFile(string fileName);
    }
}
