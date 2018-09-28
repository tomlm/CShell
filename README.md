<img src="https://game-icons.net/icons/lorc/originals/png/000000/transparent/turtle.png" width="100"/>

# CShell
CShell creates a runtime environment to make it easy to create C# based shell style scripts.

CShell is built using [MedallionShell](https://github.com/madelson/MedallionShell) and runs great using [dotnet-script](https://github.com/filipw/dotnet-script) (.csx) giving 
you a great cross platform C# alternative to powershell and bash scripts.

CShell provides:
* The concept of a current folder with relative commands for navigating and manipulating files and folders
* The ability to smoothly invoke processes and pipe 
* Helpers to make it easy to work with the output of processes

By maintaining the concept of a current folder  all file and folder commands can be take absolute or 
 relative paths just like a normal shell.

You can just create a CShell directly and start working with it

```csharp
#r "nuget: CShell, 1.1.0"
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

    var result = await Run("findstr", "/N", "yo")
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
#r "nuget: Newtonsoft.Json, 11.0.2"
#r "nuget: MedallionShell, 1.5.1"
#r "nuget: CShell, 1.1.0"

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


