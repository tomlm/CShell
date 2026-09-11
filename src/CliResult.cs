using System;
using System.Collections.Generic;
using System.Linq;

namespace CShellNet
{
    /// <summary>
    /// A command line that has been read against what a script declared.
    /// </summary>
    /// <remarks>
    /// The same words that declared each thing read it back -- Argument, Switch and Option mean
    /// "declare" on Cli and "read" here -- so the block that reads the command line can be checked
    /// line for line against the block that declared it.
    ///
    /// From Cli.Parse() this is always usable, because a command line that was not understood
    /// exited the process instead of arriving here. Read it and get on with the script.
    ///
    /// From Cli.TryParse() it may be one that should not be used, so check ShouldExit first.
    /// Forgetting is caught rather than ignored: every value below THROWS once the command line
    /// turned out to be bad, because the alternative -- handing back defaults for a line that was
    /// never understood -- is the silent-wrong-behaviour this type exists to prevent. The error
    /// itself has already been written to standard error by then, so what the user sees is the
    /// real message first and a loud failure second.
    /// </remarks>
    public class CliResult
    {
        private readonly HashSet<string> flags;
        private readonly Dictionary<string, string> values;
        private readonly Dictionary<string, string> args;
        private readonly List<string> rest;
        private readonly List<SwitchSpec> switches;
        private readonly List<ArgSpec> arguments;
        private readonly bool whatIfDeclared;

        private CliResult(string program, int exitCode, string error, bool helpRequested, string usage,
                          CliResult parent, string command)
        {
            this.ProgramName = program;
            this.ExitCode = exitCode;
            this.Error = error;
            this.HelpRequested = helpRequested;
            this.UsageText = usage;
            this.Parent = parent;
            this.Command = command;
            this.ShouldExit = true;
        }

        private CliResult(string program, string usage, HashSet<string> flags, Dictionary<string, string> values,
                          Dictionary<string, string> args, List<string> rest, List<SwitchSpec> switches,
                          List<ArgSpec> arguments, bool whatIfDeclared, CliResult parent, string command)
        {
            this.ProgramName = program;
            this.UsageText = usage;
            this.flags = flags;
            this.values = values;
            this.args = args;
            this.rest = rest;
            this.switches = switches;
            this.arguments = arguments;
            this.whatIfDeclared = whatIfDeclared;
            this.Parent = parent;
            this.Command = command;
        }

        internal static CliResult Exiting(string program, int exitCode, string error, bool helpRequested, string usage,
                                          CliResult parent, string command)
        {
            return new CliResult(program, exitCode, error, helpRequested, usage, parent, command);
        }

        internal static CliResult Parsed(string program, string usage, HashSet<string> flags,
                                         Dictionary<string, string> values, Dictionary<string, string> args,
                                         List<string> rest, List<SwitchSpec> switches, List<ArgSpec> arguments,
                                         bool whatIfDeclared, CliResult parent, string command)
        {
            return new CliResult(program, usage, flags, values, args, rest, switches, arguments, whatIfDeclared,
                                 parent, command);
        }

        internal void Ran(int exitCode)
        {
            this.HandlerRan = true;
            this.ExitCode = exitCode;
        }

        /// <summary>The name shown in the usage line.</summary>
        public string ProgramName { get; private set; }

        /// <summary>
        /// True when the script should stop -- help was shown, or the command line was not valid.
        /// </summary>
        /// <remarks>
        /// Always false from Parse(), which will have exited instead. This is for TryParse().
        /// Whatever it reports has already been printed: help to standard output, an error to
        /// standard error.
        /// </remarks>
        public bool ShouldExit { get; private set; }

        /// <summary>
        /// What to return: 0 for help, 1 for a command line that was not valid, and whatever a
        /// command's handler returned.
        /// </summary>
        /// <remarks>
        /// ShouldExit tells the two apart, and it is the only thing that can. True means the line
        /// was not read and this is a parse outcome. False with a non-zero code means the line was
        /// read, the handler ran, and the handler said so -- see HandlerRan.
        /// </remarks>
        public int ExitCode { get; private set; }

