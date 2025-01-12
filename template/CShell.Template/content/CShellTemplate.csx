#!/usr/bin/env dotnet-script
#r "nuget: CShell, 1.5.0"
global using static CShellNet.Globals;
using CShellNet;
// NOTE: run 'dotnet script init' in this folder to enable debugging .csx files in visual studio code.

Console.WriteLine("Hello world!");

foreach (var arg in Args)
{
    Console.WriteLine($"{arg}");
}

