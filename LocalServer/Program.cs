using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BusinessLayer.Entities.Transport;
using Seadrive.Implementation;

namespace LocalServer
{
    class Program
    {
        static void Main(string[] args)
        {
            List<DirtyFile> dirtyFiles = GenerateTestSet();
            Seadrive.Implementation.LocalServer server = new Seadrive.Implementation.LocalServer();
            Client client = new Client("LS-UUID");
            Debug.WriteLine("c to connect, d to disconnect s to send");
            server.StartServer("8000", @ConfigurationManager.AppSettings.Get("syncFolder"));
            //Only for testing!
            while (true)
            {
                string key = Console.ReadLine();
                if (key.StartsWith("c"))
                {
                    client.Connect();
                }
                else if (key.StartsWith("d"))
                {
                    client.Disconnect();
                }
                else if (key.StartsWith("s"))
                {
                    //Create dirty files
                    //deliver
                    client.InitiateConnection(dirtyFiles);
                   // client.InitiateConnection();
                }
            }
        }

        static List<DirtyFile> GenerateTestSet()
        {
            string inputPath = @ConfigurationManager.AppSettings["macroBenchmarkFolder"];
            List<DirtyFile> retval = GenerateDiryFilesFromPath(inputPath);
            return retval;
        }

        //Ignore subdir for this test-set, its a 20 byte patch
        public static List<DirtyFile> GenerateDiryFilesFromPath(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);

            return fileEntries.Select(GenerateDirtyFile).Where(file => file != null).ToList();
        }

        static DirtyFile GenerateDirtyFile(string filename)
        {
            if (filename.Contains("_modified"))
            {
                return null;
            }

            DirtyFile file = new DirtyFile();
            byte[] fileContents = File.ReadAllBytes(filename);
            using (var sha1 = SHA1.Create())
            {
                file.FileChecksum = sha1.ComputeHash(fileContents);
            }

            byte[] fakeOldChecksum = file.FileChecksum;
            fakeOldChecksum[0] = (byte) (fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[2] = (byte) (fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[4] = (byte) (fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[6] = (byte) (fakeOldChecksum[0] + 0x1);

            file.OldChecksum = fakeOldChecksum;
            string guid = Path.GetFileName(filename) + "guid";
            file.Guid = guid;
            file.RelativePath = filename;
            file.Patch = FetchPatch(filename);
            return file;
        }

        static byte[] FetchPatch(string path)
        {
            string patchFolder = @ConfigurationManager.AppSettings["patchFolder"];
            string currentFilename = Path.GetFileNameWithoutExtension(path);
            string[] fileEntries = Directory.GetFiles(patchFolder);
            return (from file in fileEntries where currentFilename != null && file.Contains(currentFilename) select File.ReadAllBytes(file)).FirstOrDefault();
        }

    }
}
