#!/usr/bin/env dotnet-script
#r "nuget: CShell, 1.5.0"
global using static CShellNet.Globals;
using CShellNet;

// NOTE: to enable debugging *.csx files in visual studio code run 'dotnet script init' in this folder 
// NOTE 2: on linux you may need to run 'dos2unix *.csx' to convert line endings

Console.WriteLine("Hello world!");

foreach (var arg in Args)
{
    Console.WriteLine($"{arg}");
}

