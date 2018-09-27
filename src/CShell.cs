using Medallion.Shell;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CShellNet
{
    /// <summary>
    /// CShell is class which provides the environmental equivelent of a CMD or BASH environment
    /// * current directory
    /// * Environment variables
    /// * Ability to invoke process and pipe input between processes (via MedallionShell library)
    /// </summary>
    public class CShell : Location
    {

        public CShell()
        {
            this.Environment = new Dictionary<string, string>();
            var env = System.Environment.GetEnvironmentVariables();
            foreach (var kv in env)
            {
                var keyValue = (DictionaryEntry)kv;
                this.Environment.Add(keyValue.Key.ToString(), keyValue.Value?.ToString());
            }
        }

        /// <summary>
        /// Environment variables (these are passed to processes as they are invoked
        /// </summary>
        public Dictionary<string, string> Environment { get; }

        /// <summary>
        /// Run executable
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Run(String executable, params Object[] arguments)
        {
            return Command.Run(executable, arguments, setCommandOptions);
        }

        /// <summary>
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Command ReadFile(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(this.CurrentFolder, path));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] args = new string[] { "/c", "type", path };
                return Command.Run("cmd.exe", args, setCommandOptions);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] args = new string[] { path };
                return Command.Run("cat", args, setCommandOptions);
            }
            throw new ArgumentOutOfRangeException("Unknown operating system");
        }


        private void setCommandOptions(Shell.Options options)
        {
            options.StartInfo((psi) =>
            {
                // set working folder
                psi.WorkingDirectory = this.CurrentFolder;

                // set environment
                foreach (var keyValue in this.Environment)
                {
                    psi.Environment[keyValue.Key] = keyValue.Value;
                }
            });
        }
    }
}
