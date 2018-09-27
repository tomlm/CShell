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
        /// <returns></returns>
        public static CShell cd(this CShell shell, string folder)
        {
            shell.ChangeFolder(folder);
            return shell;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <returns></returns>
        public static CShell chdir(this CShell shell, string folder)
        {
            shell.ChangeFolder(folder);
            return shell;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static CShell md(this CShell shell, string folder)
        {
            return shell.CreateFolder(folder);
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static CShell mkdir(this CShell shell, string folder)
        {
            return shell.CreateFolder(folder);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static CShell rd(this CShell shell, string folder, bool recursive = false)
        {
            return shell.DeleteFolder(folder, recursive);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static CShell rmdir(this CShell shell, string folder, bool recursive = false)
        {
            return shell.DeleteFolder(folder, recursive);
        }

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public static IEnumerable<string> dir(this CShell shell, string searchPattern = null, bool recursive = false)
        {
            return shell.CurrentFolder.EnumerateFileSystemInfos(searchPattern, (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(fileInfo => fileInfo.Name);
        }

        /// <summary>
        /// push folder
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static CShell pushd(this CShell shell, string folder)
        {
            return shell.PushFolder(folder);
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
        /// <param name="path">path to file</param>
        /// <returns></returns>
        public static Command type(this CShell shell, string path)
        {
            return shell.ReadFile(path);
        }
    }
}
