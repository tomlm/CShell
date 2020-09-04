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
        private bool _echo = true;

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
                {
                    CurrentFolder = new DirectoryInfo(startingFolder);
                }
                else
                {
                    CurrentFolder = new DirectoryInfo(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, startingFolder)));
                }
            }
            else
            {
                CurrentFolder = new DirectoryInfo(Environment.CurrentDirectory);
            }
        }

        public bool ThrowOnError { get; set; } = true;


        /// <summary>
        /// Run a process
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Run(String executable, params Object[] arguments)
        {
            if (this._echo)
            {
                Console.WriteLine($"{executable} {String.Join(" ", arguments)}");
            }

            return Command.Run(executable, arguments, SetCommandOptions);
        }

        /// <summary>
        /// Run a cmd/bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public Command Cmd(string cmd)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (this._echo)
                {
                    Console.WriteLine(cmd);
                }

                cmd = $"/C {cmd}";
                if (!_echo)
                {
                    cmd = $"/Q {cmd}";
                }

                return Command.Run("cmd.exe", new string[] { cmd }, SetCommandOptions);
            }
            else
            {
                return Bash(cmd);
            }
        }

        /// <summary>
        /// Run a bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public Command Bash(string cmd)
        {
            if (this._echo)
            {
                Console.WriteLine(cmd);
            }

            var escapedArgs = cmd.Replace("\"", "\\\"");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Command.Run("bash.exe", $"-c", escapedArgs);
            }
            else
            {
                return Command.Run("/bin/bash", "-c", escapedArgs);
            }
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
        /// Turn echo on and off 
        /// </summary>
        /// <param name="echo">true to have command echoed</param>
        /// <returns></returns>
        public CShell echo(bool echo)
        {
            this._echo = echo;
            return this;
        }

        /// <summary>
        /// Change Current Folder 
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell cd(string folderPath)
        {
            this.CurrentFolder = new DirectoryInfo(ResolvePath(folderPath));
            return this;
        }

        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public string cd()
        {
            return this.CurrentFolder.FullName;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell chdir(string folderPath)
        {
            this.cd(folderPath);
            return this;
        }

        /// <summary>
        /// copy file or folder 
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target File or folder</param>
        /// <returns></returns>
        public CShell copy(string sourcePath, string targetPath, bool overwrite = false, bool recursive = false)
        {
            if (Directory.Exists(sourcePath))
            {
                return CopyFolder(sourcePath, targetPath, recursive);
            }

            if (Directory.Exists(targetPath))
                targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, targetPath, overwrite);
            return this;
        }

        /// <summary>
        /// rename file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public CShell rename(string sourcePath, string targetPath)
        {
            return move(sourcePath, targetPath);
        }

        /// <summary>
        /// move file or folder
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target file or folder</param>
        /// <returns></returns>
        public CShell move(string sourcePath, string targetPath)
        {
            sourcePath = this.ResolvePath(sourcePath);
            targetPath = this.ResolvePath(targetPath);
            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, targetPath);
                return this;
            }
            else
            {
                if (Directory.Exists(targetPath))
                    targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                File.Move(sourcePath, targetPath);
                return this;
            }
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell md(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            return this;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell mkdir(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            return this;
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rd(string folderPath, bool recursive = false)
        {
            Directory.Delete(folderPath, recursive);
            return this;
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rmdir(string folderPath, bool recursive = false)
        {
            Directory.Delete(folderPath, recursive);
            return this;
        }

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public IEnumerable<string> dir(string searchPattern = null, bool recursive = false)
        {
            return this.CurrentFolder.EnumerateFileSystemInfos(searchPattern ?? "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(fileInfo => fileInfo.Name);
        }

        /// <summary>
        /// push folder
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell pushd(string folderPath)
        {
            return this.PushFolder(folderPath);
        }

        /// <summary>
        /// pop folder
        /// </summary>
        /// <param name="shell"></param>
        /// <returns></returns>
        public CShell popd()
        {
            return this.PopFolder();
        }

        /// <summary>
        /// type a file to stdout suitable for piping
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public Command type(string filePath)
        {
            return this.ReadFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell delete(string filePath)
        {
            File.Delete(filePath);
            return this;
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell del(string filePath)
        {
            File.Delete(filePath);
            return this;
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell erase(string filePath)
        {
            File.Delete(filePath);
            return this;
        }

        /// <summary>
        /// Cat a file to stdout
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path</param>
        /// <returns></returns>
        public Command cat(string filePath)
        {
            return this.ReadFile(filePath);
        }

        /// <summary>
        /// Copy a Folder 
        /// </summary>
        /// <param name="sourceFolderPath">absolute or relative path to a source folder</param>
        /// <param name="targetFolderPath">absolute or relative path to a target folder</param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public CShell CopyFolder(string sourceFolderPath, string targetFolderPath, bool recursive = true)
        {
            var sourcePath = ResolvePath(sourceFolderPath);
            var targetPath = ResolvePath(targetFolderPath);
            CopyFolder(sourcePath, targetPath);

            void CopyFolder(string srcFolder, string destFolder)
            {
                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                string[] files = Directory.GetFiles(srcFolder);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest);
                }
                string[] folders = Directory.GetDirectories(srcFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);
                }
            }
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
            cd(folderPath);
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
        /// <param name="filePath">absolute or relative path to file</param>
        /// <returns></returns>
        public Command ReadFile(string filePath)
        {
            var path = ResolvePath(filePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] args = new string[] { "/c", "type", path };
                return Command.Run("cmd.exe", args, SetCommandOptions);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] args = new string[] { path };
                return Command.Run("cat", args, SetCommandOptions);
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

        /// <summary>
        /// Override this to control the MedallionShell options for .Run()
        /// </summary>
        /// <param name="options"></param>
        public virtual void SetCommandOptions(Shell.Options options)
        {
            options.StartInfo((psi) =>
                {
                    // set working folder
                    psi.WorkingDirectory = this.CurrentFolder.FullName;
                })
                .ThrowOnError(this.ThrowOnError);
        }

    }
}
