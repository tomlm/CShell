using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CShellNet
{
    /// <summary>
    /// How AskChoice() labels the options it offers.
    /// </summary>
    public enum ChoiceStyle
    {
        /// <summary>
        /// Whatever the mode can afford: nothing at all when there are arrow keys to pick with,
        /// numbers when the answer has to be typed. The default, and usually the right one.
        /// </summary>
        Auto,

        /// <summary>1) 2) 3) -- and a typed answer may be the number.</summary>
        Numbers,

        /// <summary>a) b) c) -- and a typed answer may be the letter.</summary>
        Letters,

        /// <summary>
        /// nothing before each. With no label on screen to reference, a typed answer is the
        /// option's own text -- a position number names nothing and is refused.
        /// </summary>
        None,
    }

    /// <summary>
    /// CShell is class which provides the environmental equivelent of a CMD or BASH environment
    /// * current directory
    /// * Environment variables
    /// * Ability to invoke process and pipe input between processes (via MedallionShell library)
    /// </summary>
    public class CShell
    {
        /// <summary>
        /// Start a shell
        /// </summary>
        /// <param name="startingFolder">(OPTIONAL) if passed in, this will be the initial folder</param>
        public CShell(string startingFolder = null)
        {
            this.FolderHistory = new List<string>();
            this.FolderStack = new Stack<string>();
            if (startingFolder != null)
            {
                if (Path.IsPathRooted(startingFolder))
                {
                    CurrentFolder = new DirectoryInfo(startingFolder);
                }
                else
                {
                    CurrentFolder = new DirectoryInfo(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, startingFolder)));
                }
            }
            else
            {
                CurrentFolder = new DirectoryInfo(Environment.CurrentDirectory);
            }
        }

        public bool ThrowOnError { get; set; } = true;

        public bool Echo { get; set; } = true;

        /// <summary>
        /// Where the Ask methods get their keystrokes when they are reading keys rather than
        /// lines. Null reads the console; set it to drive the rich prompts from somewhere else.
        /// </summary>
        public Func<ConsoleKeyInfo> ReadKey { get; set; }

        /// <summary>
        /// Whether the Ask methods draw their rich prompts -- a selection moved with the arrow
        /// keys -- or fall back to reading a typed line.
        /// </summary>
        /// <remarks>
        /// Null, the default, decides by asking whether standard input is redirected, because
        /// Console.ReadKey() throws outright when it is: piped, scheduled and CI runs have no
        /// keys to read. Worth setting explicitly anywhere the answer matters, since the two
        /// modes accept different input and print different things -- a script that works by
        /// hand and fails under CI has usually just changed mode without being told.
        /// </remarks>
        public bool? RichPrompts { get; set; }

        bool UseKeys
        {
            get { return this.RichPrompts.HasValue ? this.RichPrompts.Value : !Console.IsInputRedirected; }
        }

        ConsoleKeyInfo NextKey()
        {
            var reader = this.ReadKey;
            return reader != null ? reader() : Console.ReadKey(true);
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
        public Command Run(String executable, params Object[] arguments)
        {
            return Run((opt) => { }, executable, arguments);
        }

        /// <summary>
        /// Run a process with options.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="options">options function</param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Run(Action<Shell.Options> options, string executable, params Object[] arguments)
        {
            if (this.Echo)
            {
                Console.WriteLine($"{executable} {String.Join(" ", arguments)}");
            }

            return Command.Run(executable, arguments, (opt) =>
            {
                this.SetCommandOptions(opt);
                options(opt);
            });
        }

        /// <summary>
        /// Start a process detached 
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Start(string executable, params Object[] arguments)
        {
            return Start((opt) => { }, executable, arguments);
        }

        /// <summary>
        /// Start a process detached.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public Command Start(Action<Shell.Options> options, string executable, params Object[] arguments)
        {
            if (this.Echo)
            {
                Console.WriteLine($"{executable} {String.Join(" ", arguments)}");
            }

            return Command.Run(executable, arguments, opt =>
            {
                this.SetCommandOptions(opt);

                options(opt);
                opt.DisposeOnExit(false);
                opt.StartInfo((info) =>
                {
                    // detached process
                    info.CreateNoWindow = false;
                    info.RedirectStandardError = false;
                    info.RedirectStandardInput = false;
                    info.RedirectStandardOutput = false;
                    info.UseShellExecute = true;
                });
            });
        }

        /// <summary>
        /// Run a cmd/bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public Command Cmd(string cmd)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (this.Echo)
                {
                    Console.WriteLine(cmd);
                }

                List<string> args = new List<string>();
                if (!Echo)
                {
                    args.Add("/Q");
                }
                args.Add("/C");
                args.Add(cmd);

                return Command.Run("cmd.exe", args, SetCommandOptions);
            }
            else
            {
                return Bash(cmd);
            }
        }

        /// <summary>
        /// Run a bash command
        /// </summary>
        /// <param name="cmd">shell cmd to run</param>
        /// <returns>Command</returns>
        public Command Bash(string cmd)
        {
            if (this.Echo)
            {
                Console.WriteLine(cmd);
            }
            var args = new List<string>();

            var escapedArgs = cmd.Replace("\"", "\\\"");
            args.Add("-c");
            args.Add(escapedArgs);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Command.Run("bash.exe", args, SetCommandOptions);
            }
            else
            {
                return Command.Run("/bin/bash", args, SetCommandOptions);
            }
        }

        /// <summary>
        /// Current folder
        /// </summary>
        private DirectoryInfo _currentFolder;
        public DirectoryInfo CurrentFolder
        {
            get
            {
                return _currentFolder;
            }
            set
            {
                _currentFolder = value;
                Environment.CurrentDirectory = value.FullName;
                if (Environment.CurrentDirectory != FolderHistory.LastOrDefault())
                {
                    FolderHistory.Add(Environment.CurrentDirectory);
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
        /// Change Current Folder 
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell cd(string folderPath)
        {
            this.CurrentFolder = new DirectoryInfo(ResolvePath(folderPath));
            return this;
        }

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
        public CShell chdir(string folderPath)
        {
            this.cd(folderPath);
            return this;
        }

        /// <summary>
        /// copy file or folder 
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file or folder</param>
        /// <param name="targetPath">absolute or relative path to a target File or folder</param>
        /// <returns></returns>
        public CShell copy(string sourcePath, string targetPath, bool overwrite = false, bool recursive = false)
        {
            if (Directory.Exists(sourcePath))
            {
                return CopyFolder(sourcePath, targetPath, recursive);
            }

            if (Directory.Exists(targetPath))
                targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, targetPath, overwrite);
            return this;
        }

        /// <summary>
        /// rename file
        /// </summary>
        /// <param name="sourcePath">absolute or relative path to a source file</param>
        /// <param name="targetPath">absolute or relative path to a target File</param>
        /// <returns></returns>
        public CShell rename(string sourcePath, string targetPath)
        {
            return move(sourcePath, targetPath);
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
                Directory.Move(sourcePath, targetPath);
                return this;
            }
            else
            {
                if (Directory.Exists(targetPath))
                    targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                File.Move(sourcePath, targetPath);
                return this;
            }
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell md(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            return this;
        }

        /// <summary>
        /// Make directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell mkdir(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            return this;
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rd(string folderPath, bool recursive = false)
        {
            Directory.Delete(folderPath, recursive);
            return this;
        }

        /// <summary>
        /// remove directory
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell rmdir(string folderPath, bool recursive = false)
        {
            Directory.Delete(folderPath, recursive);
            return this;
        }

        /// <summary>
        /// do a dir in the current folder
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        public IEnumerable<string> dir(string searchPattern = null, bool recursive = false)
        {
            return this.CurrentFolder.EnumerateFileSystemInfos(searchPattern ?? "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(fileInfo => this.ResolvePath(fileInfo.FullName));
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
            File.Delete(filePath);
            return this;
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell del(string filePath)
        {
            File.Delete(filePath);
            return this;
        }

        /// <summary>
        /// delete a file
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="filePath">absolute or relative path to a file</param>
        /// <returns></returns>
        public CShell erase(string filePath)
        {
            File.Delete(filePath);
            return this;
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

        /// <summary>
        /// returns true if path exists (file or folder)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool exists(string path)
            => Exists(path);

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public bool Exists(string path)
        {
            path = ResolvePath(path);
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public bool ExistsFile(string path)
        {
            path = ResolvePath(path);
            return File.Exists(path);
        }

        /// <summary>
        /// Returns true if file or folder exists
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>true/false</returns>
        public bool ExistsDirectory(string path)
        {
            path = ResolvePath(path);
            return Directory.Exists(path);
        }

        /// <summary>
        /// Copy a Folder 
        /// </summary>
        /// <param name="sourceFolderPath">absolute or relative path to a source folder</param>
        /// <param name="targetFolderPath">absolute or relative path to a target folder</param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public CShell CopyFolder(string sourceFolderPath, string targetFolderPath, bool recursive = true)
        {
            var sourcePath = ResolvePath(sourceFolderPath);
            var targetPath = ResolvePath(targetFolderPath);
            CopyFolder(sourcePath, targetPath);

            void CopyFolder(string srcFolder, string destFolder)
            {
                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                string[] files = Directory.GetFiles(srcFolder);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest);
                }
                string[] folders = Directory.GetDirectories(srcFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);
                }
            }
            return this;
        }

        /// <summary>
        /// Change to a folder and add it to the stack
        /// </summary>
        /// <param name="folderPath">absolute or relative path to a folder</param>
        /// <returns></returns>
        public CShell PushFolder(string folderPath)
        {
            var oldFolder = this.CurrentFolder;
            cd(folderPath);
            this.FolderStack.Push(oldFolder.FullName);
            return this;
        }

        /// <summary>
        /// Pop a folder off the stack and change the current directory to it
        /// </summary>
        /// <returns></returns>
        public CShell PopFolder()
        {
            this.CurrentFolder = new DirectoryInfo(this.FolderStack.Pop());
            return this;
        }

        /// <summary>
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="filePath">absolute or relative path to file</param>
        /// <returns></returns>
        public Command ReadFile(string filePath)
        {
            var path = ResolvePath(filePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] args = new string[] { "/c", "type", path };
                return Command.Run("cmd.exe", args, SetCommandOptions);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] args = new string[] { path };
                return Command.Run("cat", args, SetCommandOptions);
            }
            throw new ArgumentOutOfRangeException("Unknown operating system");
        }

        /// <summary>
        /// Turns lines of text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Command echo(IEnumerable<string> lines)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.WriteAllLines(path, lines);
            return ReadFile(path);
        }

        /// <summary>
        /// Turns text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Command echo(string text)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.WriteAllText(path, text);
            return ReadFile(path);
        }

        /// <summary>
        /// Turns text to a command
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Command echo(TextReader textReader)
        {
            return echo(textReader.ReadToEnd());
        }

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(string value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(bool value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(char value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(char[] buffer) => Console.Write(buffer);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(double value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(decimal value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(object value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(int value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(uint value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(long value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(ulong value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(float value) => Console.Write(value);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(string format, object arg0) => Console.Write(format, arg0);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(string format, params object[] arg) => Console.Write(format, arg);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(string format, object arg0, object arg1) => Console.Write(format, arg0, arg1);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(char[] buffer, int index, int count) => Console.Write(buffer, index, count);

        /// <summary>
        /// Write value to standard out
        /// </summary>
        /// <param name="value"></param>
        public void Write(string format, object arg0, object arg1, object arg2) => Console.Write(format, arg0, arg1, arg2);

        /// <summary>
        /// Ask the user a question and return what they typed.
        /// </summary>
        /// <remarks>
        /// The Ask family is the script asking the user. For the other direction -- a process
        /// that asks the user something itself -- see the remarks on Run().
        /// None of them will answer themselves: see ReadAnswer.
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <returns>what the user typed, trimmed; empty if they just pressed enter</returns>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public string AskText(string question)
        {
            Console.Write($"{question.TrimEnd()} ");
            return ReadAnswer(question);
        }

        /// <summary>
        /// Ask the user for something that should not be looked at, and read it without echoing.
        /// </summary>
        /// <remarks>
        /// For tokens, passwords and keys. AskText() would put the answer on the screen, into the
        /// scrollback, and into whatever is recording the terminal -- a long-lived credential is
        /// worth one method to keep out of all three. Nothing is echoed at all, not even stars,
        /// which is what a console password prompt conventionally does; backspace still works.
        ///
        /// With no keys to read this falls back to reading a line. That is not a downgrade: piped
        /// input was never being echoed to a terminal, which is the only thing being avoided.
        ///
        /// A string, not a SecureString: SecureString does not protect its contents outside
        /// Windows and .NET now advises against it, so this would be security theatre. Treat the
        /// return like any other secret -- do not log it, and hand it on through stdin rather
        /// than as an argument, where it would show up in the process list.
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <returns>what the user typed, trimmed</returns>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public string AskSecret(string question)
        {
            Console.Write($"{question.TrimEnd()} ");

            if (!this.UseKeys)
            {
                return ReadAnswer(question);
            }

            var secret = new System.Text.StringBuilder();
            while (true)
            {
                var key = NextKey();

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return secret.ToString().Trim();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (secret.Length > 0)
                    {
                        secret.Length--;
                    }

                    continue;
                }

                // Arrows, function keys and the like arrive with no character to append.
                if (key.KeyChar != '\0')
                {
                    secret.Append(key.KeyChar);
                }
            }
        }

        /// <summary>
        /// Ask the user to pick one of a list, and return the one they picked.
        /// </summary>
        /// <remarks>
        /// Labelled ChoiceStyle.Auto -- nothing in front of the options when there are arrow keys
        /// to pick with, numbers when the answer has to be typed.
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="options">the things to choose between, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the option chosen</returns>
        public T AskChoice<T>(string question, IEnumerable<T> options, Func<T, string> label = null)
        {
            return AskChoice(question, ChoiceStyle.Auto, options, label);
        }

        /// <summary>
        /// Ask the user to pick one of a list, and return the one they picked.
        /// </summary>
        /// <remarks>
        /// With keys to read, the list is drawn with the current option in brackets and moved
        /// with the arrow keys, enter choosing it. Typing an option's own marker jumps to it but
        /// still waits for enter, so a mistyped key costs nothing.
        ///
        /// Without them the list is printed once and the answer is typed: the option's LABEL --
        /// what it is shown as, not what ToString() says -- or whatever is printed in front of
        /// it. The label is matched FIRST, so a list whose options are themselves numbers --
        /// "3", "1", "2" -- answers the way it reads, and typing 3 picks the option labelled 3
        /// rather than the third one.
        ///
        /// The prompt asks for what is on screen and takes nothing else: numbers over a numbered
        /// list, letters over a lettered one, and under ChoiceStyle.None -- which prints no
        /// labels at all -- the option's text and only that.
        ///
        /// What comes back is the option itself, not where it sat. Two options that label the
        /// same are therefore indistinguishable in the answer, though a reference type still
        /// hands back the instance that was chosen.
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="style">how the options are labelled</param>
        /// <param name="options">the things to choose between, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the option chosen</returns>
        /// <exception cref="ArgumentNullException">options is null</exception>
        /// <exception cref="ArgumentException">no options were given, or too many to letter</exception>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public T AskChoice<T>(string question, ChoiceStyle style, IEnumerable<T> options, Func<T, string> label = null)
        {
            var items = Materialise("AskChoice", options, style);
            var labels = Labels(items, label);
            var resolved = Resolve(style);

            var picked = this.UseKeys
                ? ChooseByKey(question, resolved, labels)
                : ChooseByLine(question, resolved, labels);

            return items[picked - 1];
        }

        // The options as an array, with the two ways of asking for an impossible list refused up
        // front. Everything below the public methods works in labels and 1-based positions; only
        // AskChoice and AskMultiChoice know there is a T at all.
        static T[] Materialise<T>(string caller, IEnumerable<T> options, ChoiceStyle style)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options), $"{caller}() was given no options at all.");
            }

            var items = options.ToArray();

            if (items.Length == 0)
            {
                throw new ArgumentException($"{caller}() needs at least one option to choose between.", nameof(options));
            }

            if (style == ChoiceStyle.Letters && items.Length > 26)
            {
                throw new ArgumentException($"{caller}() cannot letter {items.Length} options; there are 26 letters.", nameof(options));
            }

            return items;
        }

        // What each option is shown as, and -- in the typed mode -- what it answers to. A null
        // option labels as empty rather than throwing: a hole in a list is the caller's problem
        // to see on screen, not a reason to take the prompt down.
        static string[] Labels<T>(T[] items, Func<T, string> label)
        {
            var labels = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                labels[i] = label != null
                    ? (label(items[i]) ?? "")
                    : (items[i] == null ? "" : items[i].ToString());
            }

            return labels;
        }

        // Auto asks what the mode can afford. With arrow keys the selection IS the affordance and
        // a label in front of every row is noise; without them the label is the only thing saying
        // what to type, and a bare list under a "[1-3]" prompt makes you count rows yourself.
        ChoiceStyle Resolve(ChoiceStyle style)
        {
            if (style != ChoiceStyle.Auto)
            {
                return style;
            }

            return this.UseKeys ? ChoiceStyle.None : ChoiceStyle.Numbers;
        }

        static string Marker(ChoiceStyle style, int index)
        {
            if (style == ChoiceStyle.Numbers) { return (index + 1) + ") "; }
            if (style == ChoiceStyle.Letters) { return (char)('a' + index) + ") "; }

            return "";
        }

        // Which option a typed answer names, or 0 for none. Option TEXT is matched before any
        // marker, which is what keeps a list of numbers honest.
        static int FromAnswer(ChoiceStyle style, string[] options, string answer)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (String.Equals(options[i], answer, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            if (style == ChoiceStyle.Letters)
            {
                if (answer.Length == 1)
                {
                    var index = Char.ToLowerInvariant(answer[0]) - 'a';
                    if (index >= 0 && index < options.Length)
                    {
                        return index + 1;
                    }
                }

                return 0;
            }

            // Under None the label is all there is. Taking a position number here would mean
            // answering with something the list never showed -- "[1-3]" over an unnumbered list
            // leaves you counting rows -- so the option's own text is the only answer.
            if (style == ChoiceStyle.None)
            {
                return 0;
            }

            int number;
            if (int.TryParse(answer, out number) && number >= 1 && number <= options.Length)
            {
                return number;
            }

            return 0;
        }

        static void RenderChoices(ChoiceStyle style, string[] options, int selected)
        {
            for (int i = 0; i < options.Length; i++)
            {
                var item = i == selected ? "[" + options[i] + "]" : " " + options[i] + " ";
                Console.WriteLine(Fill("  " + Marker(style, i) + item));
            }
        }

        int ChooseByKey(string question, ChoiceStyle style, string[] options)
        {
            var selected = 0;

            Console.WriteLine(question);
            RenderChoices(style, options, selected);

            while (true)
            {
                var key = NextKey();

                if (key.Key == ConsoleKey.Enter)
                {
                    return selected + 1;
                }

                if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.LeftArrow)
                {
                    selected = (selected - 1 + options.Length) % options.Length;
                }
                else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.RightArrow)
                {
                    selected = (selected + 1) % options.Length;
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    selected = 0;
                }
                else if (key.Key == ConsoleKey.End)
                {
                    selected = options.Length - 1;
                }
                else if (key.KeyChar != '\0')
                {
                    var named = FromAnswer(style, options, key.KeyChar.ToString());
                    if (named > 0)
                    {
                        selected = named - 1;
                    }
                }

                Rewind(options.Length);
                RenderChoices(style, options, selected);
            }
        }

        int ChooseByLine(string question, ChoiceStyle style, string[] options)
        {
            // Written once, outside the loop. A rejected answer reprints the input line only --
            // repeating the whole question every time buries the list it refers to.
            Console.WriteLine(question);
            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine("  " + Marker(style, i) + options[i]);
            }

            // Whatever is in front of the options is what the prompt asks for: numbers over a
            // numbered list, letters over a lettered one, and nothing to reference at all over
            // a bare one, which just takes the text.
            string hint;
            if (style == ChoiceStyle.Letters)
            {
                hint = options.Length == 1 ? "[a] " : $"[a-{(char)('a' + options.Length - 1)}] ";
            }
            else if (style == ChoiceStyle.None)
            {
                hint = "> ";
            }
            else
            {
                hint = options.Length == 1 ? "[1] " : $"[1-{options.Length}] ";
            }

            while (true)
            {
                Console.Write(hint);
                var answer = ReadAnswer(question);

                var chosen = FromAnswer(style, options, answer);
                if (chosen > 0)
                {
                    return chosen;
                }

                Console.WriteLine(answer.Length == 0
                    ? "Pick one of the above."
                    : $"'{answer}' is not one of the above.");
            }
        }

        /// <summary>
        /// Ask the user to pick any number of a list, and return the ones they picked.
        /// </summary>
        /// <remarks>
        /// Labelled ChoiceStyle.Auto -- nothing in front of the options when there are arrow keys
        /// to pick with, numbers when the answer has to be typed.
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="options">the things to choose among, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the options chosen, in list order; empty if none were</returns>
        public T[] AskMultiChoice<T>(string question, IEnumerable<T> options, Func<T, string> label = null)
        {
            return AskMultiChoice(question, ChoiceStyle.Auto, options, label);
        }

        /// <summary>
        /// Ask the user to pick any number of a list, and return the ones they picked.
        /// </summary>
        /// <remarks>
        /// AskChoice() with a checkbox. With keys to read, up and down move a `>` down the list
        /// and space checks the option under it, enter finishing. The cursor and the checkmarks
        /// are two different things, so they get two different marks: reusing the brackets for
        /// both -- as AskChoice() can afford to, having only one -- leaves a line whose state
        /// nobody can read.
        ///
        /// Without keys the answer is typed as a comma separated list, each part being an
        /// option's label, number, or letter under ChoiceStyle.Letters. Commas alone separate
        /// them, so options labelled with spaces in them still answer to their labels. One part
        /// that names nothing rejects the whole answer rather than silently selecting the rest.
        ///
        /// Choosing nothing is an answer: enter on an unchecked list, or a blank line, returns
        /// an empty array rather than asking again. A caller that needs at least one has to say
        /// so itself -- there is no way for this to tell an empty answer from a deliberate one.
        /// </remarks>
        /// <typeparam name="T">what is being chosen among</typeparam>
        /// <param name="question">the question, asked as written</param>
        /// <param name="style">how the options are labelled</param>
        /// <param name="options">the things to choose among, at least one</param>
        /// <param name="label">what to show for each; ToString() when not given</param>
        /// <returns>the options chosen, in list order; empty if none were</returns>
        /// <exception cref="ArgumentNullException">options is null</exception>
        /// <exception cref="ArgumentException">no options were given, or too many to letter</exception>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public T[] AskMultiChoice<T>(string question, ChoiceStyle style, IEnumerable<T> options, Func<T, string> label = null)
        {
            var items = Materialise("AskMultiChoice", options, style);
            var labels = Labels(items, label);
            var resolved = Resolve(style);

            var picked = this.UseKeys
                ? ChooseManyByKey(question, resolved, labels)
                : ChooseManyByLine(question, resolved, labels);

            var chosen = new T[picked.Length];
            for (int i = 0; i < picked.Length; i++)
            {
                chosen[i] = items[picked[i] - 1];
            }

            return chosen;
        }

        static int[] Checked(bool[] chosen)
        {
            var picked = new List<int>();
            for (int i = 0; i < chosen.Length; i++)
            {
                if (chosen[i])
                {
                    picked.Add(i + 1);
                }
            }

            return picked.ToArray();
        }

        static void RenderChecks(ChoiceStyle style, string[] options, bool[] chosen, int cursor)
        {
            for (int i = 0; i < options.Length; i++)
            {
                var pointer = i == cursor ? "> " : "  ";
                var box = chosen[i] ? "[x] " : "[ ] ";
                Console.WriteLine(Fill(pointer + Marker(style, i) + box + options[i]));
            }
        }

        int[] ChooseManyByKey(string question, ChoiceStyle style, string[] options)
        {
            var chosen = new bool[options.Length];
            var cursor = 0;

            Console.WriteLine(question);
            RenderChecks(style, options, chosen, cursor);

            while (true)
            {
                var key = NextKey();

                if (key.Key == ConsoleKey.Enter)
                {
                    return Checked(chosen);
                }

                // Tested before the marker jump below, so space is never read as a label.
                if (key.Key == ConsoleKey.Spacebar || key.KeyChar == ' ')
                {
                    chosen[cursor] = !chosen[cursor];
                }
                else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.LeftArrow)
                {
                    cursor = (cursor - 1 + options.Length) % options.Length;
                }
                else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.RightArrow)
                {
                    cursor = (cursor + 1) % options.Length;
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    cursor = 0;
                }
                else if (key.Key == ConsoleKey.End)
                {
                    cursor = options.Length - 1;
                }
                else if (key.KeyChar != '\0')
                {
                    var named = FromAnswer(style, options, key.KeyChar.ToString());
                    if (named > 0)
                    {
                        cursor = named - 1;
                    }
                }

                Rewind(options.Length);
                RenderChecks(style, options, chosen, cursor);
            }
        }

        int[] ChooseManyByLine(string question, ChoiceStyle style, string[] options)
        {
            Console.WriteLine(question);
            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine("  " + Marker(style, i) + options[i]);
            }

            while (true)
            {
                Console.Write("[comma separated, blank for none] ");
                var answer = ReadAnswer(question);

                if (answer.Length == 0)
                {
                    return new int[0];
                }

                var chosen = new bool[options.Length];
                string unknown = null;

                foreach (var part in answer.Split(','))
                {
                    var token = part.Trim();
                    if (token.Length == 0)
                    {
                        continue;
                    }

                    var named = FromAnswer(style, options, token);
                    if (named == 0)
                    {
                        unknown = token;
                        break;
                    }

                    chosen[named - 1] = true;
                }

                if (unknown == null)
                {
                    return Checked(chosen);
                }

                Console.WriteLine($"'{unknown}' is not one of the above.");
            }
        }

        /// <summary>
        /// Ask the user for a whole number, asking again until they give one.
        /// </summary>
        /// <param name="question">the question, asked as written</param>
        /// <returns>the number they typed</returns>
        public int AskNumber(string question)
        {
            return AskNumber(question, int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// Ask the user for a whole number within a range, asking again until they give one.
        /// </summary>
        /// <remarks>
        /// With keys to read, up and down step the number and digits type it, both held to the
        /// range so it can never show a value it would then refuse. Without them the number is
        /// typed as a line, and one outside the range is rejected the same way an unparseable
        /// one is -- a number the caller cannot use is not an answer.
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <param name="min">smallest acceptable answer, inclusive</param>
        /// <param name="max">largest acceptable answer, inclusive</param>
        /// <returns>the number they typed, between min and max</returns>
        /// <exception cref="ArgumentException">min is greater than max</exception>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public int AskNumber(string question, int min, int max)
        {
            if (min > max)
            {
                throw new ArgumentException($"AskNumber() was given an empty range: {min} to {max}.", nameof(min));
            }

            return this.UseKeys ? NumberByKey(question, min, max) : NumberByLine(question, min, max);
        }

        static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        static string Range(int min, int max)
        {
            return min == int.MinValue && max == int.MaxValue ? "" : $"[{min}-{max}] ";
        }

        int NumberByKey(string question, int min, int max)
        {
            var prefix = question.TrimEnd() + " " + Range(min, max);
            var typed = Clamp(0, min, max).ToString();

            Console.Write("\r" + Fill(prefix + typed));

            while (true)
            {
                var key = NextKey();
                int current;

                if (key.Key == ConsoleKey.Enter)
                {
                    if (int.TryParse(typed, out current) && current >= min && current <= max)
                    {
                        Console.WriteLine();
                        return current;
                    }
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    int.TryParse(typed, out current);
                    typed = Clamp(current + 1, min, max).ToString();
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    int.TryParse(typed, out current);
                    typed = Clamp(current - 1, min, max).ToString();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (typed.Length > 0)
                    {
                        typed = typed.Substring(0, typed.Length - 1);
                    }
                }
                else if (Char.IsDigit(key.KeyChar) || (key.KeyChar == '-' && typed.Length == 0))
                {
                    typed = typed + key.KeyChar;
                }

                Console.Write("\r" + Fill(prefix + typed));
            }
        }

        int NumberByLine(string question, int min, int max)
        {
            var range = Range(min, max);

            // An unbounded ask has no range to show, and a bare cursor under a question reads
            // as a hang rather than a prompt.
            var hint = range.Length > 0 ? range : "> ";

            Console.WriteLine(question);
            while (true)
            {
                Console.Write(hint);
                var answer = ReadAnswer(question);

                int number;
                if (int.TryParse(answer, out number))
                {
                    if (number >= min && number <= max)
                    {
                        return number;
                    }

                    Console.WriteLine($"{number} is outside {min} to {max}.");
                    continue;
                }

                Console.WriteLine(answer.Length == 0
                    ? "Type a number."
                    : $"'{answer}' is not a number.");
            }
        }

        /// <summary>
        /// Ask the user a yes or no question, asking again until they answer one or the other.
        /// </summary>
        /// <param name="question">the question, asked as written</param>
        /// <returns>true for yes, false for no</returns>
        public bool AskYesNo(string question)
        {
            return AskYesNo(question, (bool?)null);
        }

        /// <summary>
        /// Ask the user a yes or no question, with an answer that pressing enter accepts.
        /// </summary>
        /// <remarks>
        /// With keys to read, Yes and No sit side by side with the current one in brackets. The
        /// arrow keys move between them and so do y and n -- but only enter answers, the same
        /// way typing an option's marker in AskChoice() moves to it without choosing it. Without
        /// keys the answer is typed as y, yes, n or no, case insensitively.
        ///
        /// The default is shown capitalised the way a shell script does it -- `[Y/n]` for yes,
        /// `[y/N]` for no -- which makes the capital a promise. Pass the SAFE answer as the
        /// default, because enter is what gets pressed by someone who is not reading.
        /// </remarks>
        /// <param name="question">the question, asked as written</param>
        /// <param name="defaultAnswer">what pressing enter answers</param>
        /// <returns>true for yes, false for no</returns>
        /// <exception cref="InvalidOperationException">standard input is at end of stream</exception>
        public bool AskYesNo(string question, bool defaultAnswer)
        {
            return AskYesNo(question, (bool?)defaultAnswer);
        }

        bool AskYesNo(string question, bool? defaultAnswer)
        {
            return this.UseKeys ? YesNoByKey(question, defaultAnswer) : YesNoByLine(question, defaultAnswer);
        }

        static string YesNoBar(bool yes)
        {
            return yes ? "[Yes]  No " : " Yes  [No]";
        }

        bool YesNoByKey(string question, bool? defaultAnswer)
        {
            // With no default there is still a side the selection has to start on. Starting on
            // Yes and requiring enter is not the same promise as a default: nothing is accepted
            // until a key says so.
            var yes = defaultAnswer.HasValue ? defaultAnswer.Value : true;
            var prefix = question.TrimEnd() + " ";

            Console.Write("\r" + Fill(prefix + YesNoBar(yes)));

            while (true)
            {
                var key = NextKey();

                // Enter is the only thing that answers. y and n MOVE the selection rather than
                // committing it, which is the same rule AskChoice() plays by when you type an
                // option's marker: one key is never enough to answer a question, so a mistyped
                // one costs nothing. The alternative -- y answering outright -- makes the two
                // halves of the family disagree, and surprises anyone who reached for y meaning
                // to look before they leapt.
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Write("\r" + Fill(prefix + YesNoBar(yes)));
                    Console.WriteLine();
                    return yes;
                }

                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                {
                    yes = true;
                }
                else if (key.KeyChar == 'n' || key.KeyChar == 'N')
                {
                    yes = false;
                }
                else if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow || key.Key == ConsoleKey.Tab)
                {
                    yes = !yes;
                }
                else
                {
                    continue;
                }

                Console.Write("\r" + Fill(prefix + YesNoBar(yes)));
            }
        }

        bool YesNoByLine(string question, bool? defaultAnswer)
        {
            var choices = !defaultAnswer.HasValue ? "[y/n]"
                        : defaultAnswer.Value ? "[Y/n]"
                        : "[y/N]";

            while (true)
            {
                Console.Write($"{question.TrimEnd()} {choices} ");
                var answer = ReadAnswer(question);

                if (answer.Length == 0 && defaultAnswer.HasValue)
                {
                    return defaultAnswer.Value;
                }

                if (String.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (String.Equals(answer, "n", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(answer, "no", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Console.WriteLine("Answer y or n.");
            }
        }

        // Move back over lines already drawn so the next render replaces them. Without a console
        // there is nothing to move around in, and the renders stack up instead -- ugly on screen,
        // but exactly what a captured transcript wants.
        static void Rewind(int lines)
        {
            if (lines <= 0 || Console.IsOutputRedirected)
            {
                return;
            }

            try
            {
                var top = Console.CursorTop - lines;
                Console.SetCursorPosition(0, top < 0 ? 0 : top);
            }
            catch (IOException)
            {
            }
        }

        // Pad a redrawn line out to the width so whatever the last render left there is erased.
        static string Fill(string text)
        {
            if (Console.IsOutputRedirected)
            {
                return text;
            }

            try
            {
                var width = Console.WindowWidth - 1;
                return text.Length < width ? text.PadRight(width) : text;
            }
            catch (IOException)
            {
                return text;
            }
        }

        /// <summary>
        /// Read one answer, refusing to treat "there is nobody there" as an answer.
        /// </summary>
        /// <remarks>
        /// ReadLine() returns null at end of stream rather than blocking, which a script run
        /// non-interactively -- piped, scheduled, under CI -- hits immediately. Left unchecked
        /// that is an empty answer the caller acts on, or, in the loops above, a spin that reasks
        /// a question nobody can hear forever. Throwing says which question went unanswered.
        /// </remarks>
        string ReadAnswer(string question)
        {
            var answer = Console.ReadLine();
            if (answer == null)
            {
                throw new InvalidOperationException(
                    $"\"{question}\" could not be answered: standard input is at end of stream, " +
                    "so there is no one to ask.");
            }

            return answer.Trim();
        }

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine() => Console.WriteLine();


        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(int value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(bool value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(char value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(string value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(object value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(ulong value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(long value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(uint value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(char[] buffer) => Console.WriteLine(buffer);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(float value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(double value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(decimal value) => Console.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(string format, params object[] arg) => Console.WriteLine(format, arg);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(string format, object arg0) => Console.WriteLine(format, arg0);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(string format, object arg0, object arg1) => Console.WriteLine(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(char[] buffer, int index, int count) => Console.WriteLine(buffer, index, count);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void WriteLine(string format, object arg0, object arg1, object arg2) => Console.WriteLine(format, arg0, arg1, arg2);

        /// <summary>
        /// Write line to standard out
        /// </summary>
        public void print() => Console.Out.WriteLine();

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(int value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(bool value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(char value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(string value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(object value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(ulong value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(long value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(uint value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(char[] buffer) => Console.Out.WriteLine(buffer);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(float value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(double value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(decimal value) => Console.Out.WriteLine(value);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(string format, params object[] arg) => Console.Out.WriteLine(format, arg);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(string format, object arg0) => Console.Out.WriteLine(format, arg0);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(string format, object arg0, object arg1) => Console.Out.WriteLine(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(char[] buffer, int index, int count) => Console.Out.WriteLine(buffer, index, count);

        /// <summary>
        /// Write value as line to standard out
        /// </summary>
        public void print(string format, object arg0, object arg1, object arg2) => Console.Out.WriteLine(format, arg0, arg1, arg2);


        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error() => Console.Error.WriteLine();

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(int value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(bool value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(char value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(string value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(object value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(ulong value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(long value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(uint value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(char[] buffer) => Console.Error.WriteLine(buffer);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(float value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(double value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(decimal value) => Console.Error.WriteLine(value);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(string format, params object[] arg) => Console.Error.WriteLine(format, arg);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(string format, object arg0) => Console.Error.WriteLine(format, arg0);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(string format, object arg0, object arg1) => Console.Error.WriteLine(format, arg0, arg1);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(char[] buffer, int index, int count) => Console.Error.WriteLine(buffer, index, count);

        /// <summary>
        /// Write value as line to standard Error
        /// </summary>
        public void error(string format, object arg0, object arg1, object arg2) => Console.Error.WriteLine(format, arg0, arg1, arg2);

        /// <summary>
        /// Resolve a path relative to CurrentFolder
        /// </summary>
        /// <param name="absoluteOrReltivePath">absolute or relative path</param>
        /// <returns></returns>
        public string ResolvePath(string absoluteOrReltivePath)
        {
            if (absoluteOrReltivePath == null)
            {
                throw new ArgumentNullException(nameof(absoluteOrReltivePath));
            }

            if (Path.IsPathRooted(absoluteOrReltivePath))
            {
                return Path.GetFullPath(absoluteOrReltivePath);
            }
            else
            {
                return Path.GetFullPath(Path.Combine(this.CurrentFolder.FullName, absoluteOrReltivePath));
            }
        }

        /// <summary>
        /// Override this to control the MedallionShell options for .Run()
        /// </summary>
        /// <param name="options"></param>
        public virtual void SetCommandOptions(Shell.Options options)
        {
            options.StartInfo((psi) =>
                {
                    // set working folder
                    psi.WorkingDirectory = this.CurrentFolder.FullName;
                })
                .ThrowOnError(false); // this.ThrowOnError);
        }

    }
}
