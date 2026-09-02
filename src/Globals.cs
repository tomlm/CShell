using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;

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
        /// Where the Ask methods get their keystrokes when reading keys rather than lines.
        /// Null reads the console. See CShell.ReadKey.
        /// </summary>
        public static Func<ConsoleKeyInfo> ReadKey { get => _shell.ReadKey; set => _shell.ReadKey = value; }

        /// <summary>
        /// Whether the Ask methods draw their rich prompts or fall back to reading a typed line.
        /// Null, the default, decides by asking whether standard input is redirected, because
        /// Console.ReadKey() throws when it is. See CShell.RichPrompts.
        /// </summary>
        public static bool? RichPrompts { get => _shell.RichPrompts; set => _shell.RichPrompts = value; }

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
        /// <remarks>
        /// All three streams are redirected, which is what makes StandardOutput readable, and
        /// also what makes this the wrong method for a process that stops to ask the user
        /// something -- `claude setup-token`, `gh auth login`, ssh, anything with a terminal UI.
        /// Such a process ends up waiting on a stdin pipe that nothing will ever write to and
        /// nothing will ever close. It never exits, nothing is printed while it waits, and there
        /// is no way to answer the question it is stuck on.
        ///
        /// To run one of those, leave stdin and stderr on the console this shell is itself
        /// attached to and capture stdout alone. That is the `program | cat` shape: the question
        /// reaches the user and the answer reaches the process.
        ///
        ///     var result = await Run(opt => opt.StartInfo(psi =>
        ///     {
        ///         psi.RedirectStandardInput = false;
        ///         psi.RedirectStandardError = false;
        ///     }), "claude", "setup-token").AsResult();
        ///
        /// Captured still means unseen: a terminal UI draws itself on stdout, so it shows nothing
        /// at all while it waits, which looks exactly like the hang above. Print what to expect
        /// before calling it. Nothing is feeding stdin either, so RedirectFrom() and piping in do
        /// not apply to a call shaped like this.
        /// </remarks>
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
        /// Ask the user a question and return what they typed.
        /// </summary>
        /// <remarks>
        /// The Ask family is the script asking the user. For the other direction -- a process
        /// that asks the user something itself -- see the remarks on Run(). All of them throw if
        /// standard input is at end of stream, rather than answering for someone who is not
        /// there. See CShell.AskText().
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <returns>what the user typed, trimmed; empty if they just pressed enter</returns>
        public static string AskText(string question)
            => _shell.AskText(question);

        /// <summary>
        /// Ask the user for something that should not be looked at, and read it without echoing.
        /// </summary>
        /// <remarks>
        /// For tokens, passwords and keys -- AskText() would leave the answer on the screen and
        /// in the scrollback. Falls back to reading a line when standard input is redirected,
        /// where there is no terminal echoing it anyway. See CShell.AskSecret().
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <returns>what the user typed, trimmed</returns>
        public static string AskSecret(string question)
            => _shell.AskSecret(question);

        /// <summary>
        /// Ask the user to pick one of a list, and return the one they picked.
        /// </summary>
        /// <remarks>
        /// Labelled ChoiceStyle.Auto: nothing in front of the options when there are arrow keys
        /// to pick with, numbers when the answer has to be typed. See CShell.AskChoice().
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="options">the things to choose between, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the option chosen</returns>
        public static T AskChoice<T>(string question, IEnumerable<T> options, Func<T, string> label = null)
            => _shell.AskChoice(question, options, label);

        /// <summary>
        /// Ask the user to pick one of a list, and return the one they picked.
        /// </summary>
        /// <remarks>
        /// With keys to read the list is moved with the arrow keys, the current option shown in
        /// brackets. Without them the answer is typed: the option's label, its number, or its
        /// letter under ChoiceStyle.Letters -- label matched first. See CShell.AskChoice().
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="style">how the options are labelled</param>
        /// <param name="options">the things to choose between, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the option chosen</returns>
        public static T AskChoice<T>(string question, ChoiceStyle style, IEnumerable<T> options, Func<T, string> label = null)
            => _shell.AskChoice(question, style, options, label);

        /// <summary>
        /// Ask the user to pick any number of a list, and return the ones they picked.
        /// </summary>
        /// <remarks>
        /// Labelled ChoiceStyle.Auto: nothing in front of the options when there are arrow keys
        /// to pick with, numbers when the answer has to be typed. See CShell.AskMultiChoice().
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="options">the things to choose among, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the options chosen, in list order; empty if none were</returns>
        public static T[] AskMultiChoice<T>(string question, IEnumerable<T> options, Func<T, string> label = null)
            => _shell.AskMultiChoice(question, options, label);

        /// <summary>
        /// Ask the user to pick any number of a list, and return the ones they picked.
        /// </summary>
        /// <remarks>
        /// With keys to read, up and down move a `>` down the list and space checks the option
        /// under it. Without them the answer is a comma separated list of labels, numbers or
        /// letters. Choosing nothing is an answer and returns an empty array.
        /// See CShell.AskMultiChoice().
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="style">how the options are labelled</param>
        /// <param name="options">the things to choose among, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the options chosen, in list order; empty if none were</returns>
        public static T[] AskMultiChoice<T>(string question, ChoiceStyle style, IEnumerable<T> options, Func<T, string> label = null)
            => _shell.AskMultiChoice(question, style, options, label);

        /// <summary>
        /// Ask the user for a whole number, asking again until they give one.
        /// </summary>
        /// <param name="question">the question, asked as written</param>
        /// <returns>the number they typed</returns>
        public static int AskNumber(string question)
            => _shell.AskNumber(question);

        /// <summary>
        /// Ask the user for a whole number within a range, asking again until they give one.
        /// </summary>
        /// <param name="question">the question, asked as written</param>
        /// <param name="min">smallest acceptable answer, inclusive</param>
        /// <param name="max">largest acceptable answer, inclusive</param>
        /// <returns>the number they typed, between min and max</returns>
        public static int AskNumber(string question, int min, int max)
            => _shell.AskNumber(question, min, max);

        /// <summary>
        /// Ask the user a yes or no question, asking again until they answer one or the other.
        /// </summary>
        /// <param name="question">the question, asked as written</param>
        /// <returns>true for yes, false for no</returns>
        public static bool AskYesNo(string question)
            => _shell.AskYesNo(question);

        /// <summary>
        /// Ask the user a yes or no question, with an answer that pressing enter accepts.
        /// </summary>
        /// <remarks>
        /// Shown `[Y/n]` or `[y/N]`, so the capital is a promise -- pass the SAFE answer as the
        /// default, because enter is what gets pressed by someone who is not reading.
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <param name="defaultAnswer">what pressing enter answers</param>
        /// <returns>true for yes, false for no</returns>
        public static bool AskYesNo(string question, bool defaultAnswer)
            => _shell.AskYesNo(question, defaultAnswer);

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
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(string value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(bool value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(char value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(char[] buffer)
            => _shell.Write(buffer);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(double value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(decimal value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(object value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(int value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(uint value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(long value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(ulong value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(float value)
            => _shell.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(string format, object arg0)
            => _shell.Write(format, arg0);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(string format, params object[] arg)
            => _shell.Write(format, arg);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(string format, object arg0, object arg1)
            => _shell.Write(format, arg0, arg1);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(char[] buffer, int index, int count)
            => _shell.Write(buffer, index, count);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void Write(string format, object arg0, object arg1, object arg2)
            => _shell.Write(format, arg0, arg1, arg2);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine()
            => _shell.WriteLine();

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(int value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(bool value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(char value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(string value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(object value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(ulong value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(long value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(uint value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(char[] buffer)
            => _shell.WriteLine(buffer);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(float value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(double value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(decimal value)
            => _shell.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(string format, params object[] arg)
            => _shell.WriteLine(format, arg);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(string format, object arg0)
            => _shell.WriteLine(format, arg0);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(string format, object arg0, object arg1)
            => _shell.WriteLine(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(char[] buffer, int index, int count)
            => _shell.WriteLine(buffer, index, count);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void WriteLine(string format, object arg0, object arg1, object arg2)
            => _shell.WriteLine(format, arg0, arg1, arg2);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print()
            => _shell.print();

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(int value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(bool value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(char value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(string value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(object value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(ulong value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(long value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(uint value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(char[] buffer)
            => _shell.print(buffer);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(float value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(double value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(decimal value)
            => _shell.print(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(string format, params object[] arg)
            => _shell.print(format, arg);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(string format, object arg0)
            => _shell.print(format, arg0);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(string format, object arg0, object arg1)
            => _shell.print(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(char[] buffer, int index, int count)
            => _shell.print(buffer, index, count);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        /// <param name="value"></param>
        public static void print(string format, object arg0, object arg1, object arg2)
            => _shell.print(format, arg0, arg1, arg2);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error()
            => _shell.error();

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(int value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(bool value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(char value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(string value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(object value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(ulong value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(long value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(uint value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(char[] buffer)
            => _shell.error(buffer);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(float value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(double value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(decimal value)
            => _shell.error(value);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(string format, params object[] arg)
            => _shell.error(format, arg);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(string format, object arg0)
            => _shell.error(format, arg0);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(string format, object arg0, object arg1)
            => _shell.error(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(char[] buffer, int index, int count)
            => _shell.error(buffer, index, count);

        /// <summary>
        /// Write value as line to standard error
        /// </summary>
        /// <param name="value"></param>
        public static void error(string format, object arg0, object arg1, object arg2)
            => _shell.error(format, arg0, arg1, arg2);

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
        /// returns true if path exists (file or folder)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool exists(string path)
            => Exists(path);

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public static bool Exists(string path)
            => _shell.Exists(path);

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public static bool ExistsFile(string path)
            => _shell.ExistsFile(path);

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public static bool ExistsDirectory(string path)
            => _shell.ExistsDirectory(path);

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
