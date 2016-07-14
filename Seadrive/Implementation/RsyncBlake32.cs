using System.IO;
using BusinessLayer.Implementation.rsync;

namespace Seadrive.Implementation
{
    public class RsyncBlake32
    {
        public static void ComputeSignature(string filePath, string signatureOutputPath)
        {
            using (FileStream file = new FileStream(filePath, FileMode.Open))
            {
                using (var signature = Blake32Rsync.ComputeSignature(file))
                {
                    using (var signatureFile = File.Create(signatureOutputPath))
                    {
                        signature.Seek(0, SeekOrigin.Begin);
                        signature.CopyTo(signatureFile);
                    }
                }
            }
        }

        public static void ComputeDelta(string newFilePath, string signaturePath, string deltaOutputPath)
        {
            using (var file = new FileStream(newFilePath, FileMode.Open))
            {
                using (var signature = new FileStream(signaturePath, FileMode.Open))
                {
                    using (var delta = Blake32Rsync.ComputeDelta(signature, file))
                    {
                        using (var deltaFile = File.Create(deltaOutputPath))
                        {
                            delta.CopyTo(deltaFile);
                        }
                    }
                }
            }
        }

        public static void ComputeDeltaFromFiles(string originalFilePath, string newFilePath, string deltaOutputPath)
        {
            using (var fileA = new FileStream(originalFilePath, FileMode.Open))
            {
                var signature = Blake32Rsync.ComputeSignature(fileA);

                using (var fileB = new FileStream(newFilePath, FileMode.Open))
                {
                    using (var delta = Blake32Rsync.ComputeDelta(signature, fileB))
                    {
                        using (var deltaFile = File.Create(deltaOutputPath))
                        {
                            delta.CopyTo(deltaFile);
                        }
                    }
                }
            }
        }

        public static void ApplyDelta(string originalFilePath, string deltaPath, string newFileOutputPath)
        {
            using (var originalFile = new FileStream(originalFilePath, FileMode.Open))
            {
                using (var delta = new FileStream(deltaPath, FileMode.Open))
                {
                    using (var resultStream = Blake32Rsync.ApplyDelta(originalFile, delta))
                    {
                        using (var newFile = File.Create(newFileOutputPath))
                        {
                            newFile.Seek(0, SeekOrigin.Begin);
                            resultStream.CopyTo(newFile);
                        }
                    }
                }
            }
        }
    }
}
