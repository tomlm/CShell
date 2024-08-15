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

### File Methods
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate files.

| Method       | Description                                  |
|--------------|----------------------------------------------|
| **copy()**   | Copy a file relative to current folder       |
| **move()**   | Move a file relative to current folder       |
| **rename()** | Move a file relative to current folder       |
| **delete()** | Delete a file relative to current folder     |
| **erase()**  | does a file relative to current folder exist |
| **type()**   | type a file to standardout                   |
| **cat()**    | cat a file to standardout                    |


### Process Methods
CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell), which provides a great set of functionality for easily invoking 
processes and piping data between them.  CShell adds on location awareness and helper methods
to make it even easier to work with the output of processes.

| Method           | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| **Run(program, arg1, ..., argN)**    | run a program directly with the given args (aka Process.Start(program, args) |
| **Start(program, arg1, ..., argN)**    | run a DETACHED program directly with the given args (aka Process.Start(program, args)|
| **Cmd(cmd)**  | run the cmd inside a cmd.exe, allow you to execute shell commands (like dir /b *.*                  |
| **Bash(bash)**  | run the program in bash environment, allow you to execute bash shell commands (like ls -al *      |

```CSharp
class MyScript: CShell
{
  async Task Main(IList<string> args)
  {
    // Invoke multiple commands using fluent style
    Command cmd1= await Run("cmd1", "args1")
      .PipeTo("cmd2", "args2", "args3")
      .PipeTo("cmd3", "args4");
    CommandResult result1 = await cmd1.AsResult();

    // we can even chain commands together with the pipe operator
    Command cmd2 = await Run("cmd1", "args1") 
      | Run("cmd2", "args2", "args3") 
      | Run("cmd3", "args4");
    CommandResult  result2 = await cmd2.AsResult();

    // we can even chain commands together with the > operator
    Command cmd3 = await Run("cmd1", "args1") 
      > Run("cmd2", "args2", "args3")
      > Run("cmd3", "args4");
    CommandResult  result3 = await cmd3.AsResult();
    
  }
}
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
1. call Run()/Cmd()/Bash()
2. call any chaining commands
3. end with a result call like Execute()/AsJson()/AsString()/AsXml()etc.

The result methods all take a log argument is passed set to true then the commands output will be written to standard out.

```CSharp
class MyScript: CShell
{
  async Task Main(IList<string> args)
  {
    // run a command and interpret the json as an AccountInfo object
    var account = await Cmd("az account show").AsJson<AccountInfo>();
    
    // run a command and interpret the XML as an AccountInfo object
    var account2 = await Cmd("az account show -o xml").AsXml<AccountInfo>();
    
    // run a command interpret the result as a string.
    var accountString = await Cmd("az account show").AsString();
    
    // run a command and get back the CommandResult which has Succes, StatusCode, StandardInput and StandardError.
    CommandResult result = await Run("x", "foo") | Cmd("az account show").Execute();
    if(result.Sucess)
    {
        var output = result.StandardOutput;
        ...
    }
  }
}
```

#### Working with files
CShell has ReadFile() and AsFile() extension methods which allow you to "start" the
process chain from a file, or to end with it in a file.

| Method          | Description                                                                 |
|-----------------|-----------------------------------------------------------------------------|
| **.ReadFile()** | Read the file and use it as a Command that can be piped into other commands |
| **.AsFile()**   | Write the stdout/stderr of the last command to a file                       |

```CSharp
class MyScript: CShell
{
    async Task Main(IList<string> args)	{
    // start with text file and pass into cmd2, then write the final stdout and stderr and return a CommandResult
    var result  = await ReadFile("myfile.txt")
        .PipeTo("cmd2", "args2", "args3")
        .PipeTo("cmd3", "args4")
        .AsFile("stdout.txt", "stderr.txt");
    }
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

You can just create a CShell directly and start working with it

```csharp
#r "nuget: CShell, 1.4.0"
#r "nuget: MedallionShell, 1.5.1"
#r "nuget: Newtonsoft.Json, 11.0.2"

using CShellNet;
using Medallion.Shell;

Main(Args).Wait();

public async Task Main(IList<string> args)
{
    var shell = new CShell();
    
    foreach(var arg in args)
    {
        Console.WriteLine($"{arg}");
    }
    
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("Test");
    sb.AppendLine("yo");
    sb.AppendLine("test3");

    var result = await Cmd("findstr /N yo")
        .RedirectFrom(sb.ToString())
        .AsString();
    Console.WriteLine(result);

    shell.md("test");
    shell.cd("test");
    
    for (int i = 0; i < 100; i++)
    {
        await File.WriteAllTextAsync(i.ToString(), i.ToString());
        Console.WriteLine($"Creating: {i}");
    }

    foreach (var file in shell.CurrentFolder.GetFiles())
    {
        file.Delete();
        Console.WriteLine($"Deleting: {file.FullName}");

    shell.cd("..");
    shell.rd("test");

    Console.WriteLine("All done");
}
```

If you don't want to have the *shell.* instance prefix, you can derive from CShell and have use the methods
without a instance pointer, cleaning up your code:

```CSharp
#r "nuget: Newtonsoft.Json, 12.0.3"
#r "nuget: MedallionShell, 1.6.1"
#r "nuget: CShell, 1.4.0"

using CShellNet;
using System.Threading.Tasks;
using Medallion.Shell;

await new MyScript ().Main(Args);

class MyScript : CShell
{
    public async Task Main(IList<string> args)
    {
        foreach (var arg in args)
        {
            Console.WriteLine($"{arg}");
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Test");
        sb.AppendLine("yo");
        sb.AppendLine("test3");

        var result = await Run("findstr", "/N", "yo")
            .RedirectFrom(sb.ToString())
            .AsString();
        Console.WriteLine(result);

        md("test");
        cd("test");

        for (int i = 0; i < 100; i++)
        {
            await File.WriteAllTextAsync(i.ToString(), i.ToString());
            Console.WriteLine($"Creating: {i}");
        }

        foreach (var file in this.CurrentFolder.GetFiles())
        {
            file.Delete();
            Console.WriteLine($"Deleting: {file.FullName}");
        }

        cd("..");
        rd("test");
        Console.WriteLine("All done");
    }
}
```


### Registering .csx files to be executable on windows
You can register dotnet-script as the default handler for .csx files by running these commands:
```cmd
dotnet script register
```

After registering you can simple type **your.csx** to execute your cshell program.


## CHANGELOG
### V1.4.0
* Added Start() method for detached processess (you can monitor process but not access input/output)
* Added Run(Action<Option>, process, arg1, arg2) signature to control options for starting processes

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
