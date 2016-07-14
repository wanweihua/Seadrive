using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BusinessLayer.Entities.Transport;
using Data_Abstraction_Layer.Transport;
using Data_Abstraction_Layer.Transport.Models;

namespace PrimaryServer
{
    public class Program
    {
        static void Main(string[] args)
        {
            const bool isTest = true;
            if (isTest)
            {
                GenerateTestSet();
            }

            PrimaryServerDaemon daemon = new PrimaryServerDaemon();
            daemon.StartServer("8050");
            
            Console.ReadLine();
        }

        static void GenerateTestSet()
        {
             string inputPath = ConfigurationManager.AppSettings["testSetPath"];
            GenerateDatabaseTestSet(inputPath);
        }

        //Ignore subdir for this test-set, its a 20 byte patch
        public static void GenerateDatabaseTestSet(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);

            foreach (var file in fileEntries)
            {
                GenerateDatabaseOptions(file);
            }
        }

        static void GenerateDatabaseOptions(string filename)
        {
            if (filename.Contains("_modified"))
            {
                return;
            }

            DirtyFile file = new DirtyFile();
            byte[] fileContents = File.ReadAllBytes(filename);
            using (var sha1 = SHA1.Create())
            {
                file.FileChecksum = sha1.ComputeHash(fileContents);
            }

            byte[] fakeOldChecksum = file.FileChecksum;
            fakeOldChecksum[0] = (byte)(fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[2] = (byte)(fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[4] = (byte)(fakeOldChecksum[0] + 0x1);
            fakeOldChecksum[6] = (byte)(fakeOldChecksum[0] + 0x1);

            file.OldChecksum = fakeOldChecksum;
            string guid = Path.GetFileName(filename) + "guid";
            file.Guid = guid;
            file.RelativePath = filename;
            TransportUnitOfWork unitOfWork = new TransportUnitOfWork();
            VirtualFile fileEntry = new VirtualFile {FileChecksum = fakeOldChecksum, Guid = file.Guid, RelativePath = file.RelativePath};
            unitOfWork.VirtualFileRepository.Insert(fileEntry);
            unitOfWork.Save();
        }
    }
}
