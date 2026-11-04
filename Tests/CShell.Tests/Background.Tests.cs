using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CShellLibTests
{
    /// <summary>
    /// Background() returns at once with nothing to read, so these run the BackgroundHost helper,
    /// which writes down what it was started with, and check the file it leaves behind.
    /// </summary>
    [TestClass]
    public class BackgroundTests
    {
        private static string DotNet => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

        private static string Host => Path.Combine(AppContext.BaseDirectory, "BackgroundHost.dll");

        private string tempFolder;
        private string originalFolder;

        [TestInitialize]
        public void Init()
        {
            this.originalFolder = Environment.CurrentDirectory;
            this.tempFolder = Path.Combine(Path.GetTempPath(), "cshell-background-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // new CShell(folder) moves this whole process into the folder. Unix lets it be deleted
            // from under us, after which every later test fails to read its current directory.
            Environment.CurrentDirectory = this.originalFolder;
            try
            {
                Directory.Delete(this.tempFolder, true);
            }
            catch
            {
            }
        }

        // Report lines: pid, working folder, CSHELL_BACKGROUND_TEST, Linux session, then the args.
        private static string[] ReadReport(string file)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (!File.Exists(file))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "the background process never wrote its report");
                Thread.Sleep(50);
            }

            return File.ReadAllLines(file);
        }

        private static void TryKill(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.Kill();
            }
            catch
            {
                // Already gone.
            }
        }

        [TestMethod]
        public void Background_ReturnsTheProgramsOwnId_InTheShellsFolder_WithItsEnvironment()
        {
            var shell = new CShell(this.tempFolder) { Echo = false };

            var pid = shell.Background(
                opt => opt.EnvironmentVariable("CSHELL_BACKGROUND_TEST", "hello"),
                DotNet, Host, "report", "report.txt");

            // A relative path, so finding the file at all shows which folder it ran in.
            var report = ReadReport(Path.Combine(this.tempFolder, "report.txt"));
            Assert.AreEqual(pid.ToString(), report[0], "the id returned should be the program's own");
            StringAssert.EndsWith(report[1], Path.GetFileName(this.tempFolder));
            Assert.AreEqual("hello", report[2]);
        }

        [TestMethod]
        public void Background_WorkingDirectoryOptionWins()
        {
            var shell = new CShell() { Echo = false };

            shell.Background(opt => opt.WorkingDirectory(this.tempFolder), DotNet, Host, "report", "where.txt");

            ReadReport(Path.Combine(this.tempFolder, "where.txt"));
        }

        [TestMethod]
        public void Background_ArgumentsArriveExactlyAsGiven()
        {
            var shell = new CShell(this.tempFolder) { Echo = false };
            var arguments = new[] { "plain", "two words", "", "a \"quoted\" word", "trailing\\", "back\\\\\"slashes", "C:\\Program Files\\x", "tab\there", "ünïcödé" };

            shell.Background(DotNet, new object[] { Host, "report", "args.txt" }.Concat(arguments).ToArray());

            var report = ReadReport(Path.Combine(this.tempFolder, "args.txt"));
            CollectionAssert.AreEqual(arguments, report.Skip(4).ToArray());
        }

        [TestMethod]
        public async Task Background_HoldsNoneOfTheCallersStreams()
        {
            // The helper starts a 60 second sleep in the background and exits. Reading its output
            // to the end only finishes promptly if the sleep holds no copy of that output.
            var startInfo = new ProcessStartInfo(DotNet)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(Host);
            startInfo.ArgumentList.Add("launch");

            using var launcher = Process.Start(startInfo);
            var stderr = launcher.StandardError.ReadToEndAsync();
            var stdout = launcher.StandardOutput.ReadToEndAsync();

            var finished = await Task.WhenAny(Task.WhenAll(stdout, stderr), Task.Delay(TimeSpan.FromSeconds(20)));
            var pidText = stdout.IsCompleted ? stdout.Result.Trim() : null;
            try
            {
                Assert.IsTrue(stdout.IsCompleted && stderr.IsCompleted,
                    "reading the caller's output should end when the caller exits, not when the background process does");
                Assert.AreEqual("", await stderr);

                using var sleeper = Process.GetProcessById(int.Parse(pidText));
                Assert.IsFalse(sleeper.HasExited, "the background process should still be running");
            }
            finally
            {
                if (int.TryParse(pidText, out var pid))
                {
                    TryKill(pid);
                }
            }
        }

        [TestMethod]
        public void Background_RunsInItsOwnSessionOnLinux()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("sessions are checked on Linux only");
            }

            var shell = new CShell(this.tempFolder) { Echo = false };
            shell.Background(DotNet, Host, "report", "session.txt");

            var report = ReadReport(Path.Combine(this.tempFolder, "session.txt"));
            var stat = File.ReadAllText("/proc/self/stat");
            var ourSession = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ')[3];
            Assert.AreNotEqual(ourSession, report[3], "the background process should have a session of its own");
            Assert.AreEqual(report[0], report[3], "it should lead its own session, as setsid makes it");
        }

        [TestMethod]
        public void Background_UnknownProgramThrows()
        {
            var shell = new CShell() { Echo = false };

            Assert.ThrowsExactly<Win32Exception>(() => shell.Background("this-program-does-not-exist-cshell-test"));
        }

        [TestMethod]
        public void BackgroundGlobal_ReturnsTheProgramsOwnId()
        {
            CShellNet.Globals.ResetShell();
            CShellNet.Globals.Echo = false;
            var file = Path.Combine(this.tempFolder, "global.txt");

            var pid = CShellNet.Globals.Background(DotNet, Host, "report", file);

            Assert.AreEqual(pid.ToString(), ReadReport(file)[0]);
        }
    }
}
