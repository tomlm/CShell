#!/usr/bin/env dotnet-script
#r "nuget: CShell, 3.0.0"
global using static CShellNet.Globals;
using CShellNet;

// to install dotnet-script on your computer:
//      dotnet tool install -g dotnet-script
//
// WINDOWS: on windows you need to register .csx extensions as executable scripts: (only once)
//      dotnet script register
//
// MAC/LINUX you need to mark the script file as executable
//     chmod +x filename.csx
//
// To debug this I HIGHLY recommend LinqPad9 https://linqpad.net

var cmd = Cli.For(Args)
    .Description("..description.")
    .Option("test", "test option")
    //.Argument("file", "the file to work on")
    //.OptionalArgument("output", "where to write the result; defaults to the input name")
    //.Switch("force", "overwrite the output if it is already there")
    //.Option("format", "the output format; defaults to json")
    //.WhatIf()
    //.Example("CShellTemplate report.txt", "write report.json beside it")
    .Parse();

if (cmd.Switch("test"))
    print("test option is set");

return 0;
