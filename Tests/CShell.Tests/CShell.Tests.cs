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
        public void Test_CurrentDirectory()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();
            shell.ChangeFolder(subFolder);
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "environment path not changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder.FullName, "currentFolder not changed");
            Assert.AreEqual(subFolder, shell.FolderHistory[1], "history should be updated");
            Assert.AreEqual(testFolder, shell.FolderHistory[0], "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
        }

        [TestMethod]
        public void Test_PushPopFolder()
        {
            Environment.CurrentDirectory = testFolder;

            CShell shell = new CShell();

            shell.PushFolder("subfolder");
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder, shell.CurrentFolder.FullName, "path should be changed");
            Assert.AreEqual(subFolder, shell.FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
            Assert.AreEqual(1, shell.FolderStack.Count, "stack should be one");

            shell.PushFolder("subfolder2");
            Assert.AreEqual(subFolder2, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "path should be changed");
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
        public void Test_ChangeFolder()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
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
        public void Test_CreateDeleteFolder()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz);
            }

            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.DeleteFolder("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_DeleteFolderRecursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz, true);
            }

            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.ChangeFolder("xyz");
            shell.CreateFolder("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.ChangeFolder(testFolder);
            shell.DeleteFolder("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }


        [TestMethod]
        public void Test_MoveFile()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.ChangeFolder(testFolder);
            File.WriteAllText(Path.Combine(shell.CurrentFolder.FullName, "test.txt"), "test");

            shell.MoveFile("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            shell.ChangeFolder(subFolder);
            shell.MoveFile("test.txt", Path.Combine("..", "test2.txt"));
            string path3 = Path.Combine(testFolder, "test2.txt");
            Assert.IsTrue(File.Exists(path3));

            File.Delete(Path.Combine(testFolder, "test2.txt"));
        }

        [TestMethod]
        public void Test_CopyFile()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.ChangeFolder(testFolder);
            File.WriteAllText(Path.Combine(shell.CurrentFolder.FullName, "test.txt"), "test");

            shell.CopyFile("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            shell.ChangeFolder(subFolder);
            shell.CopyFile("test.txt", Path.Combine("..", "test2.txt"));
            string path3 = Path.Combine(testFolder, "test2.txt");
            Assert.IsTrue(File.Exists(path3));

            File.Delete(Path.Combine(subFolder, "test.txt"));
            File.Delete(Path.Combine(testFolder, "test.txt"));
            File.Delete(Path.Combine(testFolder, "test2.txt"));
        }

        [TestMethod]
        public void Test_MoveCopyFolder()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.ChangeFolder(testFolder);
            if (Directory.Exists(shell.ResolvePath("test2")))
                shell.DeleteFolder("test2", recursive: true);

            shell.CopyFolder("subfolder", "test2");
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "subfolder2", "TestE.txt")));
            shell.MoveFolder("test2", "test3");
            Assert.IsTrue(!Directory.Exists("test2"));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "subfolder2", "TestE.txt")));

            shell.DeleteFolder("test3", recursive:true);
        }
    }

}
