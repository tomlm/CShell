#!/usr/bin/env dotnet-script
#r "nuget: CShell, 3.0.1"
global using static CShellNet.Globals;
using CShellNet;

// to install dotnet-script on your computer:
//      dotnet tool install -g dotnet-script
// WINDOWS: on windows you need to register .csx extensions as executable scripts: (only once)
//      dotnet script register
// MAC/LINUX you need to mark the script file as executable
//     chmod +x filename.csx
// To debug this I HIGHLY recommend LinqPad9 https://linqpad.net
Cli.For(Args)
    .OptionalSwitch(out bool reverse, "reverse the output")
    //.Description("..hello world...")
    //.Argument(out string file, "the file to work on")
    //.OptionalArgument(out string output, "where to write the result; defaults to the input name")
    //.Switch(out bool test, "the test switch")
    //.Option(out int queueLength, "how many to queue at once")
    //.Example("CShellTemplate report.txt", "write report.json beside it")
    .Parse();

if (reverse)
    print("!dlrow olleH");
else
    print("Hello world!");

return 0;
