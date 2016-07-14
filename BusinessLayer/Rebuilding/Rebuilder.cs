using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using BusinessLayer.Entities.Chunking;
using BusinessLayer.Utility;

namespace BusinessLayer.Rebuilding
{
    public class Rebuilder
    {
        private static readonly string OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\seadrive\output";
        private static readonly int ChunkSize = Convert.ToInt32(ConfigurationManager.AppSettings["chunkSize"]);

        public Rebuilder()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }
        }

        public void RebuildFile(List<Chunk> chunks, string filename)
        {
            byte[] data = new byte[0];
            data = chunks.Select(chunk => chunk.GetContents()).Aggregate(data, ByteUtility.Combine);


            //Write the file and pray it works
            string destination = OutputFolder + @"\" + filename;
#if DEBUG
            Console.WriteLine("output filename is: " + destination);
#endif
            
            FileStream file = File.Open(destination, FileMode.Create);
            file.Write(data, 0, data.Length);
            file.Close();
        }
    }
}
