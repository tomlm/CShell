using Medallion.Shell;
using System;
using System.Collections;
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
        public CShell()
        {
            this.FolderHistory = new List<string>();
            this.FolderStack = new Stack<string>();
            this.FolderHistory.Add(Environment.CurrentDirectory);
        }

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
        /// Current directory
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
        /// Change Folder (supporting relative changes
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public CShell ChangeFolder(string relativePath)
        {
            this.CurrentFolder = new DirectoryInfo(normalizePath(relativePath));
            return this;
        }

        /// <summary>
        /// Create Folder
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public CShell CreateFolder(string folder)
        {
            folder = normalizePath(folder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return this;
        }

        /// <summary>
        /// delete a Folder 
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public CShell DeleteFolder(string folder, bool recursive = false)
        {
            folder = normalizePath(folder);
            Directory.Delete(folder, recursive);
            return this;
        }

        /// <summary>
        /// Change to a folder and add it to the stack
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public CShell PushFolder(string folder)
        {
            var oldFolder = this.CurrentFolder;
            ChangeFolder(folder);
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
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Command ReadFile(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(this.CurrentFolder.FullName, path));
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
                psi.WorkingDirectory = this.CurrentFolder.FullName;
            });
        }

        private string normalizePath(string folder)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (Path.IsPathRooted(folder))
            {
                return Path.GetFullPath(folder);
            }
            else
            {
                return Path.GetFullPath(Path.Combine(this.CurrentFolder.FullName, folder));
            }
        }

    }
}
