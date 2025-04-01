using System.IO;
using System;

namespace BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility
{
    public static partial class ProcessUtility
    {
        /// <summary>
        /// Locates an executable in the system PATH.
        /// </summary>
        /// <remarks>
        /// On Windows, if the provided filename lacks an extension, ".exe" is appended for the search.
        /// This method returns a tuple with the directory where the executable was found, the filename (with extension),
        /// and the fully qualified path to the executable.
        /// </remarks>
        /// <param name="fileName">The name of the executable file (e.g., "git" or "git.exe").</param>
        /// <returns>
        /// A tuple containing:
        ///   - Directory: The directory in which the executable was found.
        ///   - FileName: The filename with the proper extension.
        ///   - FullPath: The fully qualified path to the executable.
        /// Returns null if the executable is not found.
        /// </returns>
        /// <example>
        /// var result = LocateExecutable("git");
        /// if (result != null)
        /// {
        ///     Console.WriteLine($"Directory: {result.Value.Directory}");
        ///     Console.WriteLine($"FileName: {result.Value.FileName}");
        ///     Console.WriteLine($"FullPath: {result.Value.FullPath}");
        /// }
        /// else
        /// {
        ///     Console.WriteLine("Executable not found in the system PATH.");
        /// }
        /// </example>
        public static (string Directory, string FileName, string FullPath)? LocateExecutable(string fileName)
        {
            // On Windows, if the file name does not have an extension, append ".exe" for the search.
            string searchFileName = fileName;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Path.HasExtension(fileName))
            {
                searchFileName += ".exe";
            }

            // Retrieve the PATH environment variable.
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            // Split the PATH into individual directories.
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    string candidate = Path.Combine(path, searchFileName);
                    if (File.Exists(candidate))
                    {
                        // Get the fully qualified path.
                        string fullPath = Path.GetFullPath(candidate);
                        return (path, searchFileName, fullPath);
                    }
                }
                catch
                {
                    // Ignore exceptions and continue with the next directory.
                }
            }

            // Executable not found in any of the PATH directories.
            return null;
        }

    }
}
