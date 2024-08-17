#r "nuget: CShell, 1.4.0"
global using static CShellNet.Globals;
using CShellNet;

Console.WriteLine("Hello world!");
foreach (var arg in Args)
{
    Console.WriteLine($"{arg}");
}

// Example: folder manipulation
// md("test");
// cd("test");


/* Getting results from command
| Method             | Description                                                                  |
|--------------------|------------------------------------------------------------------------------|
| **Execute(log)**   | get the CommandResult (with stdout/stderr) of the last command               |
| **AsString(log)**  | get the standard out of the last command a string                            |
| **AsJson(log)**    | JSON Deserialize the standard out of the last command into a JObject/dynamic |
| **AsJson<T>(log)** | JSON Deserialize the standard out of the last command into a typed T         |
| **AsXml<T>(log)**  | XML Deserialize the standard out of the last command intoa typed T           |
| **AsFile()**       | Write the stdout/stderr  of the last command to a file                       |
*/

// Example: Invoke multiple commands using fluent style
// Command cmd1= await Run("cmd1", "args1")
//   .PipeTo("cmd2", "args2", "args3")
//   .PipeTo("cmd3", "args4");
// CommandResult result1 = await cmd1.AsResult();

// Example: chaining processes together with the pipe operator
// Command cmd2 = await Run("cmd1", "args1") 
//   | Run("cmd2", "args2", "args3") 
//   | Run("cmd3", "args4");
// CommandResult  result2 = await cmd2.AsResult();

// Example: we can even chain processes together with the > operator
// Command cmd3 = await Run("cmd1", "args1") 
//   > Run("cmd2", "args2", "args3")
//   > Run("cmd3", "args4");
// CommandResult  result3 = await cmd3.AsResult();

// Example: Getting output as Json => MyClass
// MyClass obj = await ReadFile("myfile.json").AsJson<MyClass>();

// Example: Getting output as XML => MyClass
// MyClass obj = await ReadFile("myfile.json").AsXml<MyClass>();
