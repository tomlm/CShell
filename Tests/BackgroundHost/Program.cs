// A helper for the Background() tests, run as its own process:
//
//   report <file> [args...]   writes what it was started with to <file> and exits:
//                             pid, working folder, CSHELL_BACKGROUND_TEST, Linux session id, args
//   launch                    starts `sleep 60` of itself with Background(), prints the id it
//                             got back, and exits at once
//   sleep <seconds>           sleeps

using System;
using System.IO;
using System.Linq;
using System.Threading;
using CShellNet;

switch (args[0])
{
    case "report":
        var lines = new[]
        {
            Environment.ProcessId.ToString(),
            Environment.CurrentDirectory,
            Environment.GetEnvironmentVariable("CSHELL_BACKGROUND_TEST") ?? "<unset>",
            LinuxSession("self"),
        }.Concat(args.Skip(2));
        // Written whole and then moved, so the test never reads half a file.
        File.WriteAllLines(args[1] + ".tmp", lines);
        File.Move(args[1] + ".tmp", args[1]);
        return 0;

    case "launch":
        var self = typeof(Program).Assembly.Location;
        var shell = new CShell { Echo = false };
        Console.WriteLine(shell.Background(Environment.ProcessPath, self, "sleep", "60"));
        return 0;

    case "sleep":
        Thread.Sleep(TimeSpan.FromSeconds(int.Parse(args[1])));
        return 0;

    default:
        return 2;
}

// The session id from /proc/<pid>/stat: the fourth field after the parenthesised name.
static string LinuxSession(string pid)
{
    var stat = $"/proc/{pid}/stat";
    if (!File.Exists(stat))
    {
        return "";
    }

    var text = File.ReadAllText(stat);
    return text.Substring(text.LastIndexOf(')') + 2).Split(' ')[3];
}
