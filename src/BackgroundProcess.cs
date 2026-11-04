using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CShellNet
{
    /// <summary>
    /// Starts a process that shares nothing with the one starting it: no window, none of its open
    /// handles or streams, and nothing that ties it to its terminal.
    /// </summary>
    /// <remarks>
    /// Process.Start cannot do this on its own. On Windows it always creates the child with
    /// bInheritHandles=TRUE, so the child holds a copy of every inheritable handle the parent has
    /// open -- including the stdout pipe of whoever is capturing the parent's output, which then
    /// never sees end-of-file until the child exits. On Unix the child gets the parent's stdin,
    /// stdout and stderr, and stays in the parent's session, so it is signalled when the terminal
    /// closes.
    /// </remarks>
    internal static class BackgroundProcess
    {
        public static int Start(string executable, IEnumerable<string> arguments, string workingDirectory, IDictionary<string, string> environment)
        {
            return OperatingSystem.IsWindows()
                ? StartWindows(executable, arguments, workingDirectory, environment)
                : StartUnix(executable, arguments, workingDirectory, environment);
        }

        // ---------------------------------------------------------------- Unix

        // Runs as `sh -c <script> sh <program> <args...>`. Every exec replaces the process rather
        // than forking, so the id Process.Start reports is the program's own. setsid makes a new
        // session, which is what keeps the terminal's hang-up and Ctrl+C away from it. macOS has
        // no setsid command, so there it falls back to nohup, which only ignores the hang-up.
        private const string UnixScript =
            "exec </dev/null >/dev/null 2>&1\n" +
            "if command -v setsid >/dev/null; then exec setsid \"$@\"; fi\n" +
            "exec nohup \"$@\"\n";

        private static int StartUnix(string executable, IEnumerable<string> arguments, string workingDirectory, IDictionary<string, string> environment)
        {
            var startInfo = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(UnixScript);
            startInfo.ArgumentList.Add("sh");
            // Found here rather than by the script, so a missing program throws like Run() and
            // Exec() do instead of starting a process that exits 127.
            startInfo.ArgumentList.Add(FindUnixProgram(executable, workingDirectory));
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var variable in environment)
            {
                if (variable.Value == null)
                {
                    startInfo.Environment.Remove(variable.Key);
                }
                else
                {
                    startInfo.Environment[variable.Key] = variable.Value;
                }
            }

            using (var process = Process.Start(startInfo))
            {
                return process.Id;
            }
        }

        private static string FindUnixProgram(string executable, string workingDirectory)
        {
            if (executable.Contains('/'))
            {
                var path = Path.GetFullPath(executable, workingDirectory);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            else
            {
                var directories = (Environment.GetEnvironmentVariable("PATH") ?? String.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
                foreach (var directory in directories)
                {
                    var path = Path.Combine(directory, executable);
                    if (File.Exists(path) && IsExecutable(path))
                    {
                        return path;
                    }
                }
            }

            throw new Win32Exception(2, $"Unable to start '{executable}': no such program.");
        }

        private static bool IsExecutable(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & anyExecute) != 0;
        }

        // ---------------------------------------------------------------- Windows

        private const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint CREATE_NO_WINDOW = 0x08000000;

        // bInheritHandles=FALSE is the point: the child gets none of our handles. CREATE_NO_WINDOW
        // gives a console program a console nobody can see, which its own children then share,
        // rather than no console at all (DETACHED_PROCESS), which would make each of them pop up
        // a window of its own. The new process group keeps our Ctrl+C away from it.
        private static int StartWindows(string executable, IEnumerable<string> arguments, string workingDirectory, IDictionary<string, string> environment)
        {
            // CreateProcess resolves a relative path against our directory, not the child's.
            if ((executable.Contains('\\') || executable.Contains('/')) && !Path.IsPathRooted(executable))
            {
                executable = Path.GetFullPath(executable, workingDirectory);
            }

            var commandLine = new StringBuilder();
            AppendArgument(commandLine, executable);
            foreach (var argument in arguments)
            {
                AppendArgument(commandLine, argument);
            }

            var startup = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
            var block = environment.Count == 0 ? null : EnvironmentBlock(environment);
            if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP | CREATE_UNICODE_ENVIRONMENT,
                    block, workingDirectory, ref startup, out var info))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Unable to start '{executable}': {new Win32Exception(error).Message}");
            }

            CloseHandle(info.hThread);
            CloseHandle(info.hProcess);
            return info.dwProcessId;
        }

        // The rules every MSVCRT program, and .NET, uses to split a command line back into
        // arguments: quote anything empty or containing whitespace or a quote, escape quotes with
        // a backslash, and double the backslashes that come before a quote or the closing quote.
        internal static void AppendArgument(StringBuilder commandLine, string argument)
        {
            if (commandLine.Length != 0)
            {
                commandLine.Append(' ');
            }

            if (argument.Length != 0 && !argument.Any(c => Char.IsWhiteSpace(c) || c == '"'))
            {
                commandLine.Append(argument);
                return;
            }

            commandLine.Append('"');
            var i = 0;
            while (i < argument.Length)
            {
                var c = argument[i++];
                if (c == '\\')
                {
                    var backslashes = 1;
                    while (i < argument.Length && argument[i] == '\\')
                    {
                        i++;
                        backslashes++;
                    }

                    if (i == argument.Length)
                    {
                        commandLine.Append('\\', backslashes * 2);
                    }
                    else if (argument[i] == '"')
                    {
                        commandLine.Append('\\', backslashes * 2 + 1).Append('"');
                        i++;
                    }
                    else
                    {
                        commandLine.Append('\\', backslashes);
                    }

                    continue;
                }

                if (c == '"')
                {
                    commandLine.Append('\\').Append('"');
                    continue;
                }

                commandLine.Append(c);
            }

            commandLine.Append('"');
        }

        private static string EnvironmentBlock(IDictionary<string, string> overrides)
        {
            var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                variables[(string)entry.Key] = (string)entry.Value;
            }

            foreach (var variable in overrides)
            {
                if (variable.Value == null)
                {
                    variables.Remove(variable.Key);
                }
                else
                {
                    variables[variable.Key] = variable.Value;
                }
            }

            var block = new StringBuilder();
            foreach (var variable in variables)
            {
                block.Append(variable.Key).Append('=').Append(variable.Value).Append('\0');
            }

            return block.Append('\0').ToString();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(string lpApplicationName, StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            string lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
