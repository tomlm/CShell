using Medallion.Shell;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CShellNet
{
    /// <summary>
    /// Adds lightweight methods to CShell which are "bash-like"
    /// </summary>
    public class BashShell : CShell
    {
        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public string cwd()
        {
            return this.CurrentFolder.FullName;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public CShell cd(string folderPath)
        {
            this.ChangeFolder(folderPath);
            return this;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public CShell chdir(string folderPath)
        {
            this.ChangeFolder(folderPath);
            return this;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public CShell mkdir(string folderPath)
        {
            return this.CreateFolder(folderPath);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
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
        public IEnumerable<string> ls(string searchPattern = null, bool recursive = false)
        {
            return this.CurrentFolder.EnumerateFileSystemInfos(searchPattern ?? "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(fileInfo => fileInfo.FullName);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path</param>
        /// <returns></returns>
        public CShell rm(string filePath)
        {
            return this.DeleteFile(filePath);
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

    }
}
