global using static CShellNet.Globals;
using CShellNet;

foreach(var arg in args)
{
    Console.WriteLine(arg);
}

Console.WriteLine(CurrentFolder);
pushd("../../../../tests/CShell.Tests/Test");
Console.WriteLine(CurrentFolder);

var job1 = ReadFile("TestA.txt") | Cmd("findstr /i joe");

// getting CommandResult will not throw, gives access to Success, StandardOut, StandardErr etc.
var result = await job1.AsResult();
if (result.Success)
{
    var text = result.AsString();
    Console.WriteLine(text);
}

// getting result directly, will throw on !Success
var job2 = ReadFile("testB.txt") | Cmd("findstr /i joe");
var text2 = await job2.AsString();
Console.WriteLine(text2);

popd();

Console.WriteLine(CurrentFolder);
