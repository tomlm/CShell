<img src="https://game-icons.net/icons/lorc/originals/png/000000/transparent/turtle.png" width="100"/>

# CShell
CShell creates a runtime environment to make it easy to create C# based shell style scripts.

CShell is built on using [MedallionShell](https://github.com/madelson/MedallionShell) and runs great using [dotnet-script](https://github.com/filipw/dotnet-script) (.csx) on .NET Core giving 
you a great cross platform C# alternative to powershell and bash scripts.

CShell provides:
* The concept of a current folder with relative commands for navigating and manipulating files and folders
* The ability to smoothly invoke processes and pipe 
* Helpers to make it easy to work with the output of processes

By maintaining the concept of a current folder then all file and folder commands can be expressed
using relative paths just like a normal shell.

You can just create a CShell directly and start working with it

```csharp
#r "nuget: CShell, 1.0.11"
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

    var result = await shell.Run("findstr", "yo")
        .RedirectFrom("test\ntest2\nyo\ntest3")
        .AsString();
    Console.WriteLine(result);

    shell.CreateFolder("test");
    shell.ChangeFolder("test");
    
    for (int i = 0; i < 100; i++)
    {
        await File.WriteAllTextAsync(i.ToString(), i.ToString());
        Console.WriteLine($"Creating: {i}");
    }

    foreach (var file in shell.CurrentFolder.GetFiles())
    {
        file.Delete();
        Console.WriteLine($"Deleting: {file.FullName}");
    }

    shell.CreateFolder("..");
    shell.DeleteFolder("test");

    Console.WriteLine("All done");
}
```

If you don't want to have the *shell.* instance prefix, you can derive from CShell and have access to the methods
without a instance pointer, cleaning up your code:

```CSharp
#r "nuget: Newtonsoft.Json, 11.0.2"
#r "nuget: MedallionShell, 1.5.1"
#r "nuget: CShell, 1.0.11"

using CShellNet;
using System.Threading.Tasks;
using Medallion.Shell;

new MyScript ().Main(Args).Wait();

class MyScript : CShell
{
    public async Task Main(IList<string> args)
    {
        foreach (var arg in args)
        {
            Console.WriteLine($"{arg}");
        }

        var result = await Run("findstr", "yo")
             .RedirectFrom("test\ntest2\nyo\ntest3")
              .AsString();
        Console.WriteLine(result);

        CreateFolder("test");
        ChangeFolder("test");

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

        ChangeFolder("..");
        DeleteFolder("test");
        Console.WriteLine("All done");
    }
}
```

### Properties
CShell exposes 3 properties which are the working environment of your script.  The CurrentFolder is used to resolve relative paths for
most methods, so if you call **MoveFile(@"..\foo.txt", @"..\..\bar")** it will resolve the paths and execute just like a normal shell.

| Property          | Description                                  |
|-------------------|----------------------------------------------|
| **CurrentFolder** | The current folder as a DirectoryInfo object |
| **FolderHistory** | List of folder paths you have navigated to   |
| **FolderStack**   | current stack from Push/Pop operations       |

### Folder Methods
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate folders.

| Method             | Description                                                                  |
|--------------------|------------------------------------------------------------------------------|
| **ChangeFolder()** | Change the current folder with relative or absolute path                     |
| **CreateFolder()** | Create a folder relative to current folder                                   |
| **CopyFolder()**   | copy a folder relative to current folder                                     |
| **MoveFolder()**   | move a folder relative to current folder                                     |
| **DeleteFolder()** | Delete a folder relative to current folder                                   |
| **PushFolder()**   | Push the current folder onto the stack and change folder to the new one      |
| **PopFolder()**    | Pop the current folder off the stack and change the folder the popped folder |
| **FolderExists()** | does the relative folder to current folder exist                             |

### File Methods
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate files.

| Method           | Description                              |
|------------------|------------------------------------------|
| **CopyFile()**   | Copy a file relative to current folder   |
| **MoveFile()**   | Move a file relative to current folder   |
| **DeleteFile()** | Delete a file relative to current folder |
| **FileExists()** | does a file relative to current folder exist                             |


### Process Methods
CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell), which provides a great set of functionality for easily invoking 
processes and piping data between them.  CShell adds on location awareness and helper methods
to make it even easier to work with the output of processes.

To invoke a process you simply call .Run(). You can chain processes together using .PipeTo(), pipe '**|**' or greater than '**>**'.

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


