using Medallion.Shell;
using System.Collections.Generic;

namespace CShellNet.CmdStyle
{
    public static class CmdStyleExtensions
    {

        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public static string cd(this Location location)
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
        public static Location md(this Location location, string folder)
        {
            return location.CreateFolder(folder);
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
        public static Location rd(this Location location, string folder, bool recursive = false)
        {
            return location.DeleteFolder(folder, recursive);
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
        public static IEnumerable<string> dir(this Location location, string searchPattern = null, bool recursive = false)
        {
            return location.List(searchPattern, recursive);
        }

        /// <summary>
        /// push folder
        /// </summary>
        /// <param name="location"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static Location pushd(this Location location, string folder)
        {
            return location.PushFolder(folder);
        }

        /// <summary>
        /// pop folder
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static Location popd(this Location location)
        {
            return location.PopFolder();
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
