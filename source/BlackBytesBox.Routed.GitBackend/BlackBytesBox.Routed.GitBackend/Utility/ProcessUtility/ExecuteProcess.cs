using System.IO;
using System;

namespace BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility
{
    public static partial class ProcessUtility
    {
        /// <summary>
        /// Executes a process with the specified filename, arguments, and working directory.
        /// </summary>
        /// <remarks>
        /// This method runs the specified process without shell execution, captures both standard output and error streams,
        /// and returns these outputs along with the process exit code.
        /// </remarks>
        /// <param name="fileName">The executable to run.</param>
        /// <param name="arguments">The command-line arguments for the process.</param>
        /// <param name="workingDirectory">The directory in which to execute the process.</param>
        /// <returns>A tuple containing the standard output, standard error, and exit code.</returns>
        /// <example>
        /// var result = ExecuteProcess("git", "-C \"C:\\gitlocal\" clone http://localhostuser/gitrepos/MyProject.git", @"C:\gitlocal");
        /// Console.WriteLine($"Output: {result.Output}");
        /// Console.WriteLine($"Error: {result.Error}");
        /// Console.WriteLine($"Exit Code: {result.ExitCode}");
        /// </example>
        public static (string Output, string Error, int ExitCode) ExecuteProcess(string fileName, string arguments, string workingDirectory)
        {
            // Configure the process start information.
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,                // Required for redirecting streams.
                RedirectStandardOutput = true,          // Enable capturing standard output.
                RedirectStandardError = true,           // Enable capturing standard error.
                RedirectStandardInput = false
            };

            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = psi;
                process.Start();

                // Capture the output streams.
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();
                int exitCode = process.ExitCode;

                // Return all captured output in a tuple.
                return (output, error, exitCode);
            }
        }
    }
}
