using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CShellNet
{
    /// <summary>
    /// CShellEx is global class which gives you ability to write a CShell script as global functions
    /// Usage:
    /// global using CShellNet.CShellEx
    /// </summary>
    public static class Globals
    {
        private static CShell _shell = new CShell();

        public static bool ThrowOnError { get => _shell.ThrowOnError; set => _shell.ThrowOnError = value; }

        public static bool Echo { get => _shell.Echo; set => _shell.Echo = value; }

        /// <summary>
        /// Reset global shell state.
        /// </summary>
        /// <param name="startFolder"></param>
        public static void ResetShell(string startFolder=null)
        {
            _shell = new CShell(startFolder);
        }

        /// <summary>
        /// Run a process
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Command Run(String executable, params Object[] arguments)
            => _shell.Run(executable, arguments);

        /// <summary>
        /// Run a process with options.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="options">options function</param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Command Run(Action<Shell.Options> options, string executable, params Object[] arguments)
            => _shell.Run(options, executable, arguments);

        /// <summary>
        /// Start a process detached 
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Command Start(string executable, params Object[] arguments)
            => _shell.Start(executable, arguments);

        /// <summary>
        /// Start a process detached.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Command Start(Action<Shell.Options> options, string executable, params Object[] arguments)
            => _shell.Start(options, executable, arguments);

        /// <summary>
        /// Run a cmd/bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public static Command Cmd(string cmd)
            => _shell.Cmd(cmd);

        /// <summary>
        /// Run a bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public static Command Bash(string cmd)
            => _shell.Bash(cmd);

        /// <summary>
        /// Current folder
        /// </summary>
        public static DirectoryInfo CurrentFolder { get => _shell.CurrentFolder; set => _shell.CurrentFolder = value; }

        /// <summary>
        /// History of folders 
        /// </summary>
        /// <remarks>Every time CurrentFolder is changed the path is placed in the folder history</remarks>
        public static List<string> FolderHistory { get => _shell.FolderHistory; }

        /// <summary>
        /// Stack of paths (only modifed by PushFolder or PopFolder)
        /// </summary>
        public static Stack<string> FolderStack { get => _shell.FolderStack; }

        /// <summary>
        /// Change Current Folder 
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell cd(string folderPath)
            => _shell.cd(folderPath);

        /// <summary>
        /// get current working directory
        /// </summary>
        /// <returns></returns>
        public static string cd()
            => _shell.cd();

        /// <summary>
        /// change current working directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell chdir(string folderPath)
            => _shell.chdir(folderPath);


        /// <summary>
        /// Turns lines of text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Command echo(IEnumerable<string> lines)
            => _shell.echo(lines);

        /// <summary>
        /// Turns text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Command echo(string text)
            => _shell.echo(text);

        /// <summary>
        /// Turns text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Command echo(TextReader textReader)
            => _shell.echo(textReader);

        /// <summary>
        /// copy file or folder 
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target File or folder</param>
        /// <returns></returns>
        public static CShell copy(string sourcePath, string targetPath, bool overwrite = false, bool recursive = false)
            => _shell.copy(sourcePath, targetPath, overwrite, recursive);

        /// <summary>
        /// rename file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public static CShell rename(string sourcePath, string targetPath)
            => _shell.rename(sourcePath, targetPath);

        /// <summary>
        /// move file or folder
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target file or folder</param>
        /// <returns></returns>
        public static CShell move(string sourcePath, string targetPath)
            => _shell.move(sourcePath, targetPath);

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell md(string folderPath)
            => _shell.md(folderPath);

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell mkdir(string folderPath)
            => _shell.mkdir(folderPath);

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell rd(string folderPath, bool recursive = false)
            => _shell.rd(folderPath, recursive);

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell rmdir(string folderPath, bool recursive = false)
            => _shell.rmdir(folderPath, recursive);

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public static IEnumerable<string> dir(string searchPattern = null, bool recursive = false)
            => _shell.dir(searchPattern, recursive);

        /// <summary>
        /// push folder
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell pushd(string folderPath)
            => _shell.PushFolder(folderPath);

        /// <summary>
        /// pop folder
        /// </summary>
        /// <param name="shell"></param>
        /// <returns></returns>
        public static CShell popd()
            => _shell.popd();

        /// <summary>
        /// type a file to stdout suitable for piping
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static Command type(string filePath)
            => _shell.type(filePath);

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell delete(string filePath)
            => _shell.del(filePath);

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell del(string filePath)
            => _shell.del(filePath);

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public static CShell erase(string filePath)
            => _shell.erase(filePath);

        /// <summary>
        /// Cat a file to stdout
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path</param>
        /// <returns></returns>
        public static Command cat(string filePath)
            => _shell.cat(filePath);

        /// <summary>
        /// Copy a Folder 
        /// </summary>
        /// <param name="sourceFolderPath">absolute or relative path to a source folder</param>
        /// <param name="targetFolderPath">absolute or relative path to a target folder</param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public static CShell CopyFolder(string sourceFolderPath, string targetFolderPath, bool recursive = true)
            => _shell.copy(sourceFolderPath, targetFolderPath, recursive);

        /// <summary>
        /// Change to a folder and add it to the stack
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public static CShell PushFolder(string folderPath)
            => _shell.PushFolder(folderPath);

        /// <summary>
        /// Pop a folder off the stack and change the current directory to it
        /// </summary>
        /// <returns></returns>
        public static CShell PopFolder()
            => _shell.PopFolder();

        /// <summary>
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="filePath">absolute or relative path to file</param>
        /// <returns></returns>
        public static Command ReadFile(string filePath)
            => _shell.ReadFile(filePath);

        /// <summary>
        /// Resolve a path relative to CurrentFolder
        /// </summary>
        /// <param name="absoluteOrReltivePath">absolute or relative path</param>
        /// <returns></returns>
        public static string ResolvePath(string absoluteOrReltivePath)
            => _shell.ResolvePath(absoluteOrReltivePath);
    }
}
