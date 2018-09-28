using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CmdShellLibTests
{
    [TestClass]
    public class CmdStyleTests
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
        public void Test_pushd_popd()
        {
            Environment.CurrentDirectory = testFolder;

            CmdShell shell = new CmdShell();

            shell.pushd("subfolder");
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder.FullName, "path should be changed");
            Assert.AreEqual(subFolder, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.pushd("subfolder2");
            Assert.AreEqual(subFolder2, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "path should be changed");
            Assert.AreEqual(subFolder2, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(3, shell.FolderHistory.Count(), "history should be three");
            Assert.AreEqual(2, shell.FolderStack.Count, "stack should be two");

            shell.popd();
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.popd();
            Assert.AreEqual(testFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(0, shell.FolderStack.Count, "stack should be zero");

            try
            {
                shell.popd();
                Assert.Fail("popd() Should have thrown when empty");
            }
            catch(Exception) { }
        }

        [TestMethod]
        public void Test_cd()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CmdShell();
            shell.ChangeFolder(@"subfolder\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "currentFolder relative path failed");
            shell.ChangeFolder(@"..\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "currentFolder relative path failed2");
            Assert.AreEqual(2, shell.FolderHistory.Count, "relative non-navigation shouldn't have created history record");
            shell.ChangeFolder(@"..\..");
            Assert.AreEqual(testFolder, shell.CurrentFolder.FullName, "currentFolder relative path failed3");
            Assert.AreEqual(3, shell.FolderHistory.Count, "history ignored on relative path");
            shell.ChangeFolder(subFolder2);
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "absolute path failed");
        }

        [TestMethod]
        public void Test_md_rd()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CmdShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz);

            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.rd("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_rd_recursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CmdShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz, true);

            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.ChangeFolder("xyz");
            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.ChangeFolder(testFolder);
            shell.rd("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_dir()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CmdShell();

            Assert.IsTrue(shell.dir().Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.dir().Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.dir().Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.AreEqual(3, shell.dir().Count(), "shallow count is wrong");

            Assert.IsTrue(shell.dir(recursive:true).Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "TestC.txt").Any(), "missing TestC");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "TestD.txt").Any(), "missing TestD");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "TestE.txt").Any(), "missing TestE");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.IsTrue(shell.dir(recursive: true).Where(path => Path.GetFileName(path) == "subfolder2").Any(), "missing subfolder2");
            Assert.AreEqual(8, shell.dir(recursive: true).Count(), "shallow count is wrong");

            Assert.IsTrue(shell.dir("TestF*", recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF pattern");
            Assert.AreEqual(1, shell.dir("TestF*", recursive: true).Count(), "count wrong TestF pattern");
        }


        [TestMethod]
        public async Task Test_type()
        {
            CmdShell shell = new CmdShell();
            shell.ChangeFolder(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await shell.type("TestA.txt").AsFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2 = await shell.type("TestAsdfsdf.txt").AsFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
        }

    }

}
