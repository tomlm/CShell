using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CShellNet
{
    /// <summary>
    /// Options for <see cref="CShell.Exec(Action{ExecOptions}, string, object[])"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately smaller than <c>Shell.Options</c>. An attached process owns the console, so
    /// there is nothing to redirect, pipe, or encode, and no output to throw on. What is left is
    /// where it runs, what it inherits, and how to stop it.
    /// </remarks>
    public class ExecOptions
    {
        internal string workingDirectory;
        internal System.Threading.CancellationToken cancellationToken = System.Threading.CancellationToken.None;
        internal Dictionary<string, string> environment = new Dictionary<string, string>();
        internal Action<ProcessStartInfo> startInfo;

        /// <summary>
        /// Run in this folder instead of the shell's current folder.
        /// </summary>
        public ExecOptions WorkingDirectory(string path)
        {
            this.workingDirectory = path;
            return this;
        }

        /// <summary>
        /// Set an environment variable for the process, on top of the ones it inherits.
        /// </summary>
        public ExecOptions EnvironmentVariable(string name, string value)
        {
            this.environment[name] = value;
            return this;
        }

        /// <summary>
        /// Set several environment variables for the process.
        /// </summary>
        public ExecOptions EnvironmentVariables(IEnumerable<KeyValuePair<string, string>> variables)
        {
            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    this.environment[variable.Key] = variable.Value;
                }
            }

            return this;
        }

        /// <summary>
        /// Kill the process when the token is cancelled.
        /// </summary>
        /// <remarks>
        /// Note that Ctrl+C reaches an attached process directly, because it shares this console.
        /// A token is for stopping it from somewhere else in the program.
        /// </remarks>
        public ExecOptions CancellationToken(System.Threading.CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            return this;
        }

        /// <summary>
        /// Adjust the ProcessStartInfo directly, for anything not covered here.
        /// </summary>
        /// <remarks>
        /// Turning redirection back on here defeats the purpose of Exec and will leave the process
        /// without a console. Use <see cref="CShell.Run(string, object[])"/> if you want its output.
        /// </remarks>
        public ExecOptions StartInfo(Action<ProcessStartInfo> startInfo)
        {
            this.startInfo = startInfo;
            return this;
        }
    }
}
