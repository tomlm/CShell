using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CShellLibTests
{
    /// <summary>
    /// Exec() attaches a process to this console instead of capturing it, so there is no output to
    /// assert on. These check the things that are left: the exit code, where it ran, what it
    /// inherited, and that cancelling it actually stops it. Anything the process should "say" is
    /// written to a file by the process itself.
    /// </summary>
    [TestClass]
    public class ExecTests
    {
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static string ShellExe => IsWindows ? "cmd" : "bash";

        private static string ShellFlag => IsWindows ? "/c" : "-c";

        private string tempFolder;
        private string originalFolder;

        [TestInitialize]
        public void Init()
        {
            this.originalFolder = Environment.CurrentDirectory;
            this.tempFolder = Path.Combine(Path.GetTempPath(), "cshell-exec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // A CShell's CurrentFolder is the PROCESS's current directory, so a test that started
            // one in the temp folder left the whole test run standing in it. Step out before
            // deleting: on Linux the delete otherwise succeeds and every later `new CShell()`
            // throws from getcwd() on a directory that is no longer there, and on Windows the
            // delete fails instead and the folders pile up.
            Environment.CurrentDirectory = this.originalFolder;

            try
            {
                Directory.Delete(this.tempFolder, true);
            }
            catch
            {
            }
        }

        [TestMethod]
        public void Exec_ReturnsTheExitCode()
        {
            var shell = new CShell() { Echo = false };

            Assert.AreEqual(0, shell.Exec(ShellExe, ShellFlag, "exit 0"));
            Assert.AreEqual(42, shell.Exec(ShellExe, ShellFlag, "exit 42"));
        }

        [TestMethod]
        public async Task ExecAsync_ReturnsTheExitCode()
        {
            var shell = new CShell() { Echo = false };

            Assert.AreEqual(7, await shell.ExecAsync(ShellExe, ShellFlag, "exit 7"));
        }

        [TestMethod]
        public async Task Exec_RunsInTheShellsCurrentFolder()
        {
            var shell = new CShell(this.tempFolder) { Echo = false };
            var marker = Path.Combine(this.tempFolder, "cwd.txt");

            // The process writes the file itself, since nothing is captured.
            await shell.ExecAsync(ShellExe, ShellFlag, IsWindows ? "cd > cwd.txt" : "pwd > cwd.txt");

            Assert.IsTrue(File.Exists(marker), "the process should have run in the shell's folder");
            StringAssert.Contains(File.ReadAllText(marker).Trim(), Path.GetFileName(this.tempFolder));
        }

        [TestMethod]
        public async Task Exec_WorkingDirectoryOptionWins()
        {
            var shell = new CShell() { Echo = false };

            await shell.ExecAsync(
                opt => opt.WorkingDirectory(this.tempFolder),
                ShellExe,
                ShellFlag,
                IsWindows ? "cd > where.txt" : "pwd > where.txt");

            Assert.IsTrue(File.Exists(Path.Combine(this.tempFolder, "where.txt")));
        }

        [TestMethod]
        public async Task Exec_PassesEnvironmentVariables()
        {
            var shell = new CShell(this.tempFolder) { Echo = false };

            await shell.ExecAsync(
                opt => opt.EnvironmentVariable("CSHELL_EXEC_TEST", "hello"),
                ShellExe,
                ShellFlag,
                IsWindows ? "echo %CSHELL_EXEC_TEST% > env.txt" : "echo $CSHELL_EXEC_TEST > env.txt");

            var written = File.ReadAllText(Path.Combine(this.tempFolder, "env.txt")).Trim();

            Assert.AreEqual("hello", written);
        }

        [TestMethod]
        public async Task Exec_ArgumentsWithSpacesStayOneArgument()
        {
            var shell = new CShell(this.tempFolder) { Echo = false };

            await shell.ExecAsync(
                ShellExe,
                ShellFlag,
                IsWindows ? "echo a b c > spaces.txt" : "echo 'a b c' > spaces.txt");

            Assert.AreEqual("a b c", File.ReadAllText(Path.Combine(this.tempFolder, "spaces.txt")).Trim());
        }

        [TestMethod]
        public async Task Exec_CancellationStopsTheProcess()
        {
            var shell = new CShell() { Echo = false };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var sleep = IsWindows ? "ping -n 30 127.0.0.1 > nul" : "sleep 30";

            var cancelled = false;
            try
            {
                await shell.ExecAsync(opt => opt.CancellationToken(cts.Token), ShellExe, ShellFlag, sleep);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled, "cancelling the token should stop the process");
        }

        [TestMethod]
        public void Exec_UnknownProgramThrows()
        {
            var shell = new CShell() { Echo = false };

            var threw = false;
            try
            {
                shell.Exec("this-program-does-not-exist-cshell-test");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "starting a program that does not exist should throw");
        }

        [TestMethod]
        public async Task ExecGlobal_ReturnsTheExitCode()
        {
            CShellNet.Globals.ResetShell();
            CShellNet.Globals.Echo = false;

            Assert.AreEqual(3, await CShellNet.Globals.ExecAsync(ShellExe, ShellFlag, "exit 3"));
        }
    }
}
