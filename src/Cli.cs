using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CShellNet
{
    /// <summary>
    /// Declares what a script accepts on its command line, and parses it.
    /// </summary>
    /// <remarks>
    /// Three words, because there are three kinds of thing: an <b>Argument</b> is a positional, a
    /// <b>Switch</b> is on or off, an <b>Option</b> carries a value.
    ///
    /// Declare each one into a variable and there is nothing else to write:
    ///
    ///     Cli.For(Args)
    ///        .Argument(out string file, "the file to operate on")
    ///        .Option(out int queueLength, "how many to queue")
    ///        .Switch(out bool whatIf, "print, do not do")
    ///        .Parse();
    ///
    ///     if (queueLength > 3) ...
    ///
    /// The variable's name IS the switch's name -- `queueLength` declares `--queue-length` -- so
    /// there is no string to keep in step with it, no misspelling that reads back null, and no
    /// cast at the far end. Parse() has already printed and exited if the line was not valid, so
    /// what the variables hold by the time it returns is good. WRITE THE TYPE: `out var` cannot
    /// be inferred and the compiler's complaint about it is no help.
    ///
    /// The same three words take a name instead, for a script that would rather read the values
    /// back from the result than declare a variable for each -- or that needs a Rest:
    ///
    ///     var cmd = Cli.For(Args)
    ///         .Argument("file", "File to operate on")
    ///         .Switch("whatif", "What if without execute")
    ///         .Option("out", "where to write the result")
    ///         .Parse();
    ///
    ///     string file = cmd.Argument("file");
    ///     bool whatIf = cmd.Switch("whatif");
    ///
    /// Anything undeclared is an ERROR rather than something to skip past. Silently ignoring a
    /// switch is how a mistyped dry-run does the real thing and a mistyped credential runs with
    /// the wrong one -- both of which were live bugs in scripts this replaces. A value that will
    /// not convert to the type asked for is the same kind of error, reported the same way.
    ///
    /// Help is generated from the declarations, so it cannot drift from what the script accepts,
    /// and `-help`, `-h` and `-?` are always understood without asking.
    ///
    /// A script with VERBS declares a Command for each, and each one declares its own line the
    /// same three ways. Commands nest, so `svc nuget push` reads the way `dotnet nuget push` does:
    ///
    ///     await Cli.For(Args)
    ///         .Command("start", "start the service", c =&gt;
    ///         {
    ///             c.Argument(out string file, "the file to start");
    ///             c.Run(() =&gt; Start(file));
    ///         })
    ///         .Command("nuget", "work with the feed", c =&gt; c
    ///             .Command("push", "push a package", p =&gt;
    ///             {
    ///                 p.Argument(out string package, "the .nupkg to push");
    ///                 p.RunAsync(async () =&gt; await Push(package));
    ///             }))
    ///         .ParseAsync();
    ///
    /// Every level has its own generated help, its own unknown-switch error, and its own name in
    /// the message -- `svc nuget push: missing &lt;package&gt;.` -- and only the command that was typed
    /// is ever built. See Command().
    ///
    /// The ceiling, stated so nobody has to discover it: no repeated options, and values ATTACH
    /// (`-out:file`, never `-out file` -- see Option). A script that needs more than this should
    /// reference System.CommandLine directly rather than growing this into a half-framework.
    /// </remarks>
    public class Cli
    {
        private readonly List<string> tokens;
        private readonly List<SwitchSpec> switches = new List<SwitchSpec>();
        private readonly List<ArgSpec> arguments = new List<ArgSpec>();
        private readonly List<CommandSpec> commands = new List<CommandSpec>();
        private readonly List<KeyValuePair<string, string>> examples = new List<KeyValuePair<string, string>>();

        private readonly List<string> conversionErrors = new List<string>();

        // The level above, and the name that got here from it. Null at the top, which is how
        // Program(), Run() and the "write it before the command" advice all tell where they are.
        private readonly Cli parent;
        private readonly string commandName;

        private string program;
        private string description;
        private bool usageWhenEmpty;
        private bool whatIfDeclared;
        private bool typedDeclared;

        private Func<int> handler;
        private Func<Task<int>> asyncHandler;

        // What the level above parsed, kept so this level's result can chain back to it and a
        // global declared at the top stays readable from the leaf.
        private CliResult parentResult;

        private Cli(List<string> tokens, string program, Cli parent, string commandName)
        {
            this.tokens = tokens;
            this.program = program;
            this.parent = parent;
            this.commandName = commandName;

            // Help always exists. No script is better off without it when it is generated free,
            // and a script wanting different wording just declares its own, which replaces this.
            this.switches.Add(new SwitchSpec(new[] { "help", "h", "?" }, new[] { "help", "h", "?" },
                                             "show this help", false, true));
        }

        /// <summary>
        /// Begin declaring what this script accepts.
        /// </summary>
        /// <remarks>
        /// The command line comes FIRST, before anything is declared, and it has to: a typed
        /// declaration fills its variable as it runs, so the tokens must already be in hand. It
        /// cannot be moved to the end as `Parse(Args)`, because an `out` parameter is a reference
        /// that lives only for the length of that one call -- a declaration cannot keep it and
        /// write to it later, and the compiler says so (CS1628). There is no way around that: ref
        /// fields exist only on ref structs, and a string cannot be pointed at.
        ///
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

            return new Cli(args.ToList(), ProgramFrom(scriptPath), null, null);
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
            // A command is already named -- by Command() -- and its name is the whole path that
            // got to it, which is what every message at that level is printed under. Letting
            // Program() overwrite that would quietly break the trail back to what to type.
            if (this.parent != null)
            {
                throw new InvalidOperationException(
                    $"Program(\"{name}\") cannot be called inside a command -- a command is named by Command(), " +
                    $"and this one is already '{this.program}'. Program() names the whole program, at the top.");
            }

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

            // A command word and a positional are the same shape of token -- a bare word in the
            // same place -- so one level cannot have both without the first one being a guess.
            if (this.commands.Count > 0)
            {
                throw new InvalidOperationException(
                    $"\"{name}\" cannot be declared beside commands -- the first bare word is the command, " +
                    $"so there is nowhere for a positional to go. Declare it inside the command that takes it.");
            }

            // A Rest changes what counts as a switch from the first positional onward, and a typed
            // declaration has already read its value by then. Rather than hand back a value read
            // under rules that no longer hold, say so at the declaration that made it ambiguous.
            if (isRest && this.typedDeclared)
            {
                throw new InvalidOperationException(
                    "Rest() cannot come after a typed declaration -- a rest hands everything after the first " +
                    "positional to the wrapped program, and the typed values were read without knowing that. " +
                    "Declare Rest() earlier in the chain (a typed Option or Switch after it is fine), or declare " +
                    "these with the (name, help) overloads and read them back from the result.");
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

            foreach (var key in keys)
            {
                if (FindCommand(key) != null)
                {
                    throw new InvalidOperationException(
                        $"\"{parts[0]}\" is already declared as a command -- one name cannot mean both.");
                }

                // A global and a command's own switch of the same name are two different values
                // that read identically, and which one a reader gets depends on which side of the
                // command word it was typed. That is the quiet kind of wrong, so it is refused.
                for (var above = this.parent; above != null; above = above.parent)
                {
                    var global = above.Find(key);
                    if (global != null && !global.BuiltIn)
                    {
                        throw new InvalidOperationException(
                            $"\"{parts[0]}\" is already declared by '{above.program}' -- a command cannot redeclare a global. " +
                            $"Read it from the result instead; a global stays readable from the command's result.");
                    }
                }
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

        // ------------------------------------------------------------------ typed declarations

        /// <summary>
        /// Declare a required positional argument, read straight into a typed variable.
        /// </summary>
        /// <remarks>
        /// The variable IS the declaration, so there is no name written twice and nothing to keep
        /// in step:
        ///
        ///     Cli.For(Args)
        ///        .Argument(out int queueLength, "how many to queue")
        ///        .Parse();
        ///
        ///     if (queueLength > 3) ...
        ///
        /// `queueLength` becomes &lt;queue-length&gt; in the usage. The name is taken from the
        /// variable by the compiler, camelCase split on the humps; since every name is normalized
        /// for matching anyway, the spelling only decides how the help reads.
        ///
        /// WRITE THE TYPE. `out var` does not compile: there is nothing for the compiler to infer T
        /// from, so it settles on the string-named Argument(name, help) instead and reports
        /// "CS1615: Argument 1 may not be passed with the 'out' keyword" -- which is true, and no
        /// help at all. `out int` compiles.
        ///
        /// The value is read AS IT IS DECLARED, which is why Cli.For() comes first. Read it after
        /// Parse(), not before: until then the command line has not been checked, and a value that
        /// was missing or would not convert is still sitting at its default.
        ///
        /// A value that will not convert is a bad command line, not an exception: it is collected
        /// and reported by Parse() alongside unknown switches, and the variable keeps its default.
        /// </remarks>
        /// <typeparam name="T">what to convert the value to -- see Option&lt;T&gt; for the list</typeparam>
        /// <param name="value">the variable to fill, whose name is the argument's name</param>
        /// <param name="help">the one line shown beside it</param>
        /// <param name="expr">filled in by the compiler; do not pass it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name cannot be read off the variable, or the help is unusable</exception>
        /// <exception cref="NotSupportedException">T cannot be made from a string</exception>
        public Cli Argument<T>(out T value, string help,
                               [CallerArgumentExpression(nameof(value))] string expr = null)
        {
            return AddTypedArgument(out value, help, expr, true, "Argument");
        }

        /// <summary>
        /// Declare a positional argument that may be left out, read into a typed variable.
        /// </summary>
        /// <remarks>
        /// Left out, the variable keeps `default(T)` -- null for a string, 0 for an int. Where 0
        /// and "not given" have to be told apart, declare it nullable and test for null:
        ///
        ///     .OptionalArgument(out int? port, "the port to listen on; defaults to 8080")
        ///     ...
        ///     var listenOn = port ?? 8080;
        ///
        /// That is deliberately the only way to say it. A defaultValue parameter would put the
        /// default in the declaration where the help text cannot see it, and real defaults are
        /// usually computed anyway.
        /// </remarks>
        /// <typeparam name="T">what to convert the value to -- see Option&lt;T&gt; for the list</typeparam>
        /// <param name="value">the variable to fill, whose name is the argument's name</param>
        /// <param name="help">the one line shown beside it</param>
        /// <param name="expr">filled in by the compiler; do not pass it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name cannot be read off the variable, or the help is unusable</exception>
        /// <exception cref="NotSupportedException">T cannot be made from a string</exception>
        public Cli OptionalArgument<T>(out T value, string help,
                                       [CallerArgumentExpression(nameof(value))] string expr = null)
        {
            return AddTypedArgument(out value, help, expr, false, "OptionalArgument");
        }

        Cli AddTypedArgument<T>(out T value, string help, string expr, bool required, string what)
        {
            var name = NameFrom(expr, what);
            AddArgument(name, help, required, false);
            this.typedDeclared = true;

            var scan = ScanTokens();
            var index = this.arguments.Count - 1;
            value = Bind<T>(index < scan.Positionals.Count ? scan.Positionals[index] : null, "<" + name + ">");
            return this;
        }

        /// <summary>
        /// Declare a switch that is either on or off, read into a bool.
        /// </summary>
        /// <remarks>
        ///     Cli.For(Args).Switch(out bool dryRun, "print, do not do").Parse();
        ///
        /// declares `--dry-run`, which -- like every switch here -- also answers to `--dryrun` and
        /// `-Dry_Run`. Aliases are the third argument rather than pipes in a name, because with
        /// the name coming off the variable there is no name to put them in:
        ///
        ///     .Switch(out bool force, "overwrite what is already there", "f")
        /// </remarks>
        /// <param name="value">the variable to fill, whose name is the switch's name</param>
        /// <param name="help">the one line shown beside it</param>
        /// <param name="aliases">other spellings, pipe-separated: "f|clobber"</param>
        /// <param name="expr">filled in by the compiler; do not pass it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name cannot be read off the variable, or the help is unusable</exception>
        /// <exception cref="InvalidOperationException">it collides with something already declared</exception>
        public Cli Switch(out bool value, string help, string aliases = null,
                          [CallerArgumentExpression(nameof(value))] string expr = null)
        {
            AddSwitch(WithAliases(NameFrom(expr, "Switch"), aliases), help, false);
            this.typedDeclared = true;

            value = ScanTokens().Flags.Contains(this.switches[this.switches.Count - 1].Keys[0]);
            return this;
        }

        /// <summary>
        /// Declare a switch carrying a value, written attached, read into a typed variable.
        /// </summary>
        /// <remarks>
        ///     Cli.For(Args).Option(out int queueLength, "how many to queue").Parse();
        ///
        ///     if (queueLength > 3) ...
        ///
        /// declares `--queue-length:8`, and queueLength is an int by the time Parse() returns.
        /// The value still ATTACHES -- `--queue-length 8` is refused, for the reasons in the
        /// string-named Option.
        ///
        /// T may be a string, a bool, a char, any of the built-in number types, an enum, a Guid, a
        /// DateTime, a DateTimeOffset, a TimeSpan, a Uri, a FileInfo or a DirectoryInfo -- or
        /// anything else with a TypeConverter that reads a string. Wrap it in Nullable to tell
        /// "not given" from a legitimate zero or false. Asking for a type that cannot be made from
        /// a string throws at the declaration, whether or not the user supplied anything, so it is
        /// found the first time the script runs rather than the first time someone passes it.
        ///
        /// A bool option is `--verbose:true`; for the bare `--verbose` form use Switch instead.
        /// </remarks>
        /// <typeparam name="T">what to convert the value to</typeparam>
        /// <param name="value">the variable to fill, whose name is the option's name</param>
        /// <param name="help">the one line shown beside it</param>
        /// <param name="aliases">other spellings, pipe-separated: "o|out"</param>
        /// <param name="expr">filled in by the compiler; do not pass it</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentException">the name cannot be read off the variable, or the help is unusable</exception>
        /// <exception cref="InvalidOperationException">it collides with something already declared</exception>
        /// <exception cref="NotSupportedException">T cannot be made from a string</exception>
        public Cli Option<T>(out T value, string help, string aliases = null,
                             [CallerArgumentExpression(nameof(value))] string expr = null)
        {
            AddSwitch(WithAliases(NameFrom(expr, "Option"), aliases), help, true);
            this.typedDeclared = true;

            var spec = this.switches[this.switches.Count - 1];
            string text;
            value = Bind<T>(ScanTokens().Values.TryGetValue(spec.Keys[0], out text) ? text : null, Dash(spec.Primary));
            return this;
        }

        /// <summary>
        /// Declare the conventional dry-run switch and read it into a bool.
        /// </summary>
        /// <remarks>
        /// The same switch WhatIf() declares -- `-whatif`, `--dry-run`, `-n` -- handed to the
        /// script as a variable instead of through CliResult.WhatIf. The name is fixed by this
        /// method rather than taken from the variable, so the switch is spelled the conventional
        /// way whatever the local is called.
        /// </remarks>
        /// <param name="value">the variable to fill</param>
        /// <returns>the builder, to go on declaring</returns>
        public Cli WhatIf(out bool value)
        {
            this.whatIfDeclared = true;
            AddSwitch("whatif|dry-run|n", "show what would happen, without doing it", false);
            this.typedDeclared = true;

            value = ScanTokens().Flags.Contains("whatif");
            return this;
        }

        static string WithAliases(string name, string aliases)
        {
            return String.IsNullOrWhiteSpace(aliases) ? name : name + "|" + aliases.Trim().Trim('|');
        }

        // The switch's name is the variable's name, which is the whole point: there is no second
        // place for it to be written, so the two cannot drift. The compiler hands over the
        // argument as it was typed -- "out int queueLength" -- and the trailing identifier is the
        // name. camelCase is split on the humps, because --queue-length is how a switch is
        // spelled; normalizing means --queuelength and --queue_length still find it.
        internal static string NameFrom(string expr, string what)
        {
            if (String.IsNullOrWhiteSpace(expr))
            {
                throw new ArgumentException(
                    $"{what}() takes its name from the variable, and the compiler did not say what the variable was. " +
                    $"Use {what}(name, help) instead.",
                    "expr");
            }

            var text = expr.Trim();

            // out this.count, out config.Retries -- the member is the name, not the path to it.
            var dot = text.LastIndexOf('.');
            if (dot >= 0)
            {
                text = text.Substring(dot + 1);
            }

            var start = text.Length;
            while (start > 0 && (Char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            {
                start--;
            }

            var name = text.Substring(start).TrimStart('_');

            if (name.Length == 0 || Char.IsDigit(name[0]))
            {
                throw new ArgumentException(
                    $"{what}() takes its name from the variable, and \"{expr}\" has no name to take. " +
                    $"Pass a variable -- {what}(out int queueLength, \"...\") -- or use {what}(name, help).",
                    "expr");
            }

            return Kebab(name);
        }

        // queueLength -> queue-length, apiKey -> api-key, maxCPU -> max-cpu, queue_length ->
        // queue-length. A run of capitals is one word until the last of them starts a new one.
        internal static string Kebab(string identifier)
        {
            var text = new StringBuilder(identifier.Length + 4);

            for (int i = 0; i < identifier.Length; i++)
            {
                var c = identifier[i];

                if (Char.IsUpper(c) && i > 0)
                {
                    var previous = identifier[i - 1];
                    var startsWord = !Char.IsUpper(previous) ||
                                     (i + 1 < identifier.Length && Char.IsLower(identifier[i + 1]));

                    if (startsWord && text.Length > 0 && text[text.Length - 1] != '-')
                    {
                        text.Append('-');
                    }
                }

                text.Append(c == '_' ? '-' : Char.ToLowerInvariant(c));
            }

            return text.ToString().Trim('-');
        }

        // Convert what was typed, or leave the variable at its default when nothing was. What went
        // wrong is COLLECTED rather than thrown: a user typing a word where a number goes is a bad
        // command line, and Parse() reports it the way it reports an unknown switch.
        T Bind<T>(string text, string display)
        {
            // Checked even when nothing was given, so a type that could never have worked is found
            // on the first run rather than the first time someone passes that switch.
            if (!Convertible(Underlying(typeof(T))))
            {
                throw new NotSupportedException(
                    $"{display} is declared as {typeof(T).Name}, which cannot be made from a string. " +
                    "Take it as a string and convert it in the script.");
            }

            if (text == null)
            {
                return default(T);
            }

            object result;
            if (TryConvert(Underlying(typeof(T)), text, out result))
            {
                return (T)result;
            }

            this.conversionErrors.Add($"{display} expects {Expected(Underlying(typeof(T)))}, but got '{text}'.");
            return default(T);
        }

        static Type Underlying(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        static bool Convertible(Type target)
        {
            if (target == typeof(string) || target == typeof(bool) || target.IsEnum ||
                target == typeof(Uri) || target == typeof(FileInfo) || target == typeof(DirectoryInfo))
            {
                return true;
            }

            try
            {
                var converter = TypeDescriptor.GetConverter(target);
                return converter != null && converter.CanConvertFrom(typeof(string));
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool TryConvert(Type target, string text, out object result)
        {
            result = null;

            if (target == typeof(string))
            {
                result = text;
                return true;
            }

            if (target == typeof(bool))
            {
                switch (text.Trim().ToLowerInvariant())
                {
                    case "true":
                    case "yes":
                    case "y":
                    case "on":
                    case "1":
                        result = true;
                        return true;

                    case "false":
                    case "no":
                    case "n":
                    case "off":
                    case "0":
                        result = false;
                        return true;

                    default:
                        return false;
                }
            }

            if (target.IsEnum)
            {
                try
                {
                    result = Enum.Parse(target, text.Trim(), true);
                }
                catch (Exception)
                {
                    return false;
                }

                // Enum.Parse takes any number at all, named or not. A [Flags] enum is the one case
                // where a combination nobody declared is still a real value.
                if (!Enum.IsDefined(target, result) && !target.IsDefined(typeof(FlagsAttribute), false))
                {
                    result = null;
                    return false;
                }

                return true;
            }

            // Paths are kept exactly as typed. A TypeConverter would go looking at the file system,
            // and whether the file is there is the script's question to ask, not the parser's.
            if (target == typeof(FileInfo) || target == typeof(DirectoryInfo))
            {
                try
                {
                    result = target == typeof(FileInfo) ? (object)new FileInfo(text) : new DirectoryInfo(text);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (target == typeof(Uri))
            {
                Uri uri;
                if (Uri.TryCreate(text.Trim(), UriKind.RelativeOrAbsolute, out uri))
                {
                    result = uri;
                    return true;
                }

                return false;
            }

            try
            {
                // Invariant, not the current culture: a script has to read the same command line
                // the same way on a machine whose decimal separator is a comma.
                result = TypeDescriptor.GetConverter(target).ConvertFromInvariantString(text.Trim());
                return result != null;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        // What to tell someone who typed the wrong thing. Says what was wanted rather than naming
        // the CLR type, because "expects a whole number" is what they needed to know and "expects
        // an Int32" is not.
        static string Expected(Type target)
        {
            if (target == typeof(bool)) { return "true or false"; }

            if (target.IsEnum) { return "one of: " + String.Join(", ", Enum.GetNames(target)); }

            if (target == typeof(sbyte) || target == typeof(byte) || target == typeof(short) ||
                target == typeof(ushort) || target == typeof(int) || target == typeof(uint) ||
                target == typeof(long) || target == typeof(ulong))
            {
                return "a whole number";
            }

            if (target == typeof(float) || target == typeof(double) || target == typeof(decimal))
            {
                return "a number";
            }

            if (target == typeof(DateTime) || target == typeof(DateTimeOffset)) { return "a date"; }

            if (target == typeof(TimeSpan)) { return "a length of time, like 00:05:00"; }

            if (target == typeof(Guid)) { return "a guid"; }

            if (target == typeof(Uri)) { return "a url"; }

            if (target == typeof(char)) { return "a single character"; }

            return "a " + target.Name;
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

        // ------------------------------------------------------------------ commands

        /// <summary>
        /// Declare a verb with a command line of its own.
        /// </summary>
        /// <remarks>
        /// A command is a Cli in its own right. It gets the tokens after its own name, declares
        /// them with the same three words, and has its own generated help and its own name in
        /// every message it prints -- `svc nuget push: missing &lt;package&gt;.`
        ///
        ///     Cli.For(Args)
        ///        .Command("start", "start the service", c =&gt;
        ///        {
        ///            c.Argument(out string file, "the file to start");
        ///            c.Run(() =&gt; Start(file));
        ///        })
        ///        .Parse();
        ///
        /// Commands NEST, because the thing handed to the lambda is a Cli and a Cli takes
        /// commands: `.Command("nuget", ..., c =&gt; c.Command("push", ...))` is `svc nuget push`.
        ///
        /// The lambda runs only for the command that was actually typed. That is what lets each
        /// command declare typed variables of its own -- nothing binds for a command nobody asked
        /// for -- and it is why `--help` at this level can list the commands without building any
        /// of them. The cost, stated so it is not a surprise: a mistake INSIDE a lambda, such as
        /// asking for a type that cannot be made from a string, is found the first time that
        /// command is typed rather than the first time the script is run.
        ///
        /// Aliases go in the name after a pipe -- `Command("remove|rm", "...")` -- exactly as they
        /// do on a Switch, and the name is matched the same way: case, hyphens and underscores are
        /// ignored, so `Command("dry-run", ...)` also answers to `dryrun`.
        ///
        /// A level with commands cannot also have positionals: the first bare word is the command,
        /// so there is nowhere for one to go. Switches at this level are GLOBAL -- they are typed
        /// before the command word, `svc --verbose start foo`, and stay readable from the
        /// command's own result. Typed globals must be declared AFTER the first Command(), because
        /// a typed declaration reads its value as it runs and before the first Command() this
        /// level does not yet know that the line splits.
        ///
        /// WATCH THE VARIABLE NAMES. The lambda's body is nested inside the script's own scope, so
        /// `c.Argument(out string file, ...)` will not compile if the script already has a `file`
        /// further down (CS0136). Two sibling commands may each declare `file`; the enclosing
        /// script may not have one too.
        /// </remarks>
        /// <param name="name">the verb, optionally followed by |aliases</param>
        /// <param name="help">the one line shown beside it in the command list</param>
        /// <param name="declare">what the command accepts, and what it does</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">declare is null</exception>
        /// <exception cref="ArgumentException">the name or help is unusable</exception>
        /// <exception cref="InvalidOperationException">it cannot follow what is already declared</exception>
        public Cli Command(string name, string help, Action<Cli> declare)
        {
            CheckName(name, help, "Command");

            if (declare == null)
            {
                throw new ArgumentNullException(nameof(declare),
                    $"Command(\"{name}\") needs the lambda that declares what it accepts.");
            }

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

            // The same reason Rest() cannot follow one: a command word moves where this level's
            // switches stop, and a typed declaration has already read its value without knowing
            // that. Rather than hand back a value read under rules that no longer hold, say so at
            // the declaration that made it ambiguous.
            if (this.typedDeclared)
            {
                throw new InvalidOperationException(
                    $"Command(\"{parts[0]}\") cannot come after a typed declaration -- the command word is where this " +
                    "level's switches stop, and the typed values were read without knowing that. Declare every " +
                    "Command() first, then the typed globals after them.");
            }

            if (this.arguments.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Command(\"{parts[0]}\") cannot be declared beside <{this.arguments[0].Name}> -- the first bare " +
                    "word is either a command or a positional, and it cannot be worked out which.");
            }

            if (this.handler != null || this.asyncHandler != null)
            {
                throw new InvalidOperationException(
                    $"Command(\"{parts[0]}\") cannot be declared beside a Run() -- a level either does something " +
                    "itself or hands off to commands that do.");
            }

            foreach (var key in keys)
            {
                if (FindCommand(key) != null)
                {
                    throw new InvalidOperationException(
                        $"\"{parts[0]}\" collides with \"{FindCommand(key).Primary}\" -- they are the same command " +
                        "once case, hyphens and underscores are ignored.");
                }

                var clash = Find(key);
                if (clash != null && !clash.BuiltIn)
                {
                    throw new InvalidOperationException(
                        $"\"{parts[0]}\" is already declared as a switch -- one name cannot mean both.");
                }
            }

            this.commands.Add(new CommandSpec(parts, keys, help, declare));
            return this;
        }

        /// <summary>
        /// Say what this command does when it is the one that was typed.
        /// </summary>
        /// <remarks>
        /// Declared beside the command's own declarations, so the variables it uses are the ones
        /// just declared and there is nothing to pass:
        ///
        ///     .Command("start", "start the service", c =&gt;
        ///     {
        ///         c.Argument(out string file, "the file to start");
        ///         c.Run(() =&gt; Start(file));
        ///     })
        ///
        /// This is the ONLY way to use a command's typed variables, and not by choice: an out
        /// variable belongs to the block it was declared in, so it cannot be read after the lambda
        /// returns. A script that would rather read its values back at the end declares them with
        /// the (name, help) overloads and reads them off the result, which knows which command was
        /// chosen -- see CliResult.Command.
        ///
        /// The handler runs only after the whole line has been read and found good, so a value
        /// that was missing or would not convert stops it from running at all.
        ///
        /// Run() belongs to a command. At the top level the script's own code is the handler --
        /// the variables are already in scope where Parse() returns -- and a level that has
        /// commands hands off to them rather than doing anything itself.
        /// </remarks>
        /// <param name="handler">what to do</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">handler is null</exception>
        /// <exception cref="InvalidOperationException">this is not a leaf command, or it already has a handler</exception>
        public Cli Run(Action handler)
        {
            if (handler == null) { throw new ArgumentNullException(nameof(handler), "Run() needs something to do."); }

            CheckHandler("Run");
            this.handler = () => { handler(); return 0; };
            return this;
        }

        /// <summary>
        /// Say what this command does, and what the script should exit with.
        /// </summary>
        /// <remarks>
        /// What comes back lands on CliResult.ExitCode, and Parse() returns rather than exiting --
        /// a command that ran is not a command line that could not be read, and the script says
        /// how it ends: `return Cli.For(Args)....Parse().ExitCode;`
        ///
        /// ShouldExit tells the two apart. It is false here, whatever the code; it is true only
        /// when the line was not understood, which is the case Parse() exits for.
        /// </remarks>
        /// <param name="handler">what to do, and what to exit with</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">handler is null</exception>
        /// <exception cref="InvalidOperationException">this is not a leaf command, or it already has a handler</exception>
        public Cli Run(Func<int> handler)
        {
            if (handler == null) { throw new ArgumentNullException(nameof(handler), "Run() needs something to do."); }

            CheckHandler("Run");
            this.handler = handler;
            return this;
        }

        /// <summary>
        /// Say what this command does, when doing it is asynchronous.
        /// </summary>
        /// <remarks>
        ///     .Command("push", "push a package", p =&gt;
        ///     {
        ///         p.Argument(out string package, "the .nupkg to push");
        ///         p.RunAsync(async () =&gt; await Push(package));
        ///     })
        ///
        /// then `await ...ParseAsync()` rather than Parse().
        ///
        /// Its own name rather than another Run() overload, because `Run(async () =&gt; ...)` would
        /// happily bind to Run(Action) as an async void that nobody ever awaits -- the script
        /// would exit while the work was still running, and nothing would say so. A separate name
        /// cannot be got wrong that way.
        ///
        /// Parse() throws when an async handler was declared, naming ParseAsync(), rather than
        /// blocking on it.
        /// </remarks>
        /// <param name="handler">what to do</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">handler is null</exception>
        /// <exception cref="InvalidOperationException">this is not a leaf command, or it already has a handler</exception>
        public Cli RunAsync(Func<Task> handler)
        {
            if (handler == null) { throw new ArgumentNullException(nameof(handler), "RunAsync() needs something to do."); }

            CheckHandler("RunAsync");
            this.asyncHandler = async () => { await handler().ConfigureAwait(false); return 0; };
            return this;
        }

        /// <summary>
        /// Say what this command does asynchronously, and what the script should exit with.
        /// </summary>
        /// <remarks>
        /// The pairing of RunAsync(Func&lt;Task&gt;) and Run(Func&lt;int&gt;): awaited by ParseAsync(),
        /// and what it returns lands on CliResult.ExitCode without exiting the process.
        /// </remarks>
        /// <param name="handler">what to do, and what to exit with</param>
        /// <returns>the builder, to go on declaring</returns>
        /// <exception cref="ArgumentNullException">handler is null</exception>
        /// <exception cref="InvalidOperationException">this is not a leaf command, or it already has a handler</exception>
        public Cli RunAsync(Func<Task<int>> handler)
        {
            if (handler == null) { throw new ArgumentNullException(nameof(handler), "RunAsync() needs something to do."); }

            CheckHandler("RunAsync");
            this.asyncHandler = handler;
            return this;
        }

        void CheckHandler(string what)
        {
            if (this.parent == null)
            {
                throw new InvalidOperationException(
                    $"{what}() belongs to a command, and this is the top level -- the script's own code runs when " +
                    "Parse() returns, with the variables already in scope. Declare a Command() and put it there.");
            }

            if (this.commands.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{what}() cannot be declared beside commands -- '{this.program}' hands off to its commands, " +
                    "so put the handler on the one that does the work.");
            }

            if (this.handler != null || this.asyncHandler != null)
            {
                throw new InvalidOperationException(
                    $"'{this.program}' already has a handler -- a command does one thing.");
            }
        }

        CommandSpec FindCommand(string key)
        {
            return this.commands.FirstOrDefault(c => c.Keys.Contains(key));
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
        /// Read the command line and await the command's handler, stopping the script if it was
        /// not valid or help was asked for.
        /// </summary>
        /// <remarks>
        /// Parse() for a script whose commands declared RunAsync(). Everything else is the same,
        /// including exiting for a line that could not be read and NOT exiting for a handler that
        /// returned a code -- that lands on ExitCode for the script to return.
        ///
        /// Parse() throws rather than blocking when an async handler was declared, so a script
        /// that forgot the await is told, not left to exit while the work is still running.
        /// </remarks>
        /// <returns>the parsed command line, always readable</returns>
        public async Task<CliResult> ParseAsync()
        {
            var cmd = await TryParseAsync().ConfigureAwait(false);

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
            Cli leaf;
            var cmd = Resolve(null, out leaf);

            if (cmd.ShouldExit)
            {
                return cmd;
            }

            if (leaf.asyncHandler != null)
            {
                throw new InvalidOperationException(
                    $"'{leaf.program}' declared an async handler with RunAsync(), so it has to be awaited -- " +
                    "call ParseAsync() instead of Parse().");
            }

            if (leaf.handler != null)
            {
                cmd.Ran(leaf.handler());
            }

            return cmd;
        }

        /// <summary>
        /// Read the command line and await the command's handler, without ever exiting the process.
        /// </summary>
        /// <remarks>
        /// TryParse() for a script whose commands declared RunAsync(). A command that declared a
        /// plain Run() still works here, so a script with a mix of both needs only this one.
        /// </remarks>
        /// <returns>the parsed command line, which may be one that should not be used</returns>
        public async Task<CliResult> TryParseAsync()
        {
            Cli leaf;
            var cmd = Resolve(null, out leaf);

            if (cmd.ShouldExit)
            {
                return cmd;
            }

            if (leaf.asyncHandler != null)
            {
                cmd.Ran(await leaf.asyncHandler().ConfigureAwait(false));
            }
            else if (leaf.handler != null)
            {
                cmd.Ran(leaf.handler());
            }

            return cmd;
        }

        // Everything TryParse() does except run the handler: read this level, and either finish
        // here or hand what is left to the command that was named and let it do the same. What
        // comes back is the LEAF's result -- the level whose line was actually being read -- with
        // Parent chaining back up, and `leaf` is the Cli it came from, which is where the handler
        // lives.
        CliResult Resolve(CliResult above, out Cli leaf)
        {
            leaf = this;
            this.parentResult = above;

            var scan = ScanTokens();
            var values = scan.Values;
            var flags = scan.Flags;
            var positionals = scan.Positionals;
            var unknown = scan.Unknown;

            // A value that would not convert is a bad command line like any other, reported with
            // the rest rather than thrown: the script author asked for an int, the user typed a
            // word, and a stack trace is the wrong way to say so.
            var badValues = new List<string>(scan.BadValues);
            badValues.AddRange(this.conversionErrors);

            var usage = RenderUsage();
            var helpKey = this.switches.First(s => s.Keys.Contains("help")).Keys[0];
            var helpAsked = flags.Contains(helpKey);

            // Being asked for help wins over anything wrong with the rest of the line: someone
            // fumbling the syntax and reaching for --help should get --help.
            if (this.usageWhenEmpty && this.tokens.Count == 0)
            {
                Console.Out.WriteLine(usage);
                return CliResult.Exiting(this.program, 0, null, true, usage, this.parentResult, this.commandName);
            }

            if (helpAsked)
            {
                Console.Out.WriteLine(usage);
                return CliResult.Exiting(this.program, 0, null, true, usage, this.parentResult, this.commandName);
            }

            // Switch-level trouble is reported on its own. Once the switches were misread the
            // positional list means nothing, and reporting it as well would echo tokens -- possibly
            // a secret -- that the user never meant as arguments. It is also reported BEFORE the
            // command is resolved, so a command whose globals were misread is never built.
            if (unknown.Count > 0 || badValues.Count > 0)
            {
                var lines = new List<string>();
                var strays = new List<string>();

                foreach (var token in unknown)
                {
                    var declaredAbove = DeclaredAbove(token);
                    if (declaredAbove == null)
                    {
                        strays.Add(token);
                    }
                    else
                    {
                        // Typed on the wrong side of the command word. Saying where it goes is the
                        // difference between a dead end and a fix.
                        var below = this.program.Substring(declaredAbove.program.Length).Trim();
                        lines.Add($"{this.program}: '{token}' is a global switch -- write it before the command: " +
                                  $"'{declaredAbove.program} {token} {below}'.");
                    }
                }

                if (strays.Count == 1)
                {
                    lines.Add($"{this.program}: unknown switch '{strays[0]}'");
                }
                else if (strays.Count > 1)
                {
                    lines.Add($"{this.program}: unknown switches: {String.Join(" ", strays.Select(u => "'" + u + "'"))}");
                }

                foreach (var bad in badValues)
                {
                    lines.Add($"{this.program}: {bad}");
                }

                return Failed(String.Join(Environment.NewLine, lines), usage);
            }

            // A level with commands reads its own globals and then gets out of the way: everything
            // from the command word on belongs to the command, which reads it the same way.
            if (this.commands.Count > 0)
            {
                var mine = CliResult.Parsed(this.program, usage, flags, values,
                                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                            new List<string>(), this.switches, this.arguments,
                                            this.whatIfDeclared, this.parentResult, this.commandName);

                if (scan.CommandIndex < 0)
                {
                    return Failed($"{this.program}: no command given -- one of: {CommandList()}.", usage);
                }

                var word = this.tokens[scan.CommandIndex];
                var chosen = FindCommand(Normalize(word));

                if (chosen == null)
                {
                    return Failed($"{this.program}: unknown command '{word}' -- expected one of: {CommandList()}.", usage);
                }

                var child = new Cli(this.tokens.Skip(scan.CommandIndex + 1).ToList(),
                                    this.program + " " + chosen.Primary, this, chosen.Primary);

                // Only now, and only for the command that was actually typed: this is where its
                // typed declarations bind, against its own tokens.
                chosen.Declare(child);

                return child.Resolve(mine, out leaf);
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
                                    this.switches, this.arguments, this.whatIfDeclared,
                                    this.parentResult, this.commandName);
        }

        CliResult Failed(string error, string usage)
        {
            var takes = this.commands.Count > 0 ? "commands" : "switches";

            Console.Error.WriteLine(error);
            Console.Error.WriteLine($"Try '{this.program} --help' for the {takes} it takes.");
            return CliResult.Exiting(this.program, 1, error, false, usage, this.parentResult, this.commandName);
        }

        string CommandList()
        {
            return String.Join(", ", this.commands.Select(c => c.Primary));
        }

        // Which level above declared this switch, if any. An unknown switch inside a command is
        // very often a global typed after the command word instead of before it.
        Cli DeclaredAbove(string raw)
        {
            var body = raw.TrimStart('-');
            var sep = body.IndexOfAny(new[] { ':', '=' });
            var key = Normalize(sep >= 0 ? body.Substring(0, sep) : body);

            for (var above = this.parent; above != null; above = above.parent)
            {
                var spec = above.Find(key);
                if (spec != null && !spec.BuiltIn)
                {
                    return above;
                }
            }

            return null;
        }

        static string Dash(string name)
        {
            return name.Length == 1 ? "-" + name : "--" + name;
        }

        // What one pass over the tokens found. Named parts rather than a tuple, because the
        // whole point is that Parse() and a typed declaration are looking at the same five things.
        class Scanned
        {
            public readonly HashSet<string> Flags = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> Positionals = new List<string>();
            public readonly List<string> Unknown = new List<string>();
            public readonly List<string> BadValues = new List<string>();

            // Where the command word is, at a level that has commands; -1 when none was given.
            // Everything from here on belongs to the command, so the scan stops.
            public int CommandIndex = -1;
        }

        // The one place the command line is turned into flags, values and positionals. Both the
        // typed declarations -- which read their value as they are declared -- and Parse() go
        // through here, so what a variable was bound to and what Parse() validates can never be
        // two different readings of the same tokens.
        Scanned ScanTokens()
        {
            var scan = new Scanned();
            var terminated = false;
            var stopSwitches = false;
            var restDeclared = this.arguments.Any(a => a.IsRest);

            // Where a level has commands, the first thing that is not a switch is the command
            // word, and the scan stops there: the rest is the command's line, not this one's.
            // The boundary is only unambiguous because an option's value ATTACHES -- with a
            // separated value, `svc --out foo build` could not be told apart from `svc --out:foo
            // build` with a stray positional.
            var hasCommands = this.commands.Count > 0;

            for (int i = 0; i < this.tokens.Count; i++)
            {
                var raw = this.tokens[i];

                if (terminated || stopSwitches)
                {
                    if (hasCommands)
                    {
                        scan.CommandIndex = i;
                        return scan;
                    }

                    scan.Positionals.Add(raw);
                    continue;
                }

                if (raw == "--")
                {
                    terminated = true;
                    continue;
                }

                if (raw.Length == 0 || raw == "-" || raw[0] != '-')
                {
                    if (hasCommands)
                    {
                        scan.CommandIndex = i;
                        return scan;
                    }

                    scan.Positionals.Add(raw);

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
                        if (hasCommands)
                        {
                            scan.CommandIndex = i;
                            return scan;
                        }

                        scan.Positionals.Add(raw);
                        if (restDeclared) { stopSwitches = true; }
                    }
                    else
                    {
                        scan.Unknown.Add(raw);
                    }

                    continue;
                }

                if (spec.TakesValue)
                {
                    if (valuePart == null || valuePart.Length == 0)
                    {
                        // Never echo what followed: someone typing the separated form may well have
                        // put a secret in the next token.
                        scan.BadValues.Add($"{Dash(spec.Primary)} needs a value, attached to the switch: '{Dash(spec.Primary)}:value'.");
                    }
                    else if (scan.Values.ContainsKey(spec.Keys[0]))
                    {
                        scan.BadValues.Add($"{Dash(spec.Primary)} was given more than once.");
                    }
                    else
                    {
                        scan.Values[spec.Keys[0]] = valuePart;
                    }
                }
                else
                {
                    if (valuePart != null)
                    {
                        scan.BadValues.Add($"{Dash(spec.Primary)} is a switch and takes no value -- write it as '{Dash(spec.Primary)}'.");
                    }
                    else
                    {
                        scan.Flags.Add(spec.Keys[0]);
                    }
                }
            }

            return scan;
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
            var named = this.commands.Select(c => String.Join(", ", c.Spellings)).ToList();

            text.AppendLine("Usage:");

            if (this.commands.Count > 0)
            {
                // Switches come BEFORE the command word here, because that is where they have to
                // be typed, so the usage line teaches the grammar in the order the tokens go.
                var head = "  " + this.program;
                var withGlobals = new StringBuilder(head);
                foreach (var s in this.switches)
                {
                    withGlobals.Append(" [" + Spelling(s) + "]");
                }

                withGlobals.Append(" <command> ...");
                text.AppendLine(withGlobals.Length <= 78 ? withGlobals.ToString() : head + " [switches] <command> ...");
            }
            else
            {
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

                text.AppendLine(withSwitches.Length <= 78 ? withSwitches.ToString() : line + " [switches]");
            }

            // One column across every section, so the lists line up as one block.
            var widest = 0;
            foreach (var a in this.arguments) { widest = Math.Max(widest, a.Name.Length); }
            foreach (var c in named) { widest = Math.Max(widest, c.Length); }
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

            if (this.commands.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Commands:");
                for (int i = 0; i < this.commands.Count; i++)
                {
                    Row(text, named[i], this.commands[i].Help, column);
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

            if (this.commands.Count > 0)
            {
                text.AppendLine();
                text.AppendLine($"See '{this.program} <command> --help' for what a command takes.");
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

    internal class CommandSpec
    {
        public CommandSpec(string[] spellings, string[] keys, string help, Action<Cli> declare)
        {
            this.Spellings = spellings;
            this.Primary = spellings[0];
            this.Keys = keys;
            this.Help = help;
            this.Declare = declare;
        }

        public string Primary { get; private set; }

        // As the author wrote them, for the command list. Keys are what a typed word is matched
        // against, normalized the same way a switch's are.
        public string[] Spellings { get; private set; }

        public string[] Keys { get; private set; }

        public string Help { get; private set; }

        // Held, not run. It runs once, for the command that was actually typed.
        public Action<Cli> Declare { get; private set; }
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
