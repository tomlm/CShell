global using static CShellNet.Globals;
global using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CShellLibTests
{
    [TestClass]
    public class CShellGlobalsTests
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
        public void Test_Global_CurrentDirectory()
        {
            ResetShell(testFolder);
            cd(subFolder);
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "environment path not changed");
            Assert.AreEqual(subFolder, CurrentFolder.FullName, "currentFolder not changed");
            Assert.AreEqual(subFolder, FolderHistory[1], "history should be updated");
            Assert.AreEqual(testFolder, FolderHistory[0], "history should be updated");
            Assert.AreEqual(2, FolderHistory.Count(), "history should be two");
        }

        [TestMethod]
        public void Test_Global_PushPopFolder()
        {
            ResetShell(testFolder);

            PushFolder("subfolder");
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder, CurrentFolder.FullName, "path should be changed");
            Assert.AreEqual(subFolder, FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(2, FolderHistory.Count(), "history should be two");
            Assert.AreEqual(1, FolderStack.Count, "stack should be one");

            PushFolder("subfolder2");
            Assert.AreEqual(subFolder2, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(subFolder2, CurrentFolder.FullName, "path should be changed");
            Assert.AreEqual(subFolder2, FolderHistory.Last(), "history should be updated");
            Assert.AreEqual(3, FolderHistory.Count(), "history should be three");
            Assert.AreEqual(2, FolderStack.Count, "stack should be two");

            PopFolder();
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(1, FolderStack.Count, "stack should be one");

            PopFolder();
            Assert.AreEqual(testFolder, Environment.CurrentDirectory, "CurrentDirectory should be changed");
            Assert.AreEqual(0, FolderStack.Count, "stack should be zero");

            try
            {
                PopFolder();
                Assert.Fail("PopFolder() Should have thrown when empty");
            }
            catch (Exception) { }
        }

        [TestMethod]
        public void Test_Global_ChangeFolder()
        {
            ResetShell(testFolder);

            cd(@"subfolder\subfolder2");
            Assert.AreEqual(subFolder2, CurrentFolder.FullName, "currentFolder relative path failed");
            cd(@"..\subfolder2");
            Assert.AreEqual(subFolder2, CurrentFolder.FullName, "currentFolder relative path failed2");
            Assert.AreEqual(2, FolderHistory.Count, "relative non-navigation shouldn't have created history record");
            cd(@"..\..");
            Assert.AreEqual(testFolder, CurrentFolder.FullName, "currentFolder relative path failed3");
            Assert.AreEqual(3, FolderHistory.Count, "history ignored on relative path");
            cd(subFolder2);
            Assert.AreEqual(subFolder2, CurrentFolder.FullName, "absolute path failed");
        }

        [TestMethod]
        public async Task Test_Global_echo()
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
        public void Test_Global_Createrd()
        {
            ResetShell(testFolder);

            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz);
            }

            md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            rd("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_Global_rdRecursive()
        {
            ResetShell(testFolder);
            ResetShell(testFolder);

            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
            {
                Directory.Delete(xyz, true);
            }

            md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            cd("xyz");
            md("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            ResetShell(testFolder);
            rd("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }


        [TestMethod]
        public void Test_Global_move()
        {
            ResetShell(testFolder);


            ResetShell(testFolder);
            File.WriteAllText(Path.Combine(CurrentFolder.FullName, "test.txt"), "test");

            move("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            cd(subFolder);
            move("test.txt", Path.Combine("..", "test2.txt"));
            string path3 = Path.Combine(testFolder, "test2.txt");
            Assert.IsTrue(File.Exists(path3));

            File.Delete(Path.Combine(testFolder, "test2.txt"));
        }

        [TestMethod]
        public void Test_Global_copy()
        {
            ResetShell(testFolder);


            ResetShell(testFolder);
            File.WriteAllText(Path.Combine(CurrentFolder.FullName, "test.txt"), "test");

            copy("test.txt", subFolder);
            string path2 = Path.Combine(subFolder, "test.txt");
            Assert.IsTrue(File.Exists(path2));

            cd(subFolder);
            copy("test.txt", Path.Combine("..", "test2.txt"));
            string path3 = Path.Combine(testFolder, "test2.txt");
            Assert.IsTrue(File.Exists(path3));

            File.Delete(Path.Combine(subFolder, "test.txt"));
            File.Delete(Path.Combine(testFolder, "test.txt"));
            File.Delete(Path.Combine(testFolder, "test2.txt"));
        }

        [TestMethod]
        public void Test_Global_MoveCopyFolder()
        {
            ResetShell(testFolder);


            ResetShell(testFolder);
            if (Directory.Exists(ResolvePath("test2")))
                rd("test2", recursive: true);

            copy("subfolder", "test2");
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test2", "subfolder2", "TestE.txt")));
            move("test2", "test3");
            Assert.IsTrue(!Directory.Exists("test2"));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "TestC.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(testFolder, "test3", "subfolder2", "TestE.txt")));

            rd("test3", recursive: true);
        }
    }

}
