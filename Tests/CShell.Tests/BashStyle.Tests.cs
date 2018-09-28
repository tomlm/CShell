using CShellNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BashShellLibTests
{
    [TestClass]
    public class BashStyleTests
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
        public void Test_cwd()
        {
            Environment.CurrentDirectory = testFolder;

            BashShell shell = new BashShell();
            shell.cd(subFolder);
            Assert.AreEqual(subFolder, Environment.CurrentDirectory, "environment path not changed");
            Assert.AreEqual(subFolder, shell.cwd(), "currentFolder not changed");
            Assert.AreEqual(subFolder, shell.FolderHistory[1], "history should be updated");
            Assert.AreEqual(testFolder, shell.FolderHistory[0], "history should be updated");
            Assert.AreEqual(2, shell.FolderHistory.Count(), "history should be two");
        }

        [TestMethod]
        public void Test_cd()
        {
            Environment.CurrentDirectory = testFolder;

            var shell = new BashShell();
            shell.cd(@"subfolder\subfolder2");
            Assert.AreEqual(subFolder2, shell.cwd(), "currentFolder relative path failed");
            shell.cd(@"..\subfolder2");
            Assert.AreEqual(subFolder2, shell.cwd(), "currentFolder relative path failed2");
            Assert.AreEqual(2, shell.FolderHistory.Count, "relative non-navigation shouldn't have created history record");
            shell.cd(@"..\..");
            Assert.AreEqual(testFolder, shell.cwd(), "currentFolder relative path failed3");
            Assert.AreEqual(3, shell.FolderHistory.Count, "history ignored on relative path");
            shell.cd(subFolder2);
            Assert.AreEqual(subFolder2, shell.cwd(), "absolute path failed");
        }

        [TestMethod]
        public void Test_rmdir()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new BashShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz);

            shell.mkdir("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.rmdir("xyz");
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void test_rmdir_recursive()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new BashShell();
            string xyz = Path.Combine(testFolder, "xyz");
            if (Directory.Exists(xyz))
                Directory.Delete(xyz, true);

            shell.mkdir("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.cd("xyz");
            shell.mkdir("xyz");
            Assert.IsTrue(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");

            shell.cd(testFolder);
            shell.rmdir("xyz", true);
            Assert.IsFalse(Directory.EnumerateDirectories(testFolder).Where(path => Path.GetFileName(path) == "xyz").Any(), "xyz not created");
        }

        [TestMethod]
        public void Test_ls()
        {
            Environment.CurrentDirectory = testFolder;
            var shell = new BashShell();

            Assert.IsTrue(shell.ls().Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.ls().Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.ls().Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.AreEqual(3, shell.ls().Count(), "shallow count is wrong");

            Assert.IsTrue(shell.ls(recursive:true).Where(path => Path.GetFileName(path) == "TestA.txt").Any(), "missing TestA");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "TestB.txt").Any(), "missing TestB");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "TestC.txt").Any(), "missing TestC");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "TestD.txt").Any(), "missing TestD");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "TestE.txt").Any(), "missing TestE");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "subfolder").Any(), "missing subfolder");
            Assert.IsTrue(shell.ls(recursive: true).Where(path => Path.GetFileName(path) == "subfolder2").Any(), "missing subfolder2");
            Assert.AreEqual(8, shell.ls(recursive: true).Count(), "shallow count is wrong");

            Assert.IsTrue(shell.ls("TestF*", recursive: true).Where(path => Path.GetFileName(path) == "TestF.txt").Any(), "missing TestF pattern");
            Assert.AreEqual(1, shell.ls("TestF*", recursive: true).Count(), "count wrong TestF pattern");
        }

        [TestMethod]
        public async Task Test_cat()
        {
            BashShell shell = new BashShell();
            shell.ChangeFolder(testFolder);

            var tmpOut = Path.GetTempFileName();
            var tmpErr = Path.GetTempFileName();

            var result = await shell.cat("TestA.txt").AsFile(tmpOut);
            var stdout = File.ReadAllText(tmpOut);
            Assert.AreEqual(stdout, result.StandardOutput, "result stdout");

            var result2 = await shell.cat("TestAsdfsdf.txt").AsFile(tmpOut, tmpErr);
            var stdout2 = File.ReadAllText(tmpOut);
            var stderr2 = File.ReadAllText(tmpErr);
            Assert.AreEqual(stdout2, result2.StandardOutput, "result stdout");
            Assert.AreEqual(stderr2, result2.StandardError, "result stderr");
            File.Delete(tmpOut);
            File.Delete(tmpErr);
        }
    }

}
