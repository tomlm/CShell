global using static CShellNet.Globals;
global using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CShellLibTests
{
    [TestClass]
    public class CommandGlobalTests
    {
        private static string testFolder;
        private static string subFolder;
        private static string subFolder2;

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            testFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"..\..\..\test"));
            subFolder = Path.Combine(testFolder, "subfolder");
            subFolder2 = Path.Combine(subFolder, "subfolder2");
        }

        [TestMethod]
        public async Task Test_Global_AsString()
        {
            ResetShell(testFolder);

            var result = await Run("cmd", "/c", "echo this is a yo yo").AsString();
            Assert.AreEqual("this is a yo yo", result.Trim(), "AsString");

            var record = await ReadFile("TestA.txt").AsString();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.Txt"));
            Assert.AreEqual(record, text, "AsString");
        }

        [TestMethod]
        public async Task Test_Global_AsJson()
        {

            ResetShell(testFolder);

            var record = await ReadFile("TestA.txt").AsJson<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");

            JObject record2 = (JObject)await ReadFile("TestA.txt").AsJson();
            Assert.AreEqual("Joe Smith", (string)record2["name"], "JOBject name is wrong");
            Assert.AreEqual(42, (int)record2["age"], "JOBject age is wrong");

            dynamic record3 = await ReadFile("TestA.txt").AsJson();
            Assert.AreEqual("Joe Smith", (string)record3.name, "dynamic name is wrong");
            Assert.AreEqual(42, (int)record3.age, "dynamic age is wrong");
        }


        [TestMethod]
        public async Task Test_Global_AsXml()
        {
            ResetShell(testFolder);

            var record = await ReadFile("TestB.txt").AsXml<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");
        }

        [TestMethod]
        public async Task Test_Global_AsResult()
        {

            ThrowOnError = false;
            ResetShell(testFolder);

            var result = await ReadFile("TestA.txt").AsResult();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.Txt"));
            Assert.AreEqual(text, result.StandardOutput, "result stdout");
            Assert.AreEqual("", result.StandardError, "result stderr");


            var badResult = await ReadFile("sdfsdffd.txt").AsResult();
            Assert.AreEqual("", badResult.StandardOutput, "result stdout");
            Assert.AreEqual("The system cannot find the file specified.", badResult.StandardError.Trim(), "result stderr");
        }

        [TestMethod]
        public async Task Test_Global_AsFile()
        {

            ThrowOnError = false;
            ResetShell(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await ReadFile("TestA.txt").AsFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2 = await ReadFile("TestAsdfsdf.txt").AsFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
        }

        [TestMethod]
        public async Task Test_Global_Throw_AsJson()
        {

            ResetShell(testFolder);

            try
            {
                var record = await Run("xyz").AsJson();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("The system cannot find the file specified."));
            }
        }

        [TestMethod]
        public async Task Test_Global_Throw_AsXml()
        {

            ResetShell(testFolder);

            try
            {
                var record = await Run("xyz").AsXml<object>();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("The system cannot find the file specified."));
            }
        }

        [TestMethod]
        public async Task Test_Global_Throw_AsString()
        {

            ResetShell(testFolder);

            try
            {
                var record = await Run("xyz").AsString();
                Assert.Fail("Should have thrown");
            }
            catch (Exception err)
            {
                Assert.IsTrue(err.Message.Contains("The system cannot find the file specified."));
            }
        }

        [TestMethod]
        public async Task Test_Global_Cmd()
        {

            ResetShell(testFolder);
            var result = await Cmd("dir /b TestA.txt").AsString();
            Assert.AreEqual("TestA.txt", result.Trim(), "AsString");
        }

        [TestMethod]
        public async Task Test_Global_Bash()
        {

            ResetShell(testFolder);
            var result = await Bash("ls TestA.txt").AsString();
            Assert.AreEqual("TestA.txt", result.Trim(), "AsString");
        }

        [TestMethod]
        public async Task Test_Global_Start()
        {
            ResetShell(testFolder);
            var command = Start("dotnet", "sleep.dll", "1000");
            Assert.IsFalse(command.Process.HasExited);
            await command.Task;
            Assert.IsTrue(command.Process.HasExited);
        }

        [TestMethod]
        public async Task Test_Global_StartExecute()
        {

            ResetShell(testFolder);
            var result = await Start("dotnet", "sleep.dll", "1000").Execute();
            Assert.IsTrue(result.Success);
            try
            {
                result = await Start("xxxxxtest.cmd").Execute();
                Assert.Fail("Should have thrown execption)");
            }
            catch 
            {

            }
        }

        //[TestMethod]
        //public async Task Test_Global_StartKill()
        //{

        //    ResetShell(testFolder);
        //    using (var command = Start("dotnet", "sleep.dll", "1000"))
        //    {
        //        Assert.IsFalse(command.Process.HasExited);
        //        await Task.Delay(500);
        //        command.Kill();
        //        Assert.IsTrue(command.Process.HasExited);
        //    }
        //}

        [TestMethod]
        public async Task Test_Global_Log()
        {

            ResetShell(testFolder);
            var commandResult = await Cmd("dir /b TestA.txt").Execute(true);
        }

    }

}
