using Medallion.Shell;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CShellNet
{
    /// <summary>
    /// Adds methods to CShell which are like CMD commands (cd, md, etc.)
    /// </summary>
    public class CmdShell : CShell
    {
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
        public CShell cd(string folderPath)
        {
            this.ChangeFolder(folderPath);
            return this;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell chdir(string folderPath)
        {
            this.ChangeFolder(folderPath);
            return this;
        }

        /// <summary>
        /// copy file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public CShell copy(string sourcePath, string targetPath, bool overwrite = false)
        {
            return this.CopyFile(sourcePath, targetPath, overwrite);
        }

        /// <summary>
        /// rename file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public CShell rename(string sourcePath, string targetPath)
        {
            return this.MoveFile(sourcePath, targetPath);
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
                return this.MoveFolder(sourcePath, targetPath);
            }
            else
            {
                return this.MoveFile(sourcePath, targetPath);
            }
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell md(string folderPath)
        {
            return this.CreateFolder(folderPath);
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell mkdir(string folderPath)
        {
            return this.CreateFolder(folderPath);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rd(string folderPath, bool recursive = false)
        {
            return this.DeleteFolder(folderPath, recursive);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rmdir(string folderPath, bool recursive = false)
        {
            return this.DeleteFolder(folderPath, recursive);
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
            return this.DeleteFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell del(string filePath)
        {
            return this.DeleteFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell erase(string filePath)
        {
            return this.DeleteFile(filePath);
        }
    }
}
