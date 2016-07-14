using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BusinessLayer.Utility
{
    public static class FileUtility
    {

        public static List<String> GetAllFiles(String directory)
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToList();
        }

        public static bool GrantAccess(string fullPath)
        {
            DirectoryInfo dInfo = new DirectoryInfo(fullPath);
            DirectorySecurity dSecurity = dInfo.GetAccessControl();
            dSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
            dInfo.SetAccessControl(dSecurity);
            return true;
        }

        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            var accessControlList = Directory.GetAccessControl(path);
            if (accessControlList == null)
                return false;
            var accessRules = accessControlList.GetAccessRules(true, true,
                                        typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in accessRules.Cast<FileSystemAccessRule>().Where(rule => (FileSystemRights.Write & rule.FileSystemRights) == FileSystemRights.Write))
            {
                switch (rule.AccessControlType)
                {
                    case AccessControlType.Allow:
                        writeAllow = true;
                        break;
                    case AccessControlType.Deny:
                        writeDeny = true;
                        break;
                }
            }

            return writeAllow && !writeDeny;
        }

        public static bool ByteArrayToFile(string fileName, byte[] byteArray)
        {
            try
            {
                // Open file for reading
                FileStream fileStream =
                   new FileStream(fileName, FileMode.Create,
                                            FileAccess.Write);
                
                fileStream.Write(byteArray, 0, byteArray.Length);

                // close file stream
                fileStream.Close();

                return true;
            }
            catch (Exception exception)
            {
                // Error
                Console.WriteLine("Exception caught in process: {0}",
                                  exception);
            }

            // error occured, return false
            return false;
        }
    }
}
