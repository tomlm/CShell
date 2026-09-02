<img src="https://github.com/tomlm/CShell/raw/master/turtle.png" width="100"/>

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

### Asking the user
The **Ask** methods are the questions a *script* asks the *user*. (For the other direction, a
process that asks the user something itself, see the remarks on **Run()**.)

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
var fruit = AskChoice("Pick a fruit:", fruits);       // returns "banana", not 2

var repo  = AskChoice("Pick a repo:", repos, r => r.Name);   // returns the Repo itself
var extra = AskMultiChoice("Choose your toppings:", toppings);
```

**AskChoice** and **AskMultiChoice** are generic. They return the option itself rather than its
position, and an optional selector says what to show for each, so you can hand them your own
objects and get one back with no lookup. Without a selector they use `ToString()`.

Every Ask method has **two modes and picks between them itself**. With a console it draws a rich
prompt -- a selection you move with the arrow keys, redrawn as it changes. With standard input
redirected it reads a typed line instead. That is not cosmetic: `Console.ReadKey()` throws when
input is redirected, so a script that is piped, scheduled or running under CI has no keys to read
and needs a typed twin rather than a degraded version of the same thing. `RichPrompts` overrides
the choice and `ReadKey` supplies the keystrokes.

`ChoiceStyle` decides how the options are labelled, and under `Letters` it also decides what may
be typed:

| Style      | Renders            | A typed answer may be              |
|------------|--------------------|------------------------------------|
| **Auto**   | nothing when there are arrow keys, numbers when the answer must be typed | the option's text, or its number |
| **Numbers**| `1) 2) 3)`         | the option's text, or its number   |
| **Letters**| `a) b) c)`         | the option's text, or its letter -- a bare `2` names nothing |
| **None**   | nothing            | the option's text only             |

The option's own text is matched **before** its position, so a list whose options are themselves
numbers -- `"3", "1", "2"` -- answers the way it reads: typing `3` picks the option labelled 3
rather than the third one.

Every Ask method throws rather than answering for someone who is not there: at end of stream it
says which question went unanswered, instead of taking an empty answer or spinning forever on a
console nobody is attached to.

See **askdemo.csx** in this repo for a guided tour that shows each call and then runs it.

### Command line
**Cli** declares what a script accepts and reads the command line against it. Three words, because
there are three kinds of thing: an **Argument** is a positional, a **Switch** is on or off, and an
**Option** carries a value. The same three words read the values back, so the block that reads a
command line can be checked line for line against the block that declared it.

```CSharp
var cmd = Cli.For(Args)
    .Description("Opens a repository in GitHub Desktop.")
    .OptionalArgument("path", "the repository to open; defaults to the current directory")
    .WhatIf()
    .Option("source", "the feed to use; defaults to nuget.org")
    .Parse();

if (cmd.ShouldExit)
    return cmd.ExitCode;

var path   = cmd.Argument("path") ?? Directory.GetCurrentDirectory();
var source = cmd.Option("source") ?? "https://api.nuget.org/v3/index.json";
var whatIf = cmd.WhatIf;
```

| Declare on Cli   | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **Argument(name, help)** | a required positional, filled in declaration order |
| **OptionalArgument(name, help)** | a positional that may be left out; reads back null |
| **Rest(name, help)** | a tail collecting everything left, verbatim |
| **Switch(name, help)** | a switch that is on or off; aliases go in the name after a pipe: `"whatif\|n"` |
| **Option(name, help)** | a switch carrying a value, written attached: `-out:file` |
| **WhatIf()** | declares the conventional dry run: `-whatif`, also `--dry-run` or `-n` |
| **Description(text)** | the paragraph shown above the usage line |
| **Example(commandLine, help)** | a worked example for the bottom of the help |
| **Program(name)** | override the name in the usage line |
| **UsageWhenEmpty()** | print the usage when run with no arguments at all |
| **Parse()** | read the command line and return a CliResult |

| Read on CliResult | Description                                                                                     |
|------------------|--------------------------------------------------------------------------------------------------|
| **ShouldExit**   | true when the script should stop -- help was shown, or the command line was not valid |
| **ExitCode**     | what to return: 0 for help, 1 for a command line that was not valid |
| **Argument(name)** | what was given for a positional, or null when an optional one was omitted |
| **Switch(name)** | whether a switch was given |
| **Option(name)** | the value given for an option, or null |
| **WhatIf**       | whether the dry-run switch was given |
| **Rest**         | everything the declared Rest collected |
| **Arguments**    | every positional given, in order |
| **Error**        | what was wrong with the command line, or null |
| **UsageText**    | the generated help, whether or not it was shown |
| **ProgramName**  | the name shown in the usage line |

**Anything undeclared is an error.** Silently ignoring an unknown switch is how a mistyped
`--dry-run` does the real thing and a mistyped `--api-key` runs with the wrong one. Bare words are
positionals rather than unknown switches, which is what lets a script take a path without every
path being rejected as a switch it does not know.

**Values attach.** `-out:file` and `-out=file`, never `-out file`. That is a safety property
rather than a shortcut: the separated form is what lets a trailing `-out` silently become a
positional, and `-out --whatif` silently swallow the next switch as its value. An attached value
is one token, so neither is possible, and someone typing the separated form is told so. Only the
name is normalized -- the value is kept exactly as typed, so
`-source:https://api.nuget.org/v3/index.json` and `-out:C:\temp\My-Folder` arrive intact.

