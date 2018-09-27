using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.Text;

namespace CShellNet.BashStyle
{
    public static class BashStyleExtensions
    {
        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public static string cwd(this Location location)
        {
            return location.CurrentFolder;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <returns></returns>
        public static Location cd(this Location location, string folder)
        {
            location.ChangeFolder(folder);
            return location;
        }

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <returns></returns>
        public static Location chdir(this Location location, string folder)
        {
            location.ChangeFolder(folder);
            return location;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static Location mkdir(this Location location, string folder)
        {
            return location.CreateFolder(folder);
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static Location rmdir(this Location location, string folder, bool recursive = false)
        {
            return location.DeleteFolder(folder, recursive);
        }

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public static IEnumerable<string> ls(this Location location, string searchPattern = null, bool recursive=false)
        {
            return location.List(searchPattern, recursive);
        }

        /// <summary>
        /// Cat a file to stdout
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="path">path to file</param>
        /// <returns></returns>
        public static Command cat(this CShell shell, string path)
        {
            return shell.ReadFile(path);
        }

    }
}