        /// <summary>True when a command's Run() or RunAsync() handler was invoked.</summary>
        public bool HandlerRan { get; private set; }

        /// <summary>
        /// The command that was typed at this level, or null when the script declared none.
        /// </summary>
        /// <remarks>
        /// What a script switches on when it declared its commands with the (name, help) overloads
        /// rather than giving each a Run():
        ///
        ///     if (cmd.Command == "start") { Start(cmd.Argument("file")); }
        ///
        /// It is the PRIMARY spelling, whichever alias was typed, so a switch on it does not have
        /// to list them. Readable whatever happened to the command line -- it says which level the
        /// error came from -- unlike the values, which are not.
        /// </remarks>
        public string Command { get; private set; }

        /// <summary>
        /// The level above this one, or null at the top.
        /// </summary>
        /// <remarks>
        /// A command's result chains back to the program's, which is how a global declared at the
        /// top stays readable from the command's result. Switch, Option and WhatIf already walk
        /// it; this is for a script that wants to be explicit about which level it is asking.
        /// </remarks>
        public CliResult Parent { get; private set; }

        /// <summary>Every command that was typed, outermost first: `nuget`, then `push`.</summary>
        /// <remarks>
        /// What to read instead of Command when the same verb appears under two parents and the
        /// leaf name alone does not say which one ran. Empty when no command was typed.
        /// </remarks>
        public IReadOnlyList<string> CommandPath
        {
            get
            {
                var path = new List<string>();
                for (var level = this; level != null; level = level.Parent)
                {
                    if (level.Command != null)
                    {
                        path.Add(level.Command);
                    }
                }

                path.Reverse();
                return path;
            }
        }

        /// <summary>What was wrong with the command line, or null when nothing was.</summary>
        public string Error { get; private set; }

        /// <summary>True when the user asked for help rather than getting something wrong.</summary>
        public bool HelpRequested { get; private set; }

        /// <summary>The generated help, whether or not it was shown.</summary>
        public string UsageText { get; private set; }

