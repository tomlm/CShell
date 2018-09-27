using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CShellNet
{
    /// <summary>
    /// Maintain a folder location with history and stack 
    /// </summary>
    public class Location
    {
        public Location()
        {
            _currentFolder = System.Environment.CurrentDirectory;
            this.FolderHistory = new List<string>();
            this.FolderStack = new Stack<string>();
            this.FolderHistory.Add(_currentFolder);
        }

        /// <summary>
        /// Current directory
        /// </summary>
        private string _currentFolder;
        public string CurrentFolder
        {
            get
            {
                return _currentFolder;
            }
            set
            {
                if (!Path.IsPathRooted(value))
                {
                    throw new ArgumentException("You can only set a full path via CurrentFolder property");
                }

                _currentFolder = Path.GetFullPath(value ?? throw new ArgumentNullException(nameof(CurrentFolder)));
                Environment.CurrentDirectory = _currentFolder;
                if (_currentFolder != FolderHistory.LastOrDefault())
                {
                    FolderHistory.Add(_currentFolder);
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
        public Location ChangeFolder(string relativePath)
        {
            this.CurrentFolder = normalizePath(relativePath);
            return this;
        }

        /// <summary>
        /// Create Folder
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public Location CreateFolder(string folder)
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
        public Location DeleteFolder(string folder, bool recursive = false)
        {
            folder = normalizePath(folder);
            Directory.Delete(folder, recursive);
            return this;
        }

        /// <summary>
        /// list files/folders in current location
        /// </summary>
        /// <param name="searchPattern">optional search pattern</param>
        /// <returns>list of paths</returns>
        public IEnumerable<string> List(string searchPattern = null, bool recursive = false)
        {
            if (searchPattern != null)
            {
                return Directory.EnumerateFileSystemEntries(this.CurrentFolder, searchPattern, (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            return Directory.EnumerateFileSystemEntries(this.CurrentFolder, "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// list files in folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public IEnumerable<string> ListFiles(string searchPattern = null, bool recursive = false)
        {
            if (searchPattern != null)
            {
                return Directory.EnumerateFiles(this.CurrentFolder, searchPattern, (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }

            return Directory.EnumerateFiles(this.CurrentFolder, "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// list folders in folder
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ListFolders(string searchPattern = null, bool recursive = false)
        {
            if (searchPattern != null)
            {
                return Directory.EnumerateDirectories(this.CurrentFolder, searchPattern, (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }

            return Directory.EnumerateDirectories(this.CurrentFolder, "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Change to a folder and add it to the stack
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public Location PushFolder(string folder)
        {
            var oldFolder = this.CurrentFolder;
            ChangeFolder(folder);
            this.FolderStack.Push(oldFolder);
            return this;
        }

        /// <summary>
        /// Pop a folder off the stack and change the current directory to it
        /// </summary>
        /// <returns></returns>
        public Location PopFolder()
        {
            this.CurrentFolder = this.FolderStack.Pop();
            return this;
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
                return Path.GetFullPath(Path.Combine(this.CurrentFolder, folder));
            }
        }
    }
}
