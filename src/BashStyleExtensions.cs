using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CShellNet.BashStyle
{
    public static class BashStyleExtensions
    {
        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public static string cwd(this CShell shell)
        {
            return shell.CurrentFolder.FullName;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public static CShell cd(this CShell shell, string folderPath)
        {
            shell.ChangeFolder(folderPath);
            return shell;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public static CShell chdir(this CShell shell, string folderPath)
        {
            shell.ChangeFolder(folderPath);
            return shell;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public static CShell mkdir(this CShell shell, string folderPath)
        {
            return shell.CreateFolder(folderPath);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path</param>
        /// <returns></returns>
        public static CShell rmdir(this CShell shell, string folderPath, bool recursive = false)
        {
            return shell.DeleteFolder(folderPath, recursive);
        }

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public static IEnumerable<string> ls(this CShell shell, string searchPattern = null, bool recursive=false)
        {
            return shell.CurrentFolder.EnumerateFileSystemInfos(searchPattern ?? "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select( fileInfo => fileInfo.FullName);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path</param>
        /// <returns></returns>
        public static CShell rm(this CShell shell, string filePath)
        {
            return shell.DeleteFile(filePath);
        }

        /// <summary>
        /// Cat a file to stdout
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path</param>
        /// <returns></returns>
        public static Command cat(this CShell shell, string filePath)
        {
            return shell.ReadFile(filePath);
        }

    }
}
