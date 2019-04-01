#r "nuget: Newtonsoft.Json, 11.0.2"
#r "nuget: MedallionShell, 1.5.1"
#r "nuget: CShell, 1.1.0"

using CShellNet;
using System.Threading.Tasks;
using Medallion.Shell;

new Script ().Main(Args).Wait();

// goto https://github.com/tomlm/CShell/blob/master/README.md for documentation

// NOTE: To make this debugabble in visual studio code, simply run 
//      dotnet script init 
// command in this folder.  This will create launch.json configuration so you can set breakpoints and step through the script

class Script : CShell
{
    public async Task Main(IList<string> args)
    {
        Console.WriteLine("Hello world!");
        foreach (var arg in args)
        {
            Console.WriteLine($"{arg}");
        }

        // Example: folder manipulation
        // md("test");
        // cd("test");

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

        // Example: reading file, piping it through chain to output
        // var result  = await ReadFile("myfile.txt")
        //   .PipeTo("cmd2", "args2", "args3")
        //   .PipeTo("cmd3", "args4")
        //   .AsFile("stdout.txt", "stderr.txt");

    }
}
