using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
            shell.cd(subFolder);
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
            shell.cd(@"subfolder\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "currentFolder relative path failed");
            shell.cd(@"..\subfolder2");
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "currentFolder relative path failed2");
            Assert.AreEqual(2, shell.FolderHistory.Count, "relative non-navigation shouldn't have created history record");
            shell.cd(@"..\..");
            Assert.AreEqual(testFolder, shell.CurrentFolder.FullName, "currentFolder relative path failed3");
            Assert.AreEqual(3, shell.FolderHistory.Count, "history ignored on relative path");
            shell.cd(subFolder2);
            Assert.AreEqual(subFolder2, shell.CurrentFolder.FullName, "absolute path failed");
        }

        [TestMethod]
        public void Test_Createrd()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz);
            }

            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.rd("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_rdRecursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new CShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz, true);
            }

            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.cd("xyz");
            shell.md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.cd(testFolder);
            shell.rd("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }


        [TestMethod]
        public void Test_move()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.cd(testFolder);
            File.WriteAllText(Path.Combine(shell.CurrentFolder.FullName, "test.txt"), "test");

            shell.move("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            shell.cd(subFolder);
            shell.move("test.txt", Path.Combine("..", "test2.txt"));
            string path3 = Path.Combine(testFolder, "test2.txt");
            Assert.IsTrue(File.Exists(path3));

            File.Delete(Path.Combine(testFolder, "test2.txt"));
        }

        [TestMethod]
        public async Task Test_echo()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            var result = await shell.echo("test").AsString();
            Assert.AreEqual("test", result);
            var result2 = shell.echo(new string[] { "test1", "test2", "test3" });
            var x = await result2.StandardOutput.ReadToEndAsync();
            Assert.AreEqual("test1\r\ntest2\r\ntest3\r\n", x);
        }

        [TestMethod]
        public void Test_copy()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new CShell();
            shell.cd(testFolder);
            File.WriteAllText(Path.Combine(shell.CurrentFolder.FullName, "test.txt"), "test");

            shell.copy("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            shell.cd(subFolder);
            shell.copy("test.txt", Path.Combine("..", "test2.txt"));
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
            shell.cd(testFolder);
            if (Directory.Exists(shell.ResolvePath("test2")))
                shell.rd("test2", recursive: true);

            shell.copy("subfolder", "test2");
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "subfolder2", "TestE.txt")));
            shell.move("test2", "test3");
            Assert.IsTrue(!Directory.Exists("test2"));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "subfolder2", "TestE.txt")));

            shell.rd("test3", recursive:true);
        }
    }

}
