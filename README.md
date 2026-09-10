![turtle](https://github.com/tomlm/CShell/raw/main/turtle.png)

[![Build and Test](https://github.com/tomlm/CShell/actions/workflows/BuildAndRunTests.yml/badge.svg)](https://github.com/tomlm/CShell/actions/workflows/BuildAndRunTests.yml)[![NuGet](https://img.shields.io/nuget/v/CShell.svg)](https://www.nuget.org/packages/CShell)

# CShell
CShell creates a runtime environment to make it easy to create C# based shell style scripts.

# Description
CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell) and runs great using [dotnet-script](https://github.com/filipw/dotnet-script) (.csx) giving 
you a great cross platform C# alternative to powershell and bash scripts.

CShell provides:
* The concept of a current folder with relative commands for navigating and manipulating files and folders
* The ability to smoothly invoke processes and pipe 
* Helpers to make it easy to work with the output of processes

By maintaining the concept of a current folder  all file and folder commands can be take absolute or 
 relative paths just like a normal shell.

CShell targets **net8.0** and depends only on
[MedallionShell](https://github.com/madelson/MedallionShell). JSON is read with the in-box
System.Text.Json.

### Properties
CShell exposes 3 properties which are the working environment of your script.  The CurrentFolder is used to resolve relative paths for
most methods, so if you call **MoveFile(@"..\foo.txt", @"..\..\bar")** it will resolve the paths and execute just like a normal shell.

| Property          | Description                                    |
|-------------------|----------------------------------------------  |
| **CurrentFolder** | The current folder as a DirectoryInfo object   |
| **FolderHistory** | List of folder paths you have navigated to     |
| **FolderStack**   | current stack from Push/Pop operations         |
| **Echo**          | Controls whether commands are echoed to output |
| **ThrowOnError**  | Controls whether to throw exception when commands have non-sucess error code |
| **RichPrompts**   | Whether the Ask methods use arrow keys or read a typed line. Null (the default) decides by asking whether standard input is redirected |
| **ReadKey**       | Where the Ask methods get their keystrokes. Null reads the console |

### Folder Methods
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate folders.

| Method      | Description                                                                  |
|-------------|------------------------------------------------------------------------------|
| **cd()**    | Change the current folder with relative or absolute path                     |
| **md()**    | Create a folder relative to current folder                                   |
| **rd()**    | Delete a folder relative to current folder                                   |
| **pushd()** | Push the current folder onto the stack and change folder to the new one      |
| **popd()**  | Pop the current folder off the stack and change the folder the popped folder |
| **exists()** | does a folder relative to current folder exist |

### File Methods
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate files.

| Method       | Description                                  |
|--------------|----------------------------------------------|
| **copy()**   | Copy a file relative to current folder       |
| **move()**   | Move a file relative to current folder       |
| **rename()** | Move a file relative to current folder       |
| **delete()** | Delete a file relative to current folder     |
| **exists()** | does a file relative to current folder exist |
| **type()**   | type a file to standardout                   |
| **cat()**    | cat a file to standardout                    |

### Output methods
CShell defines helper methods for sending output to standard out and standard error streams.
| Method           | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **Write(...)** | Alias for Console.Write() |
| **WriteLine(...)** | Alias for Console.WriteLine() |
| **print(...)** | alias for Console.WriteLine() |
| **error(...)** | alias for Console.Out.WriteLine()  |

```CSharp
WriteLine(13);
print("Hello world!");
error("ohoh!");
```

### Prompting the user
The **Ask**() family methods ask the user a question and return the answer.

| Method           | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **AskText(question)** | read a line of text, trimmed |
| **AskSecret(question)** | read without echoing anything, for tokens and passwords |
| **AskYesNo(question)** | a yes/no question, returning bool |
| **AskYesNo(question, default)** | the same, where enter accepts the default |
| **AskNumber(question)** | a whole number |
| **AskNumber(question, min, max)** | a whole number held inside a range |
| **AskChoice(question, options, label)** | pick one from a list; returns the option itself |
| **AskChoice(question, style, options, label)** | the same, choosing how the options are labelled |
| **AskMultiChoice(question, options, label)** | pick any number of them; returns an array |
| **AskMultiChoice(question, style, options, label)** | the same, choosing how the options are labelled |

```CSharp
var name    = AskText("What should I call you?");
var token   = AskSecret("Paste a token:");            // nothing appears as it is typed
var retries = AskNumber("How many retries?", 1, 5);
var push    = AskYesNo("Push straight to main?", false);

string[] fruits = ["apple", "banana", "cherry"];
var fruit = AskChoice("Pick a fruit:", fruits);              // returns "banana", not 2
var repo  = AskChoice("Pick a repo:", repos, r => r.Name);   // returns the Repo itself
var extra = AskMultiChoice("Choose your toppings:", toppings);
```

* **AskChoice** and **AskMultiChoice** are generic and return the option itself, not its position.
  The optional selector says what to show for each; without one they use `ToString()`.
* With a console they draw arrow-key prompts; with input redirected they read a typed line.
  `RichPrompts` forces either mode, `ReadKey` supplies the keystrokes.
* An option's own text is matched before its position, so a list of `"3", "1", "2"` answers the
  way it reads.
* At end of stream they throw, naming the question, rather than returning an empty answer.

`ChoiceStyle` sets the labels, and under `Letters` also what may be typed:

| Style      | Renders            | A typed answer may be              |
|------------|--------------------|------------------------------------|
| **Auto**   | nothing with arrow keys, numbers when typed | the option's text, or its number |
| **Numbers**| `1) 2) 3)`         | the option's text, or its number   |
| **Letters**| `a) b) c)`         | the option's text, or its letter   |
| **None**   | nothing            | the option's text only             |

See **askdemo.csx** for a guided tour that shows each call and then runs it.

### Cli Command processor
**Cli** declares what a script accepts and reads the command line against it. 
* An **Argument** is a positional value ("foo.txt")
* a **Switch** is on or off ("--verbose")
* an **Option** is a named value ("--output=foo.txt")

Declare each one into a variable and there is nothing else to write -- the variable's name is the
value's name, and its type is what the value is converted to:

```CSharp
Cli.For(Args)
   .Description("Opens a repository in GitHub Desktop.")
   .OptionalArgument(out string path, "the repository to open; defaults to the current directory")
   .Option(out int queueLength, "how many to queue at once")
   .Switch(out bool force, "open it even if it is already open", "f")
   .WhatIf(out bool whatIf)
   .Parse();

if (queueLength > 3)
    ...
```

| Declare into a variable | Description |
|------------------|--------------------------------------------------------------------------------------------------|
| **Argument(out T value, help)** | a required positional; `out string file` is `<file>` |
| **OptionalArgument(out T value, help)** | a positional that may be left out; keeps `default(T)` |
| **Switch(out bool value, help, aliases)** | a switch that is on or off; `out bool dryRun` is `--dry-run` |
| **Option(out T value, help, aliases)** | a switch carrying a value: `--queue-length:8` |
| **WhatIf(out bool value)** | the conventional dry run: `-whatif`, `--dry-run` or `-n` |

* Aliases are the third argument, because there is no name string to put them in:
  `.Switch(out bool force, "overwrite it", "f")`.

An `enum` gives a script a fixed set of answers, and the error that lists them is generated from
the type. It reads the same on a positional as on an option:

```CSharp
enum Color { Red, Green, Blue }

Cli.For(Args)
   .Argument(out Color color, "the colour to draw in")
   .Option(out Color? background, "the colour behind it; defaults to the terminal's")
   .Parse();
```

| Cli Method  | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **Argument(out value, help)** | a required positional, filled in declaration order |
| **OptionalArgument(out value, help)** | a positional that may be left out; reads back null |
| **Switch(out value, help, aliases)** | a switch that is on or off; aliases go in the name after a pipe: `"whatif\|n"` |
| **Option(out value, help)** | a switch carrying a value, written attached: `-out:file` |
| **WhatIf(out value)** | declares the conventional dry run: `-whatif`, also `--dry-run` or `-n` |
| **Rest(name, help)** | a tail collecting everything left, verbatim |
| **Description(text)** | the paragraph shown above the usage line |
| **Example(commandLine, help)** | a worked example for the bottom of the help |
| **Command(name, help, declare)** | a verb with a command line of its own; nests to any depth |
| **Run(handler)** / **RunAsync(handler)** | what a command does when it is the one that was typed |
| **Program(name)** | override the name in the usage line |
| **UsageWhenEmpty()** | print the usage when run with no arguments at all |
| **Parse()** | read the command line; prints and exits if it was not valid or help was asked for |
| **ParseAsync()** | the same, awaiting a command that declared RunAsync() |
| **TryParse()** / **TryParseAsync()** | the same, reported through ShouldExit rather than acted on |

Parse and TryParse return a **CliResult** object that gives access to the values.

#### Commands

A script with verbs declares a **Command** for each, and each one declares its own line the same
three ways. Commands nest, so `svc nuget push` reads the way `dotnet nuget push` does:

```CSharp
await Cli.For(Args)
    .Description("Manages the service.")
    .Command("start", "start the service", c =>
    {
        c.Argument(out string file, "the file to start");
        c.Switch(out bool force, "start it even if one is already running");
        c.Run(() => Start(file, force));
    })
    .Command("nuget", "work with the feed", c => c
        .Command("push", "push a package", p =>
        {
            p.Argument(out string package, "the .nupkg to push");
            p.RunAsync(async () => await Push(package));
        }))
    .Switch(out bool verbose, "say what is happening")
    .ParseAsync();
```

Every level has its own generated help (`svc --help`, `svc nuget push --help`), its own
unknown-switch error, and its own name in the message -- `svc nuget push: missing <package>.`
Only the command that was actually typed is ever built, so nothing binds for a command nobody
asked for.

A script that would rather switch on the verb itself declares the values with the (name, help)
overloads and reads them back:

```CSharp
var cmd = Cli.For(Args)
    .Command("start", "start it", c => c.Argument("file", "the file").Switch("force", "start anyway"))
    .Command("stop", "stop it", c => c.Option("timeout", "seconds to wait"))
    .Parse();

if (cmd.Command == "start")
    Start(cmd.Argument("file"), cmd.Switch("force"));
```

* **Run(handler)** is the only way to use a command's *typed* variables -- an `out` variable
  belongs to the block it was declared in, so it cannot be read after the lambda returns.
* What a handler returns lands on **CliResult.ExitCode**, and Parse() returns rather than exiting:
  `return Cli.For(Args)....Parse().ExitCode;`. `ShouldExit` still tells a handler's code apart
  from a command line that could not be read.
* **RunAsync()** is a separate name rather than a Run() overload, because `Run(async () => ...)`
  would bind to the void one as an async void that nobody awaits. It needs **ParseAsync()**;
  Parse() throws and says so.
* Switches at a level with commands are **global** -- typed before the command word,
  `svc --verbose start foo` -- and stay readable from the command's own result. A typed global
  must be declared *after* the first Command(), because a typed declaration reads its value as it
  runs and before then the parser does not know that the line splits. One typed after the command
  word is refused with the fix: *'--verbose' is a global switch -- write it before the command.*
* A level with commands cannot also have positionals: the first bare word is the command.
* `cmd.Command` is the verb that was typed, `cmd.CommandPath` is the whole chain, and
  `cmd.Parent` is the level above.

* No repeated options or separated values. For more complex cli support use something like [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) directly.

### Process Methods
CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell), which provides a great set of functionality for easily invoking 
processes and piping data between them.  CShell adds on location awareness and helper methods
to make it even easier to work with the output of processes.

| Method           | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **ReadFile()/cat()/type()**  | read a file and create a stream                                                        |
| **echo(text/lines/stream)** | echo text,lines from memory to a stream                                                 |
| **Run(program, arg1, ..., argN)**    | run a program and CAPTURE its output, for reading and piping |
| **Exec()/ExecAsync(program, arg1, ..., argN)** | run a program ATTACHED to this console, returning its exit code |
| **Start(program, arg1, ..., argN)**    | run a DETACHED program directly with the given args (aka Process.Start(program, args)|
| **Cmd(cmd)**  | run the cmd inside a cmd.exe, allow you to execute shell commands (like dir /b *.*                  |
| **Bash(bash)**  | run the program in bash environment, allow you to execute bash shell commands (like ls -al *      |

Three ways to run something, and the difference is where its input and output go:

| | Output | Use it for |
|---|---|---|
| **Run()**  | captured | anything you want to read, pipe, or parse |
| **Exec()** | your console | anything the user has to see and answer |
| **Start()**| its own window | anything you are not waiting for |

`Exec()` is what a shell does by default. In bash, `ssh host` or `vim` takes over the terminal and
you opt into capture with `$(...)`; `Run()` is the other way round, so anything that needs the
terminal rather than a pipe wants `Exec()`:

```CSharp
var exitCode = Exec("ssh", "user@host");
Exec("gh", "auth", "login");
Exec("git", "rebase", "-i", "HEAD~3");

await ExecAsync(opt => opt.WorkingDirectory(repo)
                          .EnvironmentVariable("EDITOR", "code --wait"),
                "git", "commit");
```

Nothing is redirected, so the program owns stdin, stdout and stderr. That is what lets a full screen
UI draw, arrow keys work, Ctrl+C reach the program rather than your script, and the window size
follow the terminal - none of which survive a pipe. It is also why there is no output to return:
reach for `Run()` when you want to read what a program printed, and `Exec()` when the user needs to
see and answer it. Passing a program that stops to ask a question to `Run()` hangs it forever on a
stdin pipe nothing will write to.

Terminal settings are restored afterwards, so a program killed before it can tidy up does not leave
your console with echo switched off.

```CSharp
// Invoke multiple commands using fluent style
var cmd1= await Run("cmd1", "args1")
    .PipeTo("cmd2", "args2", "args3")
    .PipeTo("cmd3", "args4");
var result1 = await cmd1.AsResult();

// we can even chain commands together with the pipe operator
var cmd2 = await Run("cmd1", "args1") 
    | Run("cmd2", "args2", "args3") 
    | Run("cmd3", "args4");
var result2 = await cmd2.AsResult();

// we can even chain commands together with the > operator
var = await Run("cmd1", "args1") 
    > Run("cmd2", "args2", "args3")
    > Run("cmd3", "args4");
var result3 = await cmd3.AsResult();
```

The CommandResult object has StandardOutput, StandardError information for further processing.

#### Working with results
CShell adds on helper methods to make it even easier to work with the result of a command chain.

| Method           | Description                                                                  |
|------------------|------------------------------------------------------------------------------|
| **Execute(log)**    | get the CommandResult (with stdout/stderr) of the last command               |
| **AsString(log)**   | get the standard out of the last command a string                            |
| **AsJson(log)**     | Parse the standard out of the last command as JSON: `json.owner.login`, `json["owner"]`, or assign it to a JsonObject |
| **AsJson\<T>(log)** | JSON Deserialize the standard out of the last command into a typed T         |
| **AsXml\<T>(log)**  | XML Deserialize the standard out of the last command intoa typed T           |
| **AsFile()**     | Write the stdout/stderr  of the last command to a file                       |


To call a program you await on:
1. call ReadFile()/Run()/Cmd()/Bash()/echo()
2. call any chaining commands 
3. end with a result call like Execute()/AsJson()/AsString()/AsXml()etc.

`AsJson()` gives you a **JsonDynamic**, which reads whichever way suits the script:

```CSharp
var json = await Cmd("gh api repos/tomlm/CShell").AsJson();

Console.WriteLine(json.owner.login);       // walk it with a dot
Console.WriteLine(json["stargazers_count"]);
foreach (var topic in json.topics) { }     // arrays enumerate

JsonObject o = await Cmd("gh api repos/tomlm/CShell").AsJson();   // or take the typed API
```

A property that is not there reads as null, so `if (json.optional != null)` is how you test for
one. Use `AsJson<T>()` where the shape is known.

The result methods all take a log argument is passed set to true then the commands output will be written to standard out.

```CSharp
global using static CShellNet.Globals;
using CShellNet;

Console.WriteLine("Hello world!");

// run a command and interpret the json as an AccountInfo object
var account = await Cmd("az account show").AsJson<AccountInfo>();
    
// run a command and interpret the XML as an AccountInfo object
var account2 = await Cmd("az account show -o xml").AsXml<AccountInfo>();
    
// run a command interpret the result as a string.
var accountString = await Cmd("az account show").AsString();
    
// run a command and get back the CommandResult which has Succes, StatusCode, StandardInput and StandardError.
var result = await (Run("x", "foo") | Cmd("az account show")).AsResult();
if(result.Sucess)
{
    var output = result.StandardOutput;
    ...
}
```


## CShell + dotnet-script == awesome
CShell is  a dotnet library which can be used in any .net program, but it is super useful to use from a dotnet-script (.csx) file.
There is a dotnet template to make it super easy to create a .csx file with CShell all set up to use.

To install dotnet-script support

**```dotnet tool install -g dotnet-script```**

To install the csx template

**```dotnet new --install CShell.Template```**

To invoke the template

**``` dotnet new cshell ```**

> NOTE: If you want debug support from visual studio code simply run **dotnet script init** in the same folder.

```csharp
#r "nuget: CShell, 3.0.0"
global using static CShellNet.Globals;
using CShellNet;

Console.WriteLine("Hello world!");
foreach (var arg in Args)
{
    md(arg);
    ...
}
```


### Registering .csx files to be executable on windows
You can register dotnet-script as the default handler for .csx files by running these commands:
```cmd
dotnet script register
```

After registering you can simple type **your.csx** to execute your cshell program.

> NOTE: dotnet script register will fail if visual studio code has been installed, as it registers
> itself as an editor for .csx files in a way that causes the dotnet script register command to not work correctly.
> To fix this execute:
> ```cmd
> reg delete HKCU\Software\classes\.csx /f
> dotnet script register
> ```

### Registering .csx files to be executable on MacOS/Linux
On Linux/Mac you can make a .csx file executable by
1. adding a shebang line at the top of the file 
2. running **chmod +x {yourfile}.csx**.
3. running **dos2unix {yourfile}.csx** to make sure it has unix line endings (LF \n) not windows (CRLF \r\n) line endings

```bash
#!/usr/bin/env dotnet-script
#r "nuget: CShell, 3.0.0"
global using static CShellNet.Globals;
using CShellNet;
```

### Short cut alias files 
Visual Studio Code requires the script file to have .csx extension, but you can create an alias wrapper for the script file
to make it that it can be invoked without the .csx extension.

#### Creating an script alias on Windows 
On windows if you add .csx to PATHEXT environment variable then you can invoke .csx files without the extension,
so you can create a alias file **example.csx** and then invoke it just by typing **example** in the command line.
```cmd
setx PATHEXT=%PATHEXT%;.csx
```

#### Creating an script alias on Linux/MacOS
To create an alias for **example.csx** on Linux/MacOS simply create a file **example**
```bash
#!/usr/bin/env bash
BASEDIR=$(dirname $BASH_SOURCE)
dotnet script $BASEDIR/example.csx
```
and mark it as executable
```bash
chmod +x example.csx
```

## CHANGELOG
### v3.1.0
* **Cli** now models nested commands, the way `dotnet build` and `dotnet nuget push` do
  * `.Command(name, help, c => ...)` gives a verb a command line of its own, and commands nest to any depth
  * `.Run(...)` / `.RunAsync(...)` declare what a command does; `ParseAsync()` awaits an async one
  * every level has its own generated help and its own name in its errors; only the command that was typed is built
  * switches at a command level are globals, readable from the command's result; `CliResult` gains Command, CommandPath, Parent and HandlerRan

### v3.0.0
* Added **Cli**, a declarative command line parser with generated --help
* **Cli** binds straight into typed variables: `.Option(out int queueLength, "how many to queue")` declares `--queue-length` and converts it
  * Argument()/OptionalArgument()/Rest() for positionals, Switch() for booleans, Option() for attached values
  * anything undeclared is an error; Parse() exits on a bad command line, TryParse() reports instead
* Added the **Ask** methods: AskText, AskSecret, AskYesNo, AskNumber, AskChoice, AskMultiChoice
  * AskChoice/AskMultiChoice are generic and return the option itself rather than its position
  * each has an arrow-key mode and a typed-line mode, chosen from whether input is redirected
* Added **RichPrompts** and **ReadKey** to control how the Ask methods read input
* **BREAKING** now targets net8.0 rather than netstandard2.0. .NET Framework is no longer supported
* **BREAKING** replaced Newtonsoft.Json with System.Text.Json; MedallionShell is now the only dependency
  * `AsJson()` returns a **JsonDynamic** as `dynamic`, so `json.owner.login` still works. It also indexes, enumerates, and converts to JsonNode/JsonObject/JsonArray. `AsJson<T>()` is unchanged
  * property names still match case insensitively; trailing commas, comments and a byte order mark are tolerated

### v2.1.0
* Added Write/WriteLine/print/error methods for writing to standard out and standard error

### V1.5.2
* Added Exists() methods to global

### V1.5.0
* Added Exists() methods

### V1.4.0
* Added Start() method for detached processess (you can monitor process but not access input/output)
* Added Run(Action<Option>, process, arg1, arg2) signature to control options for starting processes
* Added echo() method for piping strings/streams into .Run() commands.
* Added CShellNet.Globals which enables top-level mainless .cs/.csx projects

### V1.2.3
* Added log parameters to AsJson()/AsXml()/AsResult() output standardOut/StandardError
* added Execute() as alias for AsResult() as that seems cleaner then AsResult()

### v1.2.1
* Added ThrowOnError property to turn on/off throwing on command failing.

### v1.2.0
* Added echo(true) echo(false) to turn on off echo of the commands
* added Cmd(".....") to allow you to execute cmd.exe functions (Example: Cmd("dir /b *foo*") )
* added Bash("....") to allow you to execute bash commands (Example: Bash("ls -al") )
* Upgraded MedalianShell to 1.6.1
* upgraded JSon.Net to 12.x
