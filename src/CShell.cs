using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CShellNet
{
    /// <summary>
    /// CShell is class which provides the environmental equivelent of a CMD or BASH environment
    /// * current directory
    /// * Environment variables
    /// * Ability to invoke process and pipe input between processes (via MedallionShell library)
    /// </summary>
    public class CShell
    {
        /// <summary>
        /// Start a shell
        /// </summary>
        /// <param name="startingFolder">(OPTIONAL) if passed in, this will be the initial folder</param>
        public CShell(string startingFolder = null)
        {
            this.FolderHistory = new List<string>();
            this.FolderStack = new Stack<string>();
            if (startingFolder != null)
            {
                if (Path.IsPathRooted(startingFolder))
                    CurrentFolder = new DirectoryInfo(startingFolder);
                else
                    CurrentFolder = new DirectoryInfo(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, startingFolder)));
            }
            else
            {
                CurrentFolder = new DirectoryInfo(Environment.CurrentDirectory);
            }
        }

        /// <summary>
        /// Run a process
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Run(String executable, params Object[] arguments)
        {
            return Command.Run(executable, arguments, setCommandOptions);
        }


        /// <summary>
        /// Current folder
        /// </summary>
        private DirectoryInfo _currentFolder;
        public DirectoryInfo CurrentFolder
        {
            get
            {
                return _currentFolder;
            }
            set
            {
                _currentFolder = value;
                Environment.CurrentDirectory = value.FullName;
                if (Environment.CurrentDirectory != FolderHistory.LastOrDefault())
                {
                    FolderHistory.Add(Environment.CurrentDirectory);
                }
            }
        }

        /// <summary>
        /// History of folders 
        /// </summary>
        /// <remarks>Every time CurrentFolder is changed the path is placed in the folder history</remarks>
        public List<string> FolderHistory { get; private set; }

        /// <summary>
        /// Stack of paths (only modifed by PushFolder or PopFolder)
        /// </summary>
        public Stack<string> FolderStack { get; private set; }

        /// <summary>
        /// Change Current Folder 
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell ChangeFolder(string folderPath)
        {
            this.CurrentFolder = new DirectoryInfo(ResolvePath(folderPath));
            return this;
        }

        /// <summary>
        /// Create Folder
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell CreateFolder(string folderPath)
        {
            folderPath = ResolvePath(folderPath);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return this;
        }

        /// <summary>
        /// delete a Folder 
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public CShell DeleteFolder(string folderPath, bool recursive = false)
        {
            folderPath = ResolvePath(folderPath);
            Directory.Delete(folderPath, recursive);
            return this;
        }

        /// <summary>
        /// Change to a folder and add it to the stack
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell PushFolder(string folderPath)
        {
            var oldFolder = this.CurrentFolder;
            ChangeFolder(folderPath);
            this.FolderStack.Push(oldFolder.FullName);
            return this;
        }

        /// <summary>
        /// Pop a folder off the stack and change the current directory to it
        /// </summary>
        /// <returns></returns>
        public CShell PopFolder()
        {
            this.CurrentFolder = new DirectoryInfo(this.FolderStack.Pop());
            return this;
        }

        /// <summary>
        /// Delete a file relative to current folder
        /// </summary>
        /// <param name="filePath">absolute or relative path to file</param>
        /// <returns></returns>
        public CShell DeleteFile(string filePath)
        {
            var path = ResolvePath(filePath);
            File.Delete(path);
            return this;
        }

        /// <summary>
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="filePath">absolute or relative path to file</param>
        /// <returns></returns>
        public Command ReadFile(string filePath)
        {
            var path = ResolvePath(filePath);

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

        /// <summary>
        /// Resolve a path relative to CurrentFolder
        /// </summary>
        /// <param name="absoluteOrReltivePath">absolute or relative path</param>
        /// <returns></returns>
        public string ResolvePath(string absoluteOrReltivePath)
        {
            if (absoluteOrReltivePath == null)
            {
                throw new ArgumentNullException(nameof(absoluteOrReltivePath));
            }

            if (Path.IsPathRooted(absoluteOrReltivePath))
            {
                return Path.GetFullPath(absoluteOrReltivePath);
            }
            else
            {
                return Path.GetFullPath(Path.Combine(this.CurrentFolder.FullName, absoluteOrReltivePath));
            }
        }

        private void setCommandOptions(Shell.Options options)
        {
            options.StartInfo((psi) =>
            {
                // set working folder
                psi.WorkingDirectory = this.CurrentFolder.FullName;
            });
        }

    }
}
