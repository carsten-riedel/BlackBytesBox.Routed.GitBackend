using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility
{
    public static partial class ProcessUtility
    {
        // Existing LocateExecutable method ...

        /// <summary>
        /// Recursively searches for an executable in the specified directory tree.
        /// </summary>
        /// <remarks>
        /// On Windows, if the provided filename lacks an extension, ".exe" is appended for the search.
        /// This method searches all subdirectories of the specified starting directory for the executable.
        /// It returns a tuple with the directory where the executable was found, the filename (with extension),
        /// and the fully qualified path to the executable.
        /// </remarks>
        /// <param name="fileName">The name of the executable file (e.g., "git" or "git.exe").</param>
        /// <param name="startDirectory">The root directory to start the search from.</param>
        /// <returns>
        /// A tuple containing:
        ///   - Directory: The directory in which the executable was found.
        ///   - FileName: The filename with the proper extension.
        ///   - FullPath: The fully qualified path to the executable.
        /// Returns null if the executable is not found.
        /// </returns>
        /// <example>
        /// var result = LocateExecutableRecursively("git", @"C:\Program Files");
        /// if (result != null)
        /// {
        ///     Console.WriteLine($"Directory: {result.Value.Directory}");
        ///     Console.WriteLine($"FileName: {result.Value.FileName}");
        ///     Console.WriteLine($"FullPath: {result.Value.FullPath}");
        /// }
        /// else
        /// {
        ///     Console.WriteLine("Executable not found in the specified directory tree.");
        /// }
        /// </example>
        public static (string Directory, string FileName, string FullPath)? LocateExecutableRecursively(string fileName, string startDirectory)
        {
            if (!Directory.Exists(startDirectory))
            {
                return null;
            }

            // On Windows, if the file name does not have an extension, append ".exe" for the search.
            string searchFileName = fileName;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Path.HasExtension(fileName))
            {
                searchFileName += ".exe";
            }

            foreach (var file in SafeEnumerateFiles(startDirectory, searchFileName))
            {
                string fullPath = Path.GetFullPath(file);
                string directory = Path.GetDirectoryName(fullPath)!;
                return (directory, searchFileName, fullPath);
            }

            // Executable not found in any of the directories.
            return null;
        }

        // Helper method to safely enumerate files in a directory tree.
        private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern)
        {
            Queue<string> dirs = new Queue<string>();
            dirs.Enqueue(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Dequeue();
                IEnumerable<string> files = Enumerable.Empty<string>();

                try
                {
                    files = Directory.EnumerateFiles(currentDir, searchPattern);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories without access rights.
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    // Skip if the directory was removed.
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                IEnumerable<string> subDirs = Enumerable.Empty<string>();
                try
                {
                    subDirs = Directory.EnumerateDirectories(currentDir);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    dirs.Enqueue(subDir);
                }
            }
        }
    }
}