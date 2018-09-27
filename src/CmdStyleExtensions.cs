using Medallion.Shell;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CShellNet.CmdStyle
{
    public static class CmdStyleExtensions
    {
        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public static string cd(this CShell shell)
        {
            return shell.CurrentFolder.FullName;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell cd(this CShell shell, string folderPath)
        {
            shell.ChangeFolder(folderPath);
            return shell;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell chdir(this CShell shell, string folderPath)
        {
            shell.ChangeFolder(folderPath);
            return shell;
        }

        /// <summary>
        /// copy file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public static CShell copy(this CShell shell, string sourcePath, string targetPath, bool overwrite=false)
        {
            return shell.CopyFile(sourcePath, targetPath, overwrite);
        }

        /// <summary>
        /// rename file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public static CShell rename(this CShell shell, string sourcePath, string targetPath)
        {
            return shell.MoveFile(sourcePath, targetPath);
        }

        /// <summary>
        /// move file or folder
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target file or folder</param>
        /// <returns></returns>
        public static CShell move(this CShell shell, string sourcePath, string targetPath)
        {
            sourcePath = shell.ResolvePath(sourcePath);
            targetPath = shell.ResolvePath(targetPath);
            if (Directory.Exists(sourcePath))
                return shell.MoveFolder(sourcePath, targetPath);
            else 
                return shell.MoveFile(sourcePath, targetPath);
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell md(this CShell shell, string folderPath)
        {
            return shell.CreateFolder(folderPath);
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell mkdir(this CShell shell, string folderPath)
        {
            return shell.CreateFolder(folderPath);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell rd(this CShell shell, string folderPath, bool recursive = false)
        {
            return shell.DeleteFolder(folderPath, recursive);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
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
        public static IEnumerable<string> dir(this CShell shell, string searchPattern = null, bool recursive = false)
        {
            return shell.CurrentFolder.EnumerateFileSystemInfos(searchPattern ?? "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(fileInfo => fileInfo.Name);
        }

        /// <summary>
        /// push folder
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell pushd(this CShell shell, string folderPath)
        {
            return shell.PushFolder(folderPath);
        }

        /// <summary>
        /// pop folder
        /// </summary>
        /// <param name="shell"></param>
        /// <returns></returns>
        public static CShell popd(this CShell shell)
        {
            return shell.PopFolder();
        }

        /// <summary>
        /// type a file to stdout suitable for piping
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static Command type(this CShell shell, string filePath)
        {
            return shell.ReadFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell delete(this CShell shell, string filePath)
        {
            return shell.DeleteFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell del(this CShell shell, string filePath)
        {
            return shell.DeleteFile(filePath);
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell erase(this CShell shell, string filePath)
        {
            return shell.DeleteFile(filePath);
        }
    }
}