        /// <summary>
        /// What was given for a declared positional, or null when an optional one was left out.
        /// </summary>
        /// <param name="name">the name it was declared with</param>
        /// <returns>the value, or null</returns>
        /// <exception cref="InvalidOperationException">the command line was not valid</exception>
        /// <exception cref="ArgumentException">nothing was declared by that name</exception>
        public string Argument(string name)
        {
            Readable();

            if (!this.arguments.Any(a => String.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw Undeclared("argument", name, this.arguments.Select(a => a.Name));
            }

            string value;
            return this.args.TryGetValue(name, out value) ? value : null;
        }

        /// <summary>
        /// Whether a declared switch was given.
        /// </summary>
        /// <param name="name">the name it was declared with, or any of its aliases</param>
        /// <returns>true when it was given</returns>
        /// <exception cref="InvalidOperationException">the command line was not valid</exception>
        /// <exception cref="ArgumentException">nothing was declared by that name</exception>
        public bool Switch(string name)
        {
            Readable();

            // Up the chain, so a global declared by the program is readable from the command's
            // result -- the script that declared it should not have to know which level it
            // happens to be reading from.
            var key = Cli.Normalize(name ?? "");
            for (var level = this; level != null; level = level.Parent)
            {
                var found = level.Declared(key, false);
                if (found != null)
                {
                    return level.flags.Contains(found.Keys[0]);
                }
            }

            throw Undeclared("switch", name, false);
        }

        /// <summary>
        /// The value given for a declared option, or null when it was not supplied.
        /// </summary>
        /// <remarks>
        /// Null rather than a default, so the script says what its default is:
        /// `cmd.Option("source") ?? "https://api.nuget.org/v3/index.json"`.
        /// </remarks>
        /// <param name="name">the name it was declared with, or any of its aliases</param>
        /// <returns>the value as typed, or null</returns>
        /// <exception cref="InvalidOperationException">the command line was not valid</exception>
        /// <exception cref="ArgumentException">nothing was declared by that name</exception>
        public string Option(string name)
        {
            Readable();

            var key = Cli.Normalize(name ?? "");
            for (var level = this; level != null; level = level.Parent)
            {
                var found = level.Declared(key, true);
                if (found != null)
                {
                    string value;
                    return level.values.TryGetValue(found.Keys[0], out value) ? value : null;
                }
            }

            throw Undeclared("option", name, true);
        }

        /// <summary>
        /// Whether the dry-run switch was given.
        /// </summary>
        /// <remarks>
        /// Throws when the script never called Cli.WhatIf(). Answering false would mean a script
        /// that forgot to declare it silently never rehearses -- someone asks for a dry run and
        /// gets the real thing, which is the worst failure this whole type is guarding against.
        /// </remarks>
        /// <exception cref="InvalidOperationException">the command line was not valid, or WhatIf() was never declared</exception>
        public bool WhatIf
        {
            get
            {
                Readable();

                for (var level = this; level != null; level = level.Parent)
                {
                    if (level.whatIfDeclared)
                    {
                        return level.flags.Contains("whatif");
                    }
                }

                throw new InvalidOperationException(
                    "WhatIf was never declared -- add .WhatIf() to the Cli chain, or this script has no dry run to report.");
            }
        }

        /// <summary>Everything the declared Rest collected, or empty when it collected nothing.</summary>
        /// <exception cref="InvalidOperationException">the command line was not valid, or no Rest was declared</exception>
        public IReadOnlyList<string> Rest
        {
            get
            {
                Readable();

                if (!this.arguments.Any(a => a.IsRest))
                {
                    throw new InvalidOperationException("No Rest was declared -- add .Rest(name, help) to the Cli chain.");
                }

                return this.rest;
            }
        }

        /// <summary>Every positional given, in the order it was given.</summary>
        /// <exception cref="InvalidOperationException">the command line was not valid</exception>
        public IReadOnlyList<string> Arguments
        {
            get
            {
                Readable();

                var all = this.arguments.Where(a => !a.IsRest)
                                        .Select(a => this.args.ContainsKey(a.Name) ? this.args[a.Name] : null)
                                        .Where(v => v != null)
                                        .ToList();
                all.AddRange(this.rest);
                return all;
            }
        }

        void Readable()
        {
            if (this.ShouldExit)
            {
                throw new InvalidOperationException(
                    "The command line was not valid, so there is nothing to read from it -- check ShouldExit before reading anything.");
            }
        }

        SwitchSpec Declared(string key, bool wantValue)
        {
            return this.switches == null
                ? null
                : this.switches.FirstOrDefault(s => s.Keys.Contains(key) && s.TakesValue == wantValue);
        }

        // Everything declared by this level and every level above it: the name may have been the
        // program's rather than the command's, and either is a fair thing to have meant.
        ArgumentException Undeclared(string what, string name, bool wantValue)
        {
            var known = new List<string>();
            for (var level = this; level != null; level = level.Parent)
            {
                if (level.switches != null)
                {
                    known.AddRange(level.switches.Where(s => s.TakesValue == wantValue).Select(s => s.Primary));
                }
            }

            return Undeclared(what, name, known);
        }

        static ArgumentException Undeclared(string what, string name, IEnumerable<string> declared)
        {
            var known = declared.ToList();
            var list = known.Count > 0 ? String.Join(", ", known.Select(k => "\"" + k + "\"")) : "nothing";

            // Name what WAS declared: a typo in the script is as easy to make as one on the
            // command line, and as quiet.
            return new ArgumentException($"No {what} \"{name}\" was declared. Declared: {list}.", nameof(name));
        }
    }
}