**Switches are spelled with dashes.** `-whatif`, `--whatif` and `--what-if` are one switch: the
leading dashes come off, inner hyphens and underscores go, and case is ignored. `/` is not a
prefix -- it would make every absolute path on Linux look like a switch.

**Help is generated from the declarations**, so it cannot drift from what the script accepts.
`-help`, `-h` and `-?` work without being asked for, and the program name comes from the calling
script's file name.

**Parse() never exits the process** and never throws for a bad command line -- a stack trace is the
wrong way to say "you typed --dryrun", and a library that exits cannot be tested. It writes the
message to standard error, help to standard output, and sets `ShouldExit`. Forgetting to check
that is caught rather than ignored: every value on the result throws once the command line was
bad, so a missed check fails loudly instead of running on with defaults it never earned.

The ceiling, stated so nobody has to discover it: no subcommands, no repeated options, no typed
binding, and no separated values. A script that needs more than this can reference
[System.CommandLine](https://www.nuget.org/packages/System.CommandLine) directly.

### Process Methods
CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell), which provides a great set of functionality for easily invoking 
processes and piping data between them.  CShell adds on location awareness and helper methods
to make it even easier to work with the output of processes.

| Method           | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **ReadFile()/cat()/type()**  | read a file and create a stream                                                        |
| **echo(text/lines/stream)** | echo text,lines from memory to a stream                                                 |
| **Run(program, arg1, ..., argN)**    | run a program directly with the given args (aka Process.Start(program, args) |
| **Start(program, arg1, ..., argN)**    | run a DETACHED program directly with the given args (aka Process.Start(program, args)|
| **Cmd(cmd)**  | run the cmd inside a cmd.exe, allow you to execute shell commands (like dir /b *.*                  |
| **Bash(bash)**  | run the program in bash environment, allow you to execute bash shell commands (like ls -al *      |

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
| **AsJson(log)**     | JSON Deserialize the standard out of the last command into a JObject/dynamic |
| **AsJson\<T>(log)** | JSON Deserialize the standard out of the last command into a typed T         |
| **AsXml\<T>(log)**  | XML Deserialize the standard out of the last command intoa typed T           |
| **AsFile()**     | Write the stdout/stderr  of the last command to a file                       |


To call a program you await on:
1. call ReadFile()/Run()/Cmd()/Bash()/echo()
2. call any chaining commands 
3. end with a result call like Execute()/AsJson()/AsString()/AsXml()etc.

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
### v3.0.0
* Added **Cli**, a declarative command line parser with generated --help
  * Argument()/OptionalArgument()/Rest() for positionals, Switch() for booleans, Option() for attached values
  * anything undeclared is an error; Parse() reports and sets ShouldExit rather than exiting the process
* Added the **Ask** methods: AskText, AskSecret, AskYesNo, AskNumber, AskChoice, AskMultiChoice
  * AskChoice/AskMultiChoice are generic and return the option itself rather than its position
  * each has a rich arrow-key mode and a typed-line mode, chosen from whether input is redirected
* Added **RichPrompts** and **ReadKey** to control how the Ask methods read input
* Nothing was removed or renamed -- 3.0.0 marks the size of the release, not a break

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
