using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CShellLibTests
{
    public class TestRecord
    {
        public TestRecord()
        {

        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }
    }

    /// <summary>The same shape with no attributes, so nothing but case-insensitive matching can fill it.</summary>
    public class UnmappedRecord
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    [TestClass]
    public class CommandTests
    {
        private static string testFolder;
        private static string subFolder;
        private static string subFolder2;

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            testFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "..", "..", "test"));
            subFolder = Path.Combine(testFolder, "subfolder");
            subFolder2 = Path.Combine(subFolder, "subfolder2");
        }

        [TestMethod]
        public async Task Test_AsString()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            var result = await shell.Run(ShellExe, ShellFlag, "echo this is a yo yo").AsString();
            Assert.AreEqual("this is a yo yo", result.Trim(), "AsString");

            var record = await shell.ReadFile("TestA.txt").AsString();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.txt"));
            Assert.AreEqual(record, text, "AsString");
        }

        [TestMethod]
        public async Task Test_AsJson()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            var record = await shell.ReadFile("TestA.txt").AsJson<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");

            JsonNode record2 = await shell.ReadFile("TestA.txt").AsJson();
            Assert.AreEqual("Joe Smith", (string)record2["name"], "JsonNode name is wrong");
            Assert.AreEqual(42, (int)record2["age"], "JsonNode age is wrong");

            // Nested indexing is how a JsonNode is navigated. Member access -- record.name --
            // came from the Newtonsoft JObject and is gone with it.
            Assert.IsNull(record2["nope"], "a missing property reads as null");

            // System.Text.Json matches property names case sensitively by default, which would
            // leave both of these at their defaults rather than failing. CShell turns that off,
            // because CLI tools emit camelCase and the C# modelling them is PascalCase.
            var unmapped = await shell.ReadFile("TestA.txt").AsJson<UnmappedRecord>();
            Assert.AreEqual("Joe Smith", unmapped.Name, "lowercase json must still fill a PascalCase property");
            Assert.AreEqual(42, unmapped.Age, "lowercase json must still fill a PascalCase property");
        }


        [TestMethod]
        public async Task Test_AsXml()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            var record = await shell.ReadFile("TestB.txt").AsXml<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");
        }

        [TestMethod]
        public async Task Test_AsResult()
        {
            CShell shell = new CShell();
            shell.ThrowOnError = false;
            shell.cd(testFolder);

            var result = await shell.ReadFile("TestA.txt").AsResult();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.txt"));
            Assert.AreEqual(text, result.StandardOutput, "result stdout");
            Assert.AreEqual("", result.StandardError, "result stderr");


            var badResult = await shell.ReadFile("sdfsdffd.txt").AsResult();
            Assert.AreEqual("", badResult.StandardOutput, "result stdout");
            Assert.IsFalse(badResult.Success, "reading a file that is not there should fail");
            Assert.AreNotEqual(String.Empty, badResult.StandardError.Trim(), "and should say so on stderr");
        }

        [TestMethod]
        public async Task Test_AsFile()
        {
            CShell shell = new CShell();
            shell.ThrowOnError = false;
            shell.cd(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await shell.ReadFile("TestA.txt").AsFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2 = await shell.ReadFile("TestAsdfsdf.txt").AsFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
        }

        [TestMethod]
        public async Task Test_Throw_AsJson()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            try
            {
                var record = await shell.Run("xyz").AsJson();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("xyz"));
            }
        }

        [TestMethod]
        public async Task Test_Throw_AsXml()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            try
            {
                var record = await shell.Run("xyz").AsXml<object>();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("xyz"));
            }
        }

        [TestMethod]
        public async Task Test_Throw_AsString()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);

            try
            {
                var record = await shell.Run("xyz").AsString();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("xyz"));
            }
        }


        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>The shell to hand a command line to, and the flag that says "run this".</summary>
        private static string ShellExe => IsWindows ? "cmd" : "bash";

        private static string ShellFlag => IsWindows ? "/c" : "-c";

        /// <summary>List one file, in whichever shell Cmd() runs: cmd.exe on Windows, bash elsewhere.</summary>
        private static string ListTestA => IsWindows ? "dir /b TestA.txt" : "ls TestA.txt";

        [TestMethod]
        public async Task Test_Cmd()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);
            var result = await shell.Cmd(ListTestA).AsString();
            Assert.AreEqual("TestA.txt", result.Trim(), "AsString");
        }

        [TestMethod]
        public async Task Test_Bash()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);
            var result = await shell.Bash("ls TestA.txt").AsString();
            Assert.AreEqual("TestA.txt", result.Trim(), "AsString");
        }

        [TestMethod]
        public async Task Test_Start()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);
            var command = shell.Start("dotnet", "sleep.dll", "1000");
            Assert.IsFalse(command.Process.HasExited);
            await command.Task;
            Assert.IsTrue(command.Process.HasExited);
        }

        [TestMethod]
        public async Task Test_StartExecute()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);
            var result = await shell.Start("dotnet", "sleep.dll", "1000").Execute();
            Assert.IsTrue(result.Success);
            try
            {
                result = await shell.Start("xxxxx-no-such-program").Execute();
                Assert.Fail("Should have thrown execption)");
            }
            catch 
            {

            }
        }

        //[TestMethod]
        //public async Task Test_StartKill()
        //{
        //    CShell shell = new CShell();
        //    shell.cd(testFolder);
        //    var command = shell.Start("dotnet", "sleep.dll", "1000");
        //    Assert.IsFalse(command.Process.HasExited);
        //    await Task.Delay(500);
        //    command.Kill();
        //    Assert.IsTrue(command.Process.HasExited);
        //}

        [TestMethod]
        public async Task Test_Log()
        {
            CShell shell = new CShell();
            shell.cd(testFolder);
            var commandResult = await shell.Cmd(ListTestA).Execute(true);
        }

    }

}
