using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CShellNet
{
    /// <summary>
    /// Remembers how the terminal was set up, so a child process that dies badly cannot leave it
    /// unusable.
    /// </summary>
    /// <remarks>
    /// A full screen program turns off echo and line input on the way in and restores them on the
    /// way out -- unless it is killed, in which case the terminal is left silently swallowing
    /// keystrokes and the user has to type a command they cannot see to fix it. Since Exec exists
    /// to run exactly that sort of program, and to be interrupted, it puts the settings back.
    /// </remarks>
    internal sealed class ConsoleState : IDisposable
    {
        private const int STD_INPUT_HANDLE = -10;
        private const int STD_OUTPUT_HANDLE = -11;

        private readonly IntPtr stdIn;
        private readonly IntPtr stdOut;
        private readonly uint? inMode;
        private readonly uint? outMode;
        private readonly string sttyState;

        private ConsoleState(IntPtr stdIn, IntPtr stdOut, uint? inMode, uint? outMode, string sttyState)
        {
            this.stdIn = stdIn;
            this.stdOut = stdOut;
            this.inMode = inMode;
            this.outMode = outMode;
            this.sttyState = sttyState;
        }

        /// <summary>
        /// Snapshot the current terminal settings. Returns an object that restores them on Dispose.
        /// </summary>
        internal static ConsoleState Capture()
        {
            // No console attached means nothing to protect: output is going to a pipe or a file.
            if (Console.IsInputRedirected && Console.IsOutputRedirected)
            {
                return new ConsoleState(IntPtr.Zero, IntPtr.Zero, null, null, null);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var stdIn = GetStdHandle(STD_INPUT_HANDLE);
                var stdOut = GetStdHandle(STD_OUTPUT_HANDLE);

                return new ConsoleState(
                    stdIn,
                    stdOut,
                    GetConsoleMode(stdIn, out uint modeIn) ? modeIn : (uint?)null,
                    GetConsoleMode(stdOut, out uint modeOut) ? modeOut : (uint?)null,
                    null);
            }

            // `stty -g` prints the whole terminal state in a form stty itself can restore, which
            // avoids a pile of termios interop for the one thing we actually need.
            return new ConsoleState(IntPtr.Zero, IntPtr.Zero, null, null, ReadSttyState());
        }

        public void Dispose()
        {
            try
            {
                if (this.inMode.HasValue)
                {
                    SetConsoleMode(this.stdIn, this.inMode.Value);
                }

                if (this.outMode.HasValue)
                {
                    SetConsoleMode(this.stdOut, this.outMode.Value);
                }

                if (this.sttyState != null)
                {
                    RunStty(this.sttyState);
                }
            }
            catch
            {
                // Restoring is a courtesy. Failing at it must not replace whatever the caller was
                // actually doing with an exception about terminal modes.
            }
        }

        private static string ReadSttyState()
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo("stty", "-g")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }))
                {
                    var state = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    return process.ExitCode == 0 && state.Length > 0 ? state : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void RunStty(string state)
        {
            using (var process = Process.Start(new ProcessStartInfo("stty", state)
            {
                UseShellExecute = false,
            }))
            {
                process.WaitForExit();
            }
        }

        // Plain DllImport rather than LibraryImport: every parameter is blittable, so the source
        // generator buys nothing and would force AllowUnsafeBlocks on the whole assembly.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
