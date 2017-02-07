using System;
using System.Collections.Generic;
using System.IO;

namespace AzureSync
{
    public static class FileUtility
    {
        public static void GetFolderFilesRecursive(List<string> list, string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                list.Add(file);
            }

            foreach (var subdir in Directory.GetDirectories(dir))
            {
                GetFolderFilesRecursive(list, subdir);
            }
        }

        public static string GetAzurePath(string containerName, string file)
        {
            // the container name is the root, for example azuresync
            // file is the full file path, for example C:\azuresync\test\file1.txt
            // we need an azure path like test/file1.txt

            if (!Path.IsPathRooted(file))
                throw new ArgumentException("A rooted path is expected.", "file");

            var f = file;

            while (Path.GetFileName(f) != containerName)
            {
                if (string.IsNullOrEmpty(f))
                    break;

                f = Path.GetDirectoryName(f);
            }

            string result;

            if (string.IsNullOrEmpty(f))
                result = file.Replace(Directory.GetDirectoryRoot(file), string.Empty);
            else
                result = file.Substring(f.Length + 1);

            return result.Replace("\\", "/");
        }
    }
}
