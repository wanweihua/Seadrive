using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Seadrive.Implementation;

namespace PreliminaryExperiments
{
    class Program
    {
        static readonly List<string> RunningTimes = new List<string>();
        static readonly List<long> PatchSizes = new List<long>();
        private static readonly string InputFolder = ConfigurationManager.AppSettings["inputFolder"];
        static string _outputPath = ConfigurationManager.AppSettings["outputFolder"];
        private const TestCases TestCase = TestCases.Librsync;

        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
   //         AssaignPath();
  //          WriteCurrentTestHeader(true);
   //         WriteCurrentTestHeader(false);

            ProcessDirectory(InputFolder);

   //         WriteCurrentTestAverageResources(true);
   //         WriteCurrentTestAverageResources(false);

            Console.WriteLine("Experiment completed");
            Console.Beep();
            Console.ReadLine();
        }

        // https://msdn.microsoft.com/en-us/library/07wt70x2(v=vs.110).aspx
        // Process all files in the directory passed in, recurse on any directories 
        // that are found, and process the files they contain.
        public static void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
            {
                switch (TestCase)
                {
                    case TestCases.BsDiff:
                        CreateBinaryDiffPatchFile(fileName);
                        break;
                    case TestCases.OptimizedRsync:
                        break;
                    case TestCases.Librsync:
                        CreateSizeDifferentialBinder(fileName);
                        //CreateOptimizedRsyncDeltaFileFromPreviousVersion(fileName);
                        break;
                        case TestCases.SimpleDeduplication:
                        break;
                    default:
                        break;
                }
            }
                

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                ProcessDirectory(subdirectory);
        }

        public static string FetchModifiedFile(string targetDirectory, string targetFile)
        {
            string retval = "";
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries.Where(fileName => fileName.Contains(targetFile)))
            {
                retval = fileName;
            }
            return retval;
        }

        public static void CreateOptimizedRsyncDeltaFile(string path)
        {
            if (!path.Contains("_signature"))
            {
                return;
            }
            string currentDir = Path.GetDirectoryName(path);
            string modifiedVersionOfFile = Path.GetFileNameWithoutExtension(path);
            if (modifiedVersionOfFile.EndsWith("_signature"))
            {
                modifiedVersionOfFile = modifiedVersionOfFile.Replace("_signature", "");
            }
            modifiedVersionOfFile += "_modified";
            string modifiedFilePath = FetchModifiedFile(currentDir, modifiedVersionOfFile);
            string deltaFile = modifiedFilePath + "_delta";

            Stopwatch sw = new Stopwatch();
            sw.Start();
            RsyncBlake32.ComputeDelta(modifiedFilePath, path, deltaFile);
            sw.Stop();

            RunningTimes.Add(sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            FileInfo f = new FileInfo(deltaFile);
            PatchSizes.Add(f.Length);
            WriteCurrentInfo(deltaFile, sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), f.Length);
            Console.WriteLine("Processed file '{0}'.", path);
        }

        public static void CreateOptimizedRsyncDeltaFileFromPreviousVersion(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }

            string currentDir = Path.GetDirectoryName(path);

            //fetch modified file
            string modifiedVersionOfFile = Path.GetFileNameWithoutExtension(path);
            modifiedVersionOfFile += "_modified";
            string modifiedPath = FetchModifiedFile(currentDir, modifiedVersionOfFile);
            string deltaFile = Path.GetFileName(path) + @"_delta";

            //Calculate Patch File
            Stopwatch sw = new Stopwatch();
            string outputFullPath = _outputPath + deltaFile;

            sw.Start();
            RsyncBlake32.ComputeDeltaFromFiles(path, modifiedPath, deltaFile);
            sw.Stop();

            //Store data
            RunningTimes.Add(sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            FileInfo f = new FileInfo(deltaFile);
            PatchSizes.Add(f.Length);

            //Write current data
            WriteCurrentInfo(deltaFile, sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), f.Length);
            Console.WriteLine("Processed file '{0}'.", path);
        }

        private static void CreateBinderInfoOurRdiff(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }
            if (!(path.Contains("oversize_pdf_test_0")))
            {
                return;
            }
            string pathToModified = InputFolder + @"client_sync_folder\Binder3.pdf";
            string outputPath = _outputPath;
            //BIn DIFF
            Stopwatch sw = new Stopwatch();
            string deltaFile = Path.GetFileName(path) + @"_delta";
            sw.Start();
            RsyncBlake32.ComputeDeltaFromFiles(path, pathToModified, deltaFile);
            //LibrsyncStarterHelper.ComputeSignature(path, outputPath+"result");
            sw.Stop();
            long seadriveDiffElapsed = sw.ElapsedMilliseconds;
            FileInfo f = new FileInfo(deltaFile);
            long fSize = f.Length;
            //Write current data
            using (StreamWriter w = File.AppendText(_outputPath + "rSyncResults"))
            {
                w.WriteLine("time to create patch : " + seadriveDiffElapsed);
                w.WriteLine("size of patch in bytes: " + fSize);
            }

        }

        public static void CreateSizeDifferentialBinder(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }
                        if (!(path.Contains("oversize_pdf_test_0")))
            {
                return;
            }
            string pathToModified = InputFolder + @"client_sync_folder\Binder3.pdf";
            string currentDir = Path.GetDirectoryName(path);

            byte[] unmodifiedBytes = File.ReadAllBytes(path);
            byte[] modifiedBytes = File.ReadAllBytes(pathToModified);

            long diff = 0;
            var i = 0;
            for (i = 0; i < unmodifiedBytes.Length; i++)
            {
                if (i >= modifiedBytes.Length)
                {
                    break;
                }
                if (unmodifiedBytes[i] != modifiedBytes[i])
                {
                    diff++;
                }
            }

            if (i < modifiedBytes.Length)
            {
                for (; i < modifiedBytes.Length; i++)
                {
                    diff++;
                }
            }

            using (StreamWriter w = File.AppendText("File_differential"))
            {
                w.WriteLine("diff for: " + path + "with modified: " + pathToModified + " gives DIFF: " + diff);
            }
        }

        public static void CreateBinderInfoOurDiff(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }
            if (!(path.Contains("oversize_pdf_test_0")))
            {
                return;
            }
            string pathToModified = InputFolder + @"client_sync_folder\Binder3.pdf";
             string outputPath = _outputPath;
            //BIn DIFF
            Stopwatch sw = new Stopwatch();

            sw.Start();
            using (FileStream output = new FileStream(outputPath + "seadriveDiff", FileMode.Create))
            {
                SeadriveDeltaDifferential.CreatePatch(File.ReadAllBytes(path), File.ReadAllBytes(pathToModified), output);
            }
            sw.Stop();
            long seadriveDiffElapsed = sw.ElapsedMilliseconds;
            FileInfo f = new FileInfo(outputPath+"seadriveDiff");
            long fSize = f.Length;
            //Write current data
            using (StreamWriter w = File.AppendText(_outputPath +"seadriveDiffResults"))
            {
                w.WriteLine("time to create patch : " + seadriveDiffElapsed);
                w.WriteLine("size of patch in bytes: " + fSize);
            }

        }

        public static void CreateOptimizedRsyncSignatureFile(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }
            string signatureFile = Path.GetFileName(path) + @"_signature";
            string outputFullPath = _outputPath + signatureFile;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            RsyncBlake32.ComputeSignature(path, outputFullPath);
            sw.Stop();

            RunningTimes.Add(sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            FileInfo f = new FileInfo(outputFullPath);
            PatchSizes.Add(f.Length);
            WriteCurrentInfo(outputFullPath, sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), f.Length);
            Console.WriteLine("Processed file '{0}'.", path);

        }


        public static void CreateLibRsyncSignatureFile(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }
            string signatureFile = Path.GetFileName(path) + @"_signature";
            string outputFullPath = _outputPath + signatureFile;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            RsyncBlake32.ComputeSignature(path, outputFullPath);
            sw.Stop();

            RunningTimes.Add(sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            FileInfo f = new FileInfo(outputFullPath);
            PatchSizes.Add(f.Length);
            WriteCurrentInfo(outputFullPath, sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), f.Length);
            Console.WriteLine("Processed file '{0}'.", path);

        }

        // Insert logic for processing found files here.
        public static void CreateBinaryDiffPatchFile(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }

            string currentDir = Path.GetDirectoryName(path);

            //fetch modified file
            string modifiedVersionOfFile = Path.GetFileNameWithoutExtension(path);
            modifiedVersionOfFile += "_modified";
            string modifiedPath = FetchModifiedFile(currentDir, modifiedVersionOfFile);
            string patchFile = Path.GetFileName(path) + @"_patch";

            Console.WriteLine("FOUND MODIFIED");
            //Calculate Patch File
            Stopwatch sw = new Stopwatch();
            string outputFullPath = _outputPath + patchFile;

            sw.Start();
            using (FileStream output = new FileStream(outputFullPath, FileMode.Create))
            {
                SeadriveDeltaDifferential.CreatePatch(File.ReadAllBytes(path), File.ReadAllBytes(modifiedPath), output);
            }
            sw.Stop();

            //Store data
            RunningTimes.Add(sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            FileInfo f = new FileInfo(outputFullPath);
            PatchSizes.Add(f.Length);

            //Write current data
            WriteCurrentInfo(patchFile, sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), f.Length);
            Console.WriteLine("Processed file '{0}'.", path);
        }

        private static void WriteSizeDifferential(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }

            string currentDir = Path.GetDirectoryName(path);

            //fetch modified file
            string modifiedVersionOfFile = Path.GetFileNameWithoutExtension(path);
            modifiedVersionOfFile += "_modified";
            string modifiedPath = FetchModifiedFile(currentDir, modifiedVersionOfFile);
            string patchFile = Path.GetFileName(path) + @"_patch";

            FileInfo baseFileInfo = new FileInfo(path);
            FileInfo modifiedFileInfo = new FileInfo(modifiedPath);

            long diff = baseFileInfo.Length - modifiedVersionOfFile.Length;

            Console.WriteLine("FOUND MODIFIED");
            //Calculate Patch File
            Stopwatch sw = new Stopwatch();
            string outputFullPath = _outputPath + patchFile;
            using (StreamWriter w = File.AppendText("File_differential"))
            {
                w.WriteLine(path + " : " + diff);
            }

        }

        private static void WriteCurrentInfo(string patchFile, string elapsedMilliseconds, long fileLengthInBytes)
        {
            //Write current data
            using (StreamWriter w = File.AppendText(_outputPath + TestCase + "result_time.txt"))
            {
                w.WriteLine(patchFile + " : " + elapsedMilliseconds);
            }

            using (StreamWriter w = File.AppendText(_outputPath + TestCase + "result_patch_size.txt"))
            {
                w.WriteLine(patchFile + " : " + fileLengthInBytes);
            }
        }

        private static void WriteCurrentTestHeader(bool isTime)
        {
            if (isTime)
            {
                using (StreamWriter w = File.AppendText(_outputPath + TestCase +"result_time.txt"))
                {
                    w.WriteLine("Starting NEW RUN for " + TestCase + " Running times: ");
                }
            }
            else
            {
                using (StreamWriter w = File.AppendText(_outputPath + TestCase + "result_patch_size.txt"))
                {
                    w.WriteLine("Starting NEW RUN for " + TestCase + " Patch Sizes: ");
                }
            }
        }

        private static void WriteCurrentTestAverageResources(bool isTime)
        {
            if (isTime)
            {
                using (StreamWriter w = File.AppendText(_outputPath + TestCase + "result_time.txt"))
                {
                    double averageTimeToCreatePatch = RunningTimes.Sum(time => Convert.ToDouble(time));
                    averageTimeToCreatePatch /= RunningTimes.Count;
                    w.WriteLine("Average time to create a patch file in milliseconds: " + averageTimeToCreatePatch);
                }
            }
            else
            {
                using (StreamWriter w = File.AppendText(_outputPath + TestCase + "result_patch_size.txt"))
                {
                    long averagePatchSize = PatchSizes.Sum();
                    averagePatchSize /= PatchSizes.Count;
                    w.WriteLine("Average patch size: " + averagePatchSize);
                }
            }
        }

        private static void CalculateByteDifferences(string path)
        {
            if (path.Contains("_modified"))
            {
                return;
            }

            string currentDir = Path.GetDirectoryName(path);

            //fetch modified file
            string modifiedVersionOfFile = Path.GetFileNameWithoutExtension(path);
            modifiedVersionOfFile += "_modified";
            string modifiedPath = FetchModifiedFile(currentDir, modifiedVersionOfFile);

            byte[] unmodifiedBytes = File.ReadAllBytes(path);
            byte[] modifiedBytes = File.ReadAllBytes(modifiedPath);

            long diff = 0;
            var i = 0;
            for (i = 0; i < unmodifiedBytes.Length; i++)
            {
                if (i >= modifiedBytes.Length)
                {
                    break;
                }
                if (unmodifiedBytes[i] != modifiedBytes[i])
                {
                    diff++;
                }
            }

            if (i < modifiedBytes.Length)
            {
                for (; i < modifiedBytes.Length; i++)
                {
                    diff++;
                }
            }

            using (StreamWriter w = File.AppendText("File_differential"))
            {
                w.WriteLine("diff for: " + path + "with modified: " + modifiedPath + " gives DIFF: " + diff);
            }
        }

        // ReSharper disable once HeuristicUnreachableCode
        private static void AssaignPath()
        {
            switch (TestCase)
            {
                case TestCases.BsDiff:
                    _outputPath += @"bsdiff_output\";
                    break;
                case TestCases.OptimizedRsync:
                    break;
                case TestCases.Librsync:
                    _outputPath += @"librsync_output\";
                    break;
                case TestCases.SimpleDeduplication:
                    break;
                default:
                    break;
            }
        }

    }
}
