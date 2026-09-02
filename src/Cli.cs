using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CShellNet
{
    /// <summary>
    /// Declares what a script accepts on its command line, and parses it.
    /// </summary>
    /// <remarks>
    /// Three words, because there are three kinds of thing: an <b>Argument</b> is a positional, a
    /// <b>Switch</b> is on or off, an <b>Option</b> carries a value.
    ///
    ///     var cmd = Cli.For(Args)
    ///         .Argument("file", "File to operate on")
    ///         .Switch("whatif", "What if without execute")
    ///         .Option("out", "where to write the result")
    ///         .Parse();
    ///
    ///     if (cmd.ShouldExit) return cmd.ExitCode;
    ///
    ///     string file = cmd.Argument("file");
    ///     bool whatIf = cmd.Switch("whatif");
    ///
    /// Anything undeclared is an ERROR rather than something to skip past. Silently ignoring a
    /// switch is how a mistyped dry-run does the real thing and a mistyped credential runs with
    /// the wrong one -- both of which were live bugs in scripts this replaces.
    ///
    /// Help is generated from the declarations, so it cannot drift from what the script accepts,
    /// and `-help`, `-h` and `-?` are always understood without asking.
    ///
    /// The ceiling, stated so nobody has to discover it: no subcommands, no repeated options, no
    /// typed binding, and values ATTACH (`-out:file`, never `-out file` -- see Option). A script
    /// that needs more than this should reference System.CommandLine directly rather than growing
    /// this into a half-framework.
    /// </remarks>
    public class Cli
    {
        private readonly List<string> tokens;
        private readonly List<SwitchSpec> switches = new List<SwitchSpec>();
        private readonly List<ArgSpec> arguments = new List<ArgSpec>();
        private readonly List<KeyValuePair<string, string>> examples = new List<KeyValuePair<string, string>>();

        private string program;
        private string description;
        private bool usageWhenEmpty;
        private bool whatIfDeclared;

        private Cli(List<string> tokens, string program)
        {
            this.tokens = tokens;
            this.program = program;

            // Help always exists. No script is better off without it when it is generated free,
            // and a script wanting different wording just declares its own, which replaces this.
            this.switches.Add(new SwitchSpec(new[] { "help", "h", "?" }, new[] { "help", "h", "?" },
                                             "show this help", false, true));
        }

        /// <summary>
        /// Begin declaring what this script accepts.
        /// </summary>
        /// <remarks>
        /// The program name shown in the usage line is worked out from the calling script's file
        /// name, which is why scriptPath is filled in by the compiler and should not be passed.
        /// Under dotnet-script the entry assembly is `dotnet-script` rather than the script, so
        /// inferring it any other way would put the wrong name in every usage line. Program()
        /// overrides it.
        /// </remarks>
        /// <param name="args">the command line, `Args` in a .csx or `args` in a .cs</param>
        /// <param name="scriptPath">filled in by the compiler; do not pass it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">args is null</exception>
        public static Cli For(IEnumerable<string> args, [CallerFilePath] string scriptPath = null)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args), "Cli.For() needs the command line, not null.");
            }

            return new Cli(args.ToList(), ProgramFrom(scriptPath));
        }

        static string ProgramFrom(string scriptPath)
        {
            if (!String.IsNullOrEmpty(scriptPath))
            {
                var name = Path.GetFileNameWithoutExtension(scriptPath);

                // A SCRIPT is invoked by its own file name -- a .csx once .csx is on PATHEXT, a
                // .csrun through `dotnet run --file`. The entry assembly is no help for either:
                // under dotnet-script it is "dotnet-script", and under a test runner it is
                // whatever is hosting. A compiled app is the other way round, so it falls through.
                if (!String.IsNullOrEmpty(name) &&
                    (scriptPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase) ||
                     scriptPath.EndsWith(".csrun", StringComparison.OrdinalIgnoreCase)))
                {
                    return name;
                }

                var entry = Assembly.GetEntryAssembly();
                if (entry != null && !String.IsNullOrEmpty(entry.GetName().Name))
                {
                    return entry.GetName().Name;
                }

                if (!String.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            var fallback = Assembly.GetEntryAssembly();
            return fallback != null && !String.IsNullOrEmpty(fallback.GetName().Name)
                ? fallback.GetName().Name
                : "script";
        }

        /// <summary>
        /// Name the program in the generated usage, overriding the script's file name.
        /// </summary>
        /// <param name="name">what the user types to run this</param>
        /// <returns>the builder, to go on declaring</returns>
        public Cli Program(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Program() needs a name.", nameof(name));
            }

            this.program = name.Trim();
            return this;
        }

        /// <summary>
        /// The paragraph shown above the usage line, saying what the script is for.
        /// </summary>
        /// <remarks>
        /// Rendered as written apart from having its common leading whitespace removed, so a
        /// verbatim string indented inside a script still comes out flush left. The line breaks
        /// are the author's and are not re-wrapped.
        /// </remarks>
        /// <param name="text">one or more lines of prose</param>
        /// <returns>the builder, to go on declaring</returns>
        public Cli Description(string text)
        {
            this.description = text;
            return this;
        }

        /// <summary>
        /// Declare a required positional argument.
        /// </summary>
        /// <remarks>
        /// Positionals fill in declaration order. A bare word on the command line is a positional
        /// and never a candidate for the unknown-switch error -- which is what lets a script take
        /// a path without every path being rejected as a switch it does not know.
        /// </remarks>
        /// <param name="name">what it is called in the usage</param>
        /// <param name="help">the one line shown beside it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it cannot follow what is already declared</exception>
        public Cli Argument(string name, string help)
        {
            return AddArgument(name, help, true, false);
        }

        /// <summary>
        /// Declare a positional argument that may be left out.
        /// </summary>
        /// <remarks>
        /// Reads back null when omitted, so `cmd.Argument("path") ?? Directory.GetCurrentDirectory()`
        /// is the idiom. Its own method rather than a `required: false` argument, because a bare
        /// `false` in the third position reads as nothing at the call site, and because the
        /// declaration chain should read down the page the way the usage line reads across it.
        /// </remarks>
        /// <param name="name">what it is called in the usage</param>
        /// <param name="help">the one line shown beside it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it cannot follow what is already declared</exception>
        public Cli OptionalArgument(string name, string help)
        {
            return AddArgument(name, help, false, false);
        }

        /// <summary>
        /// Declare a tail that collects every positional left over.
        /// </summary>
        /// <remarks>
        /// Declaring a Rest STOPS switch parsing at the first positional: everything from there on
        /// is collected verbatim, switches and all, so a wrapper can pass `/k dir` to the program
        /// it launches. Switches before that first positional are still the script's own.
        ///
        /// The boundary is the first positional rather than the first unrecognised switch, so that
        /// a mistyped switch before it is still rejected instead of being quietly handed to a
        /// child process.
        /// </remarks>
        /// <param name="name">what it is called in the usage</param>
        /// <param name="help">the one line shown beside it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it cannot follow what is already declared</exception>
        public Cli Rest(string name, string help)
        {
            return AddArgument(name, help, false, true);
        }

        Cli AddArgument(string name, string help, bool required, bool isRest)
        {
            CheckName(name, help, "Argument");

            if (name.IndexOf('|') >= 0)
            {
                throw new ArgumentException(
                    $"Argument(\"{name}\") cannot have aliases -- positionals are matched by position, not by name.",
                    nameof(name));
            }

            if (this.arguments.Any(a => a.IsRest))
            {
                throw new InvalidOperationException(
                    $"\"{name}\" cannot be declared after a Rest -- a rest collects everything left, so nothing can follow it.");
            }

            if (required && this.arguments.Any(a => !a.Required))
            {
                var optional = this.arguments.First(a => !a.Required).Name;
                throw new InvalidOperationException(
                    $"Argument(\"{name}\") cannot follow OptionalArgument(\"{optional}\") -- an optional argument must be last.");
            }

            if (this.arguments.Any(a => String.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"\"{name}\" is already declared as an argument.");
            }

            if (Find(Normalize(name)) != null)
            {
                throw new InvalidOperationException(
                    $"\"{name}\" is already declared as a switch -- one name cannot mean both.");
            }

            this.arguments.Add(new ArgSpec(name, help, required, isRest));
            return this;
        }

        /// <summary>
        /// Declare a switch that is either on or off.
        /// </summary>
        /// <remarks>
        /// Aliases go in the name after a pipe -- `Switch("whatif|n", "...")` -- so that the second
        /// argument is ALWAYS the help text. An overload taking aliases after the help would let
        /// `Switch("whatif", "n")` compile and silently make "n" the help, which is the class of
        /// quiet mistake this whole type exists to prevent.
        ///
        /// `-whatif`, `--whatif` and `--what-if` are all the same switch: the leading dashes come
        /// off, inner hyphens and underscores go, and case is ignored.
        ///
        /// Dashes only. `/whatif` is a positional, not a switch: `--` is the near-universal
        /// standard now, and treating `/` as a prefix would make every absolute path on Linux
        /// look like a switch it had to recognise.
        /// </remarks>
        /// <param name="name">the name, optionally followed by |aliases</param>
        /// <param name="help">the one line shown beside it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it collides with something already declared</exception>
        public Cli Switch(string name, string help)
        {
            return AddSwitch(name, help, false);
        }

        /// <summary>
        /// Declare a switch that carries a value, written attached: `-out:file` or `-out=file`.
        /// </summary>
        /// <remarks>
        /// The value ATTACHES. `-out file` is not accepted, and that is a safety property rather
        /// than a shortcut: the separated form is what lets a trailing `-out` silently become a
        /// positional, and `-out -whatif` silently eat the next switch as its value. Both were
        /// live bugs in the scripts this replaces. An attached value is one token, so neither is
        /// possible, and someone typing the separated form is told so instead of being misread.
        ///
        /// Only the NAME is normalized. The value is kept exactly as typed, which is what keeps
        /// `-source:https://api.nuget.org/v3/index.json` and `-out:C:\temp\My-Folder` intact.
        ///
        /// Reads back null when not supplied, so the script writes `cmd.Option("source") ?? "..."`.
        /// There is no default parameter here because real defaults are usually computed -- an
        /// environment variable, the current directory -- and a parameter serving only constants
        /// would be two ways to say one thing.
        /// </remarks>
        /// <param name="name">the name, optionally followed by |aliases</param>
        /// <param name="help">the one line shown beside it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it collides with something already declared</exception>
        public Cli Option(string name, string help)
        {
            return AddSwitch(name, help, true);
        }

        Cli AddSwitch(string name, string help, bool takesValue)
        {
            CheckName(name, help, takesValue ? "Option" : "Switch");

            var parts = name.Split('|').Select(p => p.Trim()).ToArray();
            if (parts.Any(p => p.Length == 0))
            {
                throw new ArgumentException($"\"{name}\" has an empty name or alias between its pipes.", nameof(name));
            }

            if (parts.Any(p => p.Any(Char.IsWhiteSpace)))
            {
                throw new ArgumentException($"\"{name}\" has whitespace inside a name or alias.", nameof(name));
            }

            var keys = parts.Select(Normalize).ToArray();
            if (keys.Distinct().Count() != keys.Length)
            {
                throw new ArgumentException($"\"{name}\" names the same thing twice.", nameof(name));
            }

            foreach (var key in keys)
            {
                var clash = Find(key);
                if (clash != null && !clash.BuiltIn)
                {
                    throw new InvalidOperationException(
                        $"\"{parts[0]}\" collides with \"{clash.Primary}\" -- they are the same once case, hyphens and underscores are ignored.");
                }
            }

            if (this.arguments.Any(a => Normalize(a.Name) == keys[0]))
            {
                throw new InvalidOperationException(
                    $"\"{parts[0]}\" is already declared as an argument -- one name cannot mean both.");
            }

            // A user declaration REPLACES a built-in of the same name. That is how a script gives
            // -help its own wording without having to opt out of anything.
            foreach (var key in keys)
            {
                var builtIn = Find(key);
                if (builtIn != null)
                {
                    this.switches.Remove(builtIn);
                }
            }

            this.switches.Add(new SwitchSpec(parts, keys, help, takesValue, false));
            return this;
        }

        static void CheckName(string name, string help, string what)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{what}() needs a name.", nameof(name));
            }

            if (name[0] == '-' || name[0] == '/')
            {
                throw new ArgumentException(
                    $"{what}(\"{name}\") should be declared without a prefix -- write \"{name.TrimStart('-', '/')}\". " +
                    "Switches are written with dashes; '/' is not a prefix.",
                    nameof(name));
            }

            if (String.IsNullOrWhiteSpace(help))
            {
                throw new ArgumentException(
                    $"{what}(\"{name}\") needs the one-line help text shown in --help.", nameof(help));
            }

            // The second argument is ALWAYS the help text; aliases live in the name after a pipe.
            // Something short and word-like in that position is almost certainly an alias written
            // in the wrong place, and saying so is better than silently printing it as the help.
            if (help[0] == '-' || help[0] == '/' || (help.Trim().Length <= 4 && !help.Any(Char.IsWhiteSpace)))
            {
                throw new ArgumentException(
                    $"{what}(\"{name}\", \"{help}\") -- the second argument is the help text shown in --help, not an alias. " +
                    $"Aliases go in the name: \"{name}|{help.Trim().TrimStart('-', '/')}\".",
                    nameof(help));
            }
        }

        /// <summary>
        /// Declare the conventional dry-run switch: -whatif, also spelled --dry-run or -n.
        /// </summary>
        /// <remarks>
        /// Opt-in on purpose. A dry-run that is accepted and then ignored is worse than none at
        /// all -- it is the failure where someone asks for a rehearsal and gets the real thing.
        /// So the library declares the switch and nothing more; what a dry run MEANS is the
        /// script's to implement, and reading CliResult.WhatIf without having declared it throws
        /// rather than quietly answering false.
        /// </remarks>
        /// <returns>the builder, to go on declaring</returns>
        public Cli WhatIf()
        {
            this.whatIfDeclared = true;
            return Switch("whatif|dry-run|n", "show what would happen, without doing it");
        }

        /// <summary>
        /// Print the usage and stop when the script is run with no arguments at all.
        /// </summary>
        /// <remarks>
        /// Opt-in, because a script whose no-argument case is the real work must not print help
        /// instead of doing it. Exits 0 -- being asked for help is not a failure.
        /// </remarks>
        /// <returns>the builder, to go on declaring</returns>
        public Cli UsageWhenEmpty()
        {
            this.usageWhenEmpty = true;
            return this;
        }

        /// <summary>
        /// Add a worked example to the bottom of the generated help.
        /// </summary>
        /// <param name="commandLine">the command as it would be typed</param>
        /// <param name="help">what it does</param>
        /// <returns>the builder, to go on declaring</returns>
        public Cli Example(string commandLine, string help)
        {
            if (String.IsNullOrWhiteSpace(commandLine))
            {
                throw new ArgumentException("Example() needs the command line to show.", nameof(commandLine));
            }

            this.examples.Add(new KeyValuePair<string, string>(commandLine.Trim(), (help ?? "").Trim()));
            return this;
        }

        SwitchSpec Find(string key)
        {
            return this.switches.FirstOrDefault(s => s.Keys.Contains(key));
        }

        // Lower-cased with inner hyphens and underscores removed, so --dry-run, --dryrun and
        // -Dry_Run are one switch and --api-key and --apikey are one option.
        internal static string Normalize(string name)
        {
            var text = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (c != '-' && c != '_')
                {
                    text.Append(Char.ToLowerInvariant(c));
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// Read the command line, and stop the script if it was not valid or help was asked for.
        /// </summary>
        /// <remarks>
        /// What comes back is always usable, so a script goes straight on to reading it:
        ///
        ///     var cmd = Cli.For(Args).Switch("whatif", "touch nothing").Parse();
        ///     bool whatIf = cmd.Switch("whatif");
        ///
        /// There is nothing to check, because a command line that was not understood never gets
        /// this far. The message has already gone to standard error, or the help to standard
        /// output, and the process has exited 1 or 0 accordingly.
        ///
        /// It never throws for a BAD COMMAND LINE -- a stack trace is the wrong way to say "you
        /// typed --dryrun". It still throws for a mistake in the script itself, at the declaration
        /// that caused it.
        ///
        /// Use TryParse() where exiting is not acceptable: a test, or a Cli parsed inside a
        /// larger program that means to handle the failure itself.
        /// </remarks>
        /// <returns>the parsed command line, always readable</returns>
        public CliResult Parse()
        {
            var cmd = TryParse();

            if (cmd.ShouldExit)
            {
                Environment.Exit(cmd.ExitCode);
            }

            return cmd;
        }

        /// <summary>
        /// Read the command line without ever exiting the process.
        /// </summary>
        /// <remarks>
        /// The same work as Parse(), reported rather than acted on: check ShouldExit and use
        /// ExitCode. Everything else on the result throws until you do, so a skipped check fails
        /// loudly instead of running on with defaults it never earned.
        ///
        /// This is what Parse() is built on, and what the tests use. A script wants Parse().
        /// </remarks>
        /// <returns>the parsed command line, which may be one that should not be used</returns>
        public CliResult TryParse()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var flags = new HashSet<string>(StringComparer.Ordinal);
            var positionals = new List<string>();

            var unknown = new List<string>();
            var badValues = new List<string>();
            var terminated = false;
            var stopSwitches = false;
            var restDeclared = this.arguments.Any(a => a.IsRest);

            foreach (var raw in this.tokens)
            {
                if (terminated || stopSwitches)
                {
                    positionals.Add(raw);
                    continue;
                }

                if (raw == "--")
                {
                    terminated = true;
                    continue;
                }

                if (raw.Length == 0 || raw == "-" || raw[0] != '-')
                {
                    positionals.Add(raw);

                    // A declared Rest hands everything from the first positional onward to whatever
                    // the script is wrapping, switches included.
                    if (restDeclared)
                    {
                        stopSwitches = true;
                    }

                    continue;
                }

                var prefix = raw.StartsWith("--", StringComparison.Ordinal) ? 2 : 1;
                var body = raw.Substring(prefix);

                // Split BEFORE normalizing, on the first separator only: the name half is
                // normalized and the value half is not. The other order corrupts every value that
                // contains a hyphen, a capital, or a second colon.
                var sep = body.IndexOfAny(new[] { ':', '=' });
                var namePart = sep >= 0 ? body.Substring(0, sep) : body;
                var valuePart = sep >= 0 ? body.Substring(sep + 1) : null;

                var spec = Find(Normalize(namePart));

                if (spec == null)
                {
                    // A negative number is a value, not a mistake. Anything else starting with a
                    // dash was meant as a switch, so say that it is not one.
                    if (namePart.Length > 0 && Char.IsDigit(namePart[0]))
                    {
                        positionals.Add(raw);
                        if (restDeclared) { stopSwitches = true; }
                    }
                    else
                    {
                        unknown.Add(raw);
                    }

                    continue;
                }

                if (spec.TakesValue)
                {
                    if (valuePart == null || valuePart.Length == 0)
                    {
                        // Never echo what followed: someone typing the separated form may well have
                        // put a secret in the next token.
                        badValues.Add($"{Dash(spec.Primary)} needs a value, attached to the switch: '{Dash(spec.Primary)}:value'.");
                    }
                    else if (values.ContainsKey(spec.Keys[0]))
                    {
                        badValues.Add($"{Dash(spec.Primary)} was given more than once.");
                    }
                    else
                    {
                        values[spec.Keys[0]] = valuePart;
                    }
                }
                else
                {
                    if (valuePart != null)
                    {
                        badValues.Add($"{Dash(spec.Primary)} is a switch and takes no value -- write it as '{Dash(spec.Primary)}'.");
                    }
                    else
                    {
                        flags.Add(spec.Keys[0]);
                    }
                }
            }

            var usage = RenderUsage();
            var helpKey = this.switches.First(s => s.Keys.Contains("help")).Keys[0];
            var helpAsked = flags.Contains(helpKey);

            // Being asked for help wins over anything wrong with the rest of the line: someone
            // fumbling the syntax and reaching for --help should get --help.
            if (this.usageWhenEmpty && this.tokens.Count == 0)
            {
                Console.Out.WriteLine(usage);
                return CliResult.Exiting(this.program, 0, null, true, usage);
            }

            if (helpAsked)
            {
                Console.Out.WriteLine(usage);
                return CliResult.Exiting(this.program, 0, null, true, usage);
            }

            // Switch-level trouble is reported on its own. Once the switches were misread the
            // positional list means nothing, and reporting it as well would echo tokens -- possibly
            // a secret -- that the user never meant as arguments.
            if (unknown.Count > 0 || badValues.Count > 0)
            {
                var lines = new List<string>();
                if (unknown.Count == 1)
                {
                    lines.Add($"{this.program}: unknown switch '{unknown[0]}'");
                }
                else if (unknown.Count > 1)
                {
                    lines.Add($"{this.program}: unknown switches: {String.Join(" ", unknown.Select(u => "'" + u + "'"))}");
                }

                foreach (var bad in badValues)
                {
                    lines.Add($"{this.program}: {bad}");
                }

                return Failed(String.Join(Environment.NewLine, lines), usage);
            }

            // Fill the declared positionals in order, then the rest.
            var taken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tail = new List<string>();
            var next = 0;

            foreach (var arg in this.arguments)
            {
                if (arg.IsRest)
                {
                    while (next < positionals.Count)
                    {
                        tail.Add(positionals[next++]);
                    }

                    break;
                }

                if (next < positionals.Count)
                {
                    taken[arg.Name] = positionals[next++];
                }
            }

            var missing = this.arguments.FirstOrDefault(a => a.Required && !taken.ContainsKey(a.Name));
            if (missing != null)
            {
                return Failed($"{this.program}: missing <{missing.Name}>.", usage);
            }

            var extra = positionals.Skip(next).ToList();
            if (extra.Count == 1)
            {
                return Failed($"{this.program}: unexpected argument '{extra[0]}'.", usage);
            }

            if (extra.Count > 1)
            {
                return Failed(
                    $"{this.program}: unexpected arguments: {String.Join(" ", extra.Select(e => "'" + e + "'"))}",
                    usage);
            }

            return CliResult.Parsed(this.program, usage, flags, values, taken, tail,
                                    this.switches, this.arguments, this.whatIfDeclared);
        }

        CliResult Failed(string error, string usage)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine($"Try '{this.program} --help' for the switches it takes.");
            return CliResult.Exiting(this.program, 1, error, false, usage);
        }

        static string Dash(string name)
        {
            return name.Length == 1 ? "-" + name : "--" + name;
        }

        internal string RenderUsage()
        {
            var text = new StringBuilder();

            if (!String.IsNullOrWhiteSpace(this.description))
            {
                foreach (var prose in Dedent(this.description))
                {
                    text.AppendLine(prose);
                }

                text.AppendLine();
            }

            var spelled = this.switches.Select(Spelling).ToList();
            var line = new StringBuilder("  " + this.program);
            foreach (var arg in this.arguments)
            {
                line.Append(arg.IsRest ? $" [{arg.Name}...]" : arg.Required ? $" <{arg.Name}>" : $" [{arg.Name}]");
            }

            var withSwitches = new StringBuilder(line.ToString());
            foreach (var s in this.switches)
            {
                withSwitches.Append(" [" + Spelling(s) + "]");
            }

            text.AppendLine("Usage:");
            text.AppendLine(withSwitches.Length <= 78 ? withSwitches.ToString() : line + " [switches]");

            // One column across both sections, so the two lists line up as one block.
            var widest = 0;
            foreach (var a in this.arguments) { widest = Math.Max(widest, a.Name.Length); }
            foreach (var s in spelled) { widest = Math.Max(widest, s.Length); }
            var column = Math.Min(2 + widest + 2, 30);

            if (this.arguments.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Arguments:");
                foreach (var a in this.arguments)
                {
                    Row(text, a.Name, a.Help, column);
                }
            }

            text.AppendLine();
            text.AppendLine("Switches:");
            for (int i = 0; i < this.switches.Count; i++)
            {
                Row(text, spelled[i], this.switches[i].Help, column);
            }

            if (this.examples.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Examples:");
                foreach (var e in this.examples)
                {
                    text.AppendLine("  " + e.Key);
                    if (e.Value.Length > 0)
                    {
                        text.AppendLine("      " + e.Value);
                    }
                }
            }

            return text.ToString().TrimEnd();
        }

        static string Spelling(SwitchSpec spec)
        {
            // Every spelling the user may type, primary first, so the help teaches the aliases
            // instead of hiding them.
            var text = String.Join(", ", spec.Spellings.Select(Dash));
            return spec.TakesValue ? text + ":<value>" : text;
        }

        static void Row(StringBuilder text, string left, string help, int column)
        {
            var padded = "  " + left;
            if (padded.Length + 2 <= column)
            {
                text.AppendLine(padded.PadRight(column) + help);
            }
            else
            {
                // Too wide to share a line; the help goes underneath, still in the column.
                text.AppendLine(padded);
                text.AppendLine(new string(' ', column) + help);
            }
        }

        // Strip the indentation a verbatim string literal carries, so an indented declaration in a
        // script still renders flush left. The author's line breaks are left alone.
        internal static IEnumerable<string> Dedent(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
            while (lines.Count > 0 && lines[0].Length == 0) { lines.RemoveAt(0); }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) { lines.RemoveAt(lines.Count - 1); }

            var indent = lines.Where(l => l.Length > 0)
                              .Select(l => l.Length - l.TrimStart().Length)
                              .DefaultIfEmpty(0)
                              .Min();

            return lines.Select(l => l.Length >= indent ? l.Substring(indent) : l.TrimStart());
        }
    }

    internal class SwitchSpec
    {
        public SwitchSpec(string[] spellings, string[] keys, string help, bool takesValue, bool builtIn)
        {
            this.Spellings = spellings;
            this.Primary = spellings[0];
            this.Keys = keys;
            this.Help = help;
            this.TakesValue = takesValue;
            this.BuiltIn = builtIn;
        }

        public string Primary { get; private set; }

        // The spellings as the author wrote them. Keys are normalized for matching; these are
        // what help shows, so a switch declared "dry-run" is not advertised as "--dryrun".
        public string[] Spellings { get; private set; }

        public string[] Keys { get; private set; }

        public string Help { get; private set; }

        public bool TakesValue { get; private set; }

        public bool BuiltIn { get; private set; }
    }

    internal class ArgSpec
    {
        public ArgSpec(string name, string help, bool required, bool isRest)
        {
            this.Name = name;
            this.Help = help;
            this.Required = required;
            this.IsRest = isRest;
        }

        public string Name { get; private set; }

        public string Help { get; private set; }

        public bool Required { get; private set; }

        public bool IsRest { get; private set; }
    }
}
