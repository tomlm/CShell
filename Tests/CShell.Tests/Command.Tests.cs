using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CShellLibTests
{
    public class TestRecord
    {
        public TestRecord()
        {

        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("age")]
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
            testFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"..\..\..\test"));
            subFolder = Path.Combine(testFolder, "subfolder");
            subFolder2 = Path.Combine(subFolder, "subfolder2");
        }

        [TestMethod]
        public async Task Test_AsString()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var record = await shell.ReadFile("TestA.txt").AsString();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.Txt"));
            Assert.AreEqual(record, text, "AsString");
        }

        [TestMethod]
        public async Task Test_AsJson()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var record = await shell.ReadFile("TestA.txt").AsJson<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");

            JObject record2 = (JObject)await shell.ReadFile("TestA.txt").AsJson();
            Assert.AreEqual("Joe Smith", (string)record2["name"], "JOBject name is wrong");
            Assert.AreEqual(42, (int)record2["age"], "JOBject age is wrong");

            dynamic record3 = await shell.ReadFile("TestA.txt").AsJson();
            Assert.AreEqual("Joe Smith", (string)record3.name, "dynamic name is wrong");
            Assert.AreEqual(42, (int)record3.age, "dynamic age is wrong");
        }

        [TestMethod]
        public async Task Test_AsXml()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var record = await shell.ReadFile("TestB.txt").AsXml<TestRecord>();
            Assert.AreEqual("Joe Smith", record.Name, "name is wrong");
            Assert.AreEqual(42, record.Age, "age is wrong");
        }

        [TestMethod]
        public async Task Test_AsResult()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var result = await shell.ReadFile("TestA.txt").AsResult();
            var text = File.ReadAllText(Path.Combine(testFolder, "TestA.Txt"));
            Assert.AreEqual(text, result.StandardOutput, "result stdout");
            Assert.AreEqual("", result.StandardError, "result stderr");


            var badResult = await shell.ReadFile("sdfsdffd.txt").AsResult();
            Assert.AreEqual("", badResult.StandardOutput, "result stdout");
            Assert.AreEqual("The system cannot find the file specified.", badResult.StandardError.Trim(), "result stderr");
        }

        [TestMethod]
        public async Task Test_AsFile()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await shell.ReadFile("TestA.txt").AsFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2= await shell.ReadFile("TestAsdfsdf.txt").AsFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
        }

    }

}
