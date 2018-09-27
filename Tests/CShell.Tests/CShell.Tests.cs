using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CShellLibTests
{
    [TestClass]
    public class CShellTests
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
        public void CShellEnvironment()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();
            Assert.AreEqual(Environment.GetEnvironmentVariable("path"), shell.Environment["Path"], "environment missing");
            Assert.AreEqual(testFolder, shell.CurrentFolder, "currentFolder should be set");
            Assert.AreEqual(testFolder, shell.FolderHistory.First(), "history should be set");
            Assert.AreEqual(1, shell.FolderHistory.Count(), "history should be one");
        }

        [TestMethod]
        public void CShellFolderChangingTracked()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();
            shell.CurrentFolder = subFolder;
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "environment path not changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder, "currentFolder not changed");
            Assert.AreEqual(subFolder, shell.FolderHistory[1], "history should be updated");
            Assert.AreEqual(testFolder, shell.FolderHistory[0], "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
        }

        [TestMethod]
        public void CShellPushPopFolder()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();

            shell.PushFolder("subfolder");
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder, "path should be changed");
            Assert.AreEqual(subFolder, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.PushFolder("subfolder2");
            Assert.AreEqual(subFolder2, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder2, shell.CurrentFolder, "path should be changed");
            Assert.AreEqual(subFolder2, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(3, shell.FolderHistory.Count(), "history should be three");
            Assert.AreEqual(2, shell.FolderStack.Count, "stack should be two");

            shell.PopFolder();
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.PopFolder();
            Assert.AreEqual(testFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(0, shell.FolderStack.Count, "stack should be zero");

            try
            {
                shell.PopFolder();
                Assert.Fail("PopFolder() Should have thrown when empty");
            }
            catch (Exception) { }
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
        public void CreateDeleteFolder()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz);

            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.DeleteFolder("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void DeleteFolderRecursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz, true);

            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.ChangeFolder("xyz");
            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.CurrentFolder = testFolder;
            shell.DeleteFolder("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void TestList()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();

            Assert.IsTrue(shell.List().Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.List().Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.List().Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.AreEqual(3, shell.List().Count(), "shallow count is wrong");

            Assert.IsTrue(shell.List(recursive:true).Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "TestC.txt").Any(), "missing TestC");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "TestD.txt").Any(), "missing TestD");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "TestE.txt").Any(), "missing TestE");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.IsTrue(shell.List(recursive: true).Where(path => Path.GetFileName(path) == "subfolder2").Any(), "missing subfolder2");
            Assert.AreEqual(8, shell.List(recursive: true).Count(), "shallow count is wrong");

            Assert.IsTrue(shell.List("TestF*", recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF pattern");
            Assert.AreEqual(1, shell.List("TestF*", recursive: true).Count(), "count wrong TestF pattern");
        }

        [TestMethod]
        public void TestListFiles()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();

            Assert.IsTrue(shell.ListFiles().Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.ListFiles().Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.AreEqual(2, shell.ListFiles().Count(), "shallow count is wrong");

            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestC.txt").Any(), "missing TestC");
            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestD.txt").Any(), "missing TestD");
            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestE.txt").Any(), "missing TestE");
            Assert.IsTrue(shell.ListFiles(recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF");
            Assert.AreEqual(6, shell.ListFiles(recursive: true).Count(), "shallow count is wrong");

            Assert.IsTrue(shell.ListFiles("TestF*", recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF pattern");
            Assert.AreEqual(1, shell.ListFiles("TestF*", recursive: true).Count(), "count wrong TestF pattern");
        }

        [TestMethod]
        public void TestListFolders()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();

            Assert.IsTrue(shell.ListFolders().Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.AreEqual(1, shell.ListFolders().Count(), "shallow count is wrong");

            Assert.IsTrue(shell.ListFolders(recursive: true).Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.IsTrue(shell.ListFolders(recursive: true).Where(path => Path.GetFileName(path) == "subfolder2").Any(), "missing subfolder2");
            Assert.AreEqual(2, shell.ListFolders(recursive: true).Count(), "shallow count is wrong");

            Assert.IsFalse(shell.ListFolders("TestF*", recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF pattern");
            Assert.AreEqual(0, shell.ListFolders("TestF*", recursive: true).Count(), "count wrong TestF pattern");
        }

    }

}