#### Working with typed results
Medallion makes it wasy to work with processes, but CShell adds on helper methods to make it even
easier to work with the result of a command chain.

| Method           | Description                                                                  |
|------------------|------------------------------------------------------------------------------|
| **AsResult()**   | get the CommandResult (with stdout/stderr) of the last command               |
| **AsString()**   | get the standard out of the last command a string                            |
| **AsJson()**     | JSON Deserialize the standard out of the last command into a JObject/dynamic |
| **AsJson\<T>()** | JSON Deserialize the standard out of the last command into a typed T         |
| **AsXml\<T>()**  | XML Deserialize the standard out of the last command intoa typed T           |
| **AsFile()**     | Write the stdout/stderr  of the last command to a file                       |

This allows you to change the previous example directly into a typed object in one command:

```CSharp
class MyScript: CShell
{
  async Task Main(IList<string> args)
  {
    // get the result as dynamic object
    dynamic jsonResult  = await Run("cmd1", "args1")
      .PipeTo("cmd2", "args2", "args3")
      .PipeTo("cmd3", "args4")
      .AsJson();

    // get the json result as a typed object
    MyObject myObject  = await Run("cmd1", "args1")
      .PipeTo("cmd2", "args2", "args3")
      .PipeTo("cmd3", "args4")
      .AsJson<MyObject>();
  }
}
```

#### Working with files
CShell has ReadFile() and AsFile() extension methods which allow you to "start" the
process chain from a file, or to end with it in a file.

| Method          | Description                                                                 |
|-----------------|-----------------------------------------------------------------------------|
| **.ReadFile()** | Read the file and use it as a Command that can be piped into other commands |
| **.AsFile()**   | Write the stdout/stderr of the last command to a file                      |

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


### Custom - CmdShell 
To make working with CShell familiar to windows CMD programmers there is a class called CmdShell which
adds methods that look like CMD style commands.

| Method       | Description                                                                                        |
|--------------|----------------------------------------------------------------------------------------------------|
| **cd()**     | Change the current folder with relative or absolute path                                           |
| **chdir()**  | Change the current folder with relative or absolute path                                           |
| **md()**     | Create a folder relative to current folder                                                         |
| **mkdir()**  | Create a folder relative to current folder                                                         |
| **rd()**     | Delete a folder relative to current folder                                                         |
| **rmdir()**  | Delete a folder relative to current folder                                                         |
| **erase()**  | Delete a file relative to current folder                                                           |
| **del()**    | Delete a file relative to current folder                                                           |
| **delete()** | Delete a file relative to current folder                                                           |
| **pushd()**  | Push the current folder onto the stack and change folder to the new one                            |
| **popd()**   | Pop the current folder off the stack and change the folder the popped folder                       |
| **type()**   | TYPE equivelant create a file command which can be piped to other commands  *(maps to ReadFile())* |

```CSharp
using CShellNet.CmdStyle;

class MyScript: CShell()
{
    async Task Go()
    {
        // cmd style
        md("test");
        cd("test");

        foreach(var folder in CurrentFolder.EnumerateDirectories()) 
        {
            Console.WriteLine(folder.FullName);
        }

        cd("..");
        rd("test");
    }
}
```

#### Custom - BashShell
To make working with CShell familiar to OSX/Linux programmers there is a class **BashShell** which
adds methods which look like Bash style commands.

| Method       | Description                                                                                        |
|--------------|----------------------------------------------------------------------------------------------------|
| **cwd()**    | get current working directory path                                                                 |
| **cd()**     | Change the current folder with relative or absolute path                                           |
| **chdir()**  | Change the current folder with relative or absolute path                                           |
| **mkdir()**  | Create a folder relative to current folder                                                         |
| **rmdir()**  | Delete a folder relative to current folder                                                         |
| **erase()**  | Delete a file relative to current folder                                                           |
| **del()**    | Delete a file relative to current folder                                                           |
| **delete()** | Delete a file relative to current folder                                                           |
| **cat()**    | cat cmd equivelant create a file command which can be piped to other commands *(maps to ReadFile)* |

```CSharp
using CShellNet.CmdStyle;

class MyScript: CShell()
{
    async Task Go()
    {
        // bash style
        mkdir("test");
        cd("test");
        
        foreach(var folder in CurrentFolder.EnumerateDirectories()) 
        {
            Console.WriteLine(folder.FullName);
        }
        
        chdir("..");
        rmdir("test");
    }
}
```
