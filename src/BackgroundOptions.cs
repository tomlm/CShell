using System.Collections.Generic;

namespace CShellNet
{
    /// <summary>
    /// Options for <see cref="CShell.Background(System.Action{BackgroundOptions}, string, object[])"/>.
    /// </summary>
    /// <remarks>
    /// Smaller still than ExecOptions. A background process has no streams to redirect and is not
    /// waited for, so there is nothing to cancel either. What is left is where it runs and what
    /// environment it gets.
    /// </remarks>
    public class BackgroundOptions
    {
        internal string workingDirectory;
        internal Dictionary<string, string> environment = new Dictionary<string, string>();

        /// <summary>
        /// Run in this folder instead of the shell's current folder.
        /// </summary>
        public BackgroundOptions WorkingDirectory(string path)
        {
            this.workingDirectory = path;
            return this;
        }

        /// <summary>
        /// Set an environment variable for the process, on top of the ones it inherits. A null
        /// value removes the variable instead.
        /// </summary>
        public BackgroundOptions EnvironmentVariable(string name, string value)
        {
            this.environment[name] = value;
            return this;
        }

        /// <summary>
        /// Set several environment variables for the process.
        /// </summary>
        public BackgroundOptions EnvironmentVariables(IEnumerable<KeyValuePair<string, string>> variables)
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
    }
}
