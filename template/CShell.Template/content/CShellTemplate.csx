#!/usr/bin/env dotnet-script
#r "nuget: CShell, 2.1.0"
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

foreach (var arg in Args)
{
    WriteLine(arg);
    print(arg);
}

