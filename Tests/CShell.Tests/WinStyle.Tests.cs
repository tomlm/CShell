using CShellNet;
using CShellNet.CmdStyle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CShellLibTests
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
        public void CShellFolderChangingTracked()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();
            shell.ChangeFolder(subFolder);
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "environment path not changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder, "currentFolder not changed");
            Assert.AreEqual(subFolder, shell.FolderHistory[1], "history should be updated");
            Assert.AreEqual(testFolder, shell.FolderHistory[0], "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
        }

        [TestMethod]
        public void CShellPushpopd()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();

            shell.pushd("subfolder");
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder, "path should be changed");
            Assert.AreEqual(subFolder, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.pushd("subfolder2");
            Assert.AreEqual(subFolder2, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder2, shell.CurrentFolder, "path should be changed");
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
        public void ChangeFolder()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.ChangeFolder(@"subfolder\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder, "currentFolder relative path failed");
            shell.ChangeFolder(@"..\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder, "currentFolder relative path failed2");
            Assert.AreEqual(2, shell.FolderHistory.Count, "relative non-navigation shouldn't have created history record");
            shell.ChangeFolder(@"..\..");
            Assert.AreEqual(testFolder, shell.CurrentFolder, "currentFolder relative path failed3");
            Assert.AreEqual(3, shell.FolderHistory.Count, "history ignored on relative path");
            shell.ChangeFolder(subFolder2);
            Assert.AreEqual(subFolder2, shell.CurrentFolder, "absolute path failed");
        }

        [TestMethod]
        public void Createrd()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz);

            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.rd("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void rdRecursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
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
        public void Testdir()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();

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
        public async Task TestAsFile()
        {
            CShell shell = new CShell();
            shell.ChangeFolder(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await shell.type("TestA.txt").ToFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2 = await shell.type("TestAsdfsdf.txt").ToFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
        }

    }

}