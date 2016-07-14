using System.Collections.Generic;
using System.Linq;
using BusinessLayer.Entities.Chunking;
using Data_Abstraction_Layer.Deduplication.Models;

namespace SeaDrive.Utility
{
    //Misc utility
    public static class MiscUtility
    {
        public static List<CompressedChunk> ExtractCompressedChunksFromCacheEntries(List<CacheEntries> input)
        {
            List<CompressedChunk> output = input.Select(entry => new CompressedChunk(entry.FileChecksum, entry.BlockChecksum, entry.ZippedData, entry.BlockNum)).ToList();
            return output;
        } 
    }
}
