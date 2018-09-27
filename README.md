<img src="https://game-icons.net/icons/lorc/originals/png/000000/transparent/turtle.png" width="100"/>

# CShell
CShell creates a runtime environment to make it easy to create C# based shell style scripts.
CShell is built on top of MedallionShell and runs great as a CSX on .NET Core giving 
you a great cross platform C# alternative to powershell and bash scripts.

There are 2 functionalities which CShell provides:
* The concept of a current folder with relative commands for navigating and manipulating files and folders
* The ability to smoothly invoke processes and pipe and manipulate the output of processes

You can just create a CShell directly and start working with it
```csharp
var shell = new CShell();

shell.CreateFolder("test");
shell.ChangeFolder("test");
foreach(var folder in shell.CurrentFolder.EnumerateDirectories()) 
{
	Console.WriteLine(folder.FullName);
}
```

Or you can derive a class and not need the shell. prefix at all
```CSharp
class MyScript: CShell()
{
	async Task Go()
	{
		CreateFolder("test");
		ChangeFolder("test");
		foreach(var folder in shell.CurrentFolder.EnumerateDirectories()) 
		{
			Console.WriteLine(folder.FullName);
		}
	}
}
```

### CShell Properties
CShell exposes 3 properties which are essentially the working environment of your script.

| Property          | Description                                  |
|-------------------|----------------------------------------------|
| **CurrentFolder** | The current folder as a DirectoryInfo object |
| **FolderHistory** | List of folder paths you have navigated to   |
| **FolderStack**   | current stack from Push/Pop operations       |

### CShell Folder Commands
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate folders.

| Method             | Description                                                                  |
|--------------------|------------------------------------------------------------------------------|
| **ChangeFolder()** | Change the current folder with relative or absolute path                     |
| **CreateFolder()** | Create a folder relative to current folder                                   |
| **DeleteFolder()** | Delete a folder relative to current folder                                   |
| **PushFolder()**   | Push the current folder onto the stack and change folder to the new one      |
| **PopFolder()**    | Pop the current folder off the stack and change the folder the popped folder |

### CShell File Commands
CShell defines a number of methods which work relative to the current folder to make it easy
to manipulate files.

| Method             | Description                                                                  |
|--------------------|------------------------------------------------------------------------------|
| **DeleteFile()**   | Delete a file relative to current folder                                     |

#### Windows CMD Style Extensions
To make working with CShell familiar to windows programmers there is a namespace CShellNet.CmdStyle which
when you add it adds extension methods that look like CMD style methods.

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

#### Bash Style Extensions
To make working with CShell familiar to OSX/Linux programmers there is a namespace CShellNet.BashStyle which
when you add it adds extension methods that look like CMD style methods.

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

### CShell Process Commands
CShell is built on using [MedallionShell](https://github.com/madelson/MedallionShell), which provides a great set of functionality for easily invoking 
processes and piping data between them.  CShell adds on location awareness and helper methods
to make it even easier to work with processes.

You can use .RedirectTo(), pipe '**|**' or greater than '**>**' to chain processes together.

```CSharp
class MyScript: CShell()
{
	async Task Go()
	{
		// Invoke multiple commands using fluent style
		CommandResult result3 = await Run("cmd1", "args1")
			.RedirectTo("cmd2", "args2", "args3")
			.RedirectTo("cmd3", "args4")
			.AsResult();

		// we can even chain commands together with the pipe operator
		CommandResult result = await Run("cmd1", "args1") 
			| Run("cmd2", "args2", "args3") 
			| Run("cmd3", "args4")
			.AsResult();

		// we can even chain commands together with the > operator
		CommandResult result2 = await Run("cmd1", "args1") 
 			> Run("cmd2", "args2", "args3")
			> Run("cmd3", "args4")
			.AsResult();
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
| **AsJson()**     | JSON Deserialize the standard out of the last command into a JObject/dynamic |
| **AsJson\<T>()** | JSON Deserialize the standard out of the last command into a typed T         |
| **AsXml\<T>()**  | XML Deserialize the standard out of the last command intoa typed T           |
| **AsFile()**     | Write the stdout/stderr  of the last command to a file                       |

This allows you to change the previous example directly into a typed object in one command:

```CSharp
class MyScript: CShell()
{
	async Task Go()
	{
		// get the result as dynamic object
		dynamic jsonResult  = await Run("cmd1", "args1")
			.RedirectTo("cmd2", "args2", "args3")
			.RedirectTo("cmd3", "args4")
			.ToJson();

		// get the json result as a typed object
		MyObject myObject  = await Run("cmd1", "args1")
			.RedirectTo("cmd2", "args2", "args3")
			.RedirectTo("cmd3", "args4")
			.ToJson<MyObject>();
	}
}
```

#### Working with files
CShell has ReadFile() and AsFile() extension methods which allow you to "start" the
process chain from a file, or to end with it in a file.

| Method          | Description                                                                 |
|-----------------|-----------------------------------------------------------------------------|
| **.ReadFile()** | Read the file and use it as a Command that can be piped into other commands |
| **.AsFile()**   | Write the stdout/stderr  of the last command to a file                      |

```CSharp
class MyScript: CShell()
{
	async Task Go()
	{
		// start with text file and pass into cmd2, then write the final stdout and stderr and return a CommandResult
		var result  = await ReadFile("myfile.txt")
			.RedirectTo("cmd2", "args2", "args3")
			.RedirectTo("cmd3", "args4")
			.AsFile("stdout.txt", "stderr.txt");
	}
}
```


