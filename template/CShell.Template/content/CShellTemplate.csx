#!/usr/bin/env dotnet-script
#r "nuget: CShell, 1.5.2"
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
// NOTE: To make this debuggable in visual studio code, simply run
//      dotnet script init
// command in this folder.  This will make it so you can set breakpoints in any .csx file in this folder and step through the script

Console.WriteLine("Hello world!");

foreach (var arg in Args)
{
    Console.WriteLine($"{arg}");
}

