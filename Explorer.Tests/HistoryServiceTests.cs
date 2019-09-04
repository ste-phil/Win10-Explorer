using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Explorer.Entities;
using Explorer.Logic;
using Explorer.Logic.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Explorer.Tests
{
    [TestClass]
    public class HistoryServiceTests
    {
        private readonly string[] folderNames =
        {
            "AMD", "System", "Windows", "Git", "Intel", "Programs", "Program Files", "Games", "Office 19"
        };

        private HistoryService hs;

        [TestInitialize]
        public void Init()
        {
            hs = HistoryService.Instance;
            hs.GetType().GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hs, null);
        }

        [TestMethod]
        public void TestAdd()
        {
            var fse = new FileSystemElement("Windows", "C:", DateTimeOffset.Now, 500);

            hs.AddCreateOperation(fse);
            var h = hs.GetHistory(fse).ToList();
            CompareOperations(new List<IFileSystemElementOperation> { new FileSystemElementCreateOperation() }, h, false);
        }

        [TestMethod]
        public void TestAddMove()
        {
            var fse = new FileSystemElement("Windows", "C:", DateTimeOffset.Now, 500);
            var op = fse.Path;
            var on = fse.Name;

            MoveToRandomPath(fse);
            RenameElement(fse);

            hs.AddMoveOperation(fse, op, on);
            var h = hs.GetHistory(fse).ToList();

            CompareOperations(
                new List<IFileSystemElementOperation> {
                    new FileSystemElementCreateOperation(),
                    new FileSystemElementMoveOperation()
                }, 
                h,
                false
            );
        }

        [TestMethod]
        public void TestAddContent()
        {
            var fse = new FileSystemElement("Windows", "C:", DateTimeOffset.Now, 500);
            var op = fse.Path;
            var on = fse.Name;

            var newPath = MoveToRandomPath(fse);
            var newName = RenameElement(fse);

            hs.AddMoveOperation(fse, op, on);
            var h = hs.GetHistory(fse).ToList();

            CompareOperations(
                new List<IFileSystemElementOperation> {
                    new FileSystemElementCreateOperation { ElementId = 1, Name = "Windows", Path = "C:" },
                    new FileSystemElementMoveOperation { ElementId = 1, Name = newName, Path = newPath, OriginalName = "Windows", OriginalPath = "C:" }
                },
                h,
                true
            );
        }


        [TestMethod]
        public void TestRenameMultiple004()
        {
            var fse1Name = "Windows";
            var fse1Path = "C:";
            var fse2Name = "Linux";
            var fse2Path = "D:";

            var fse1 = new FileSystemElement(fse1Name, fse1Path, DateTimeOffset.Now, 500);
            var fse2 = new FileSystemElement(fse2Name, fse2Path, DateTimeOffset.Now, 500);

            hs.AddCreateOperation(fse1);
            hs.AddCreateOperation(fse2);

            var newName1_1 = RenameElement(fse1);
            hs.AddRenameOperation(fse1, fse1Name);

            var newName2_1 = RenameElement(fse2);
            hs.AddRenameOperation(fse2, fse1Name);

            var newName1_2 = RenameElement(fse1);
            hs.AddRenameOperation(fse1, newName1_1);

            var h1 = hs.GetHistory(fse1).ToList();
            var h2 = hs.GetHistory(fse2).ToList();

            CompareOperations(
                new List<IFileSystemElementOperation> {
                    new FileSystemElementCreateOperation(),
                    new FileSystemElementRenameOperation(),
                    new FileSystemElementRenameOperation(),
                },
                h1,
                false
            );

            CompareOperations(
                new List<IFileSystemElementOperation> {
                    new FileSystemElementCreateOperation(),
                    new FileSystemElementRenameOperation(),
                },
                h2,
                false
            );
        }

        private string MoveToRandomPath(FileSystemElement fse)
        {
            var random = new Random();
            var depth = random.Next(4);
            var newPath = "";
            for (int i = 0; i < depth; i++)
            {
                var val = random.Next(folderNames.Length);
                newPath += folderNames[val] + "\\";
            }

            fse.Path = newPath;
            return newPath;
        }

        private string RenameElement(FileSystemElement fse)
        {
            var random = new Random();
            var val = random.Next(folderNames.Length);
            fse.Name = folderNames[val];
            return fse.Name;
        }

        private void CompareOperations(List<IFileSystemElementOperation> expected, List<IFileSystemElementOperation> actual, bool cmpContent)
        {
            if (expected.Count < actual.Count) Assert.Fail("There are more operations than expected");
            if (expected.Count > actual.Count) Assert.Fail("There are less operations than expected");

            for (int i = 0; i < expected.Count; i++)
            {
                var type = expected[i].GetType();
                Assert.IsInstanceOfType(actual[i], type);

                if (cmpContent) CompareOperation((dynamic)expected[i], (dynamic)actual[i]);
            }
        }

        private void CompareOperationBase(IFileSystemElementOperation expected, IFileSystemElementOperation actual)
        {
            Assert.AreEqual(expected.ElementId, actual.ElementId);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Path, actual.Path);
        }

        private void CompareOperation(FileSystemElementMoveOperation expected, FileSystemElementMoveOperation actual)
        {
            CompareOperationBase(expected, actual);
            Assert.AreEqual(expected.OriginalName, actual.OriginalName);
            Assert.AreEqual(expected.OriginalPath, actual.OriginalPath);
        }

        private void CompareOperation(FileSystemElementCreateOperation expected, FileSystemElementCreateOperation actual)
        {
            CompareOperationBase(expected, actual);
        }

        private void CompareOperation(FileSystemElementDeleteOperation expected, FileSystemElementDeleteOperation actual)
        {
            CompareOperationBase(expected, actual);
        }

        private void CompareOperation(FileSystemElementRenameOperation expected, FileSystemElementRenameOperation actual)
        {
            CompareOperationBase(expected, actual);
            Assert.AreEqual(expected.OriginalName, actual.OriginalName);
        }

        private void CompareOperation(FileSystemElementPasteOperation expected, FileSystemElementPasteOperation actual)
        {
            CompareOperationBase(expected, actual);
            Assert.AreEqual(expected.OriginalName, actual.OriginalName);
            Assert.AreEqual(expected.OriginalPath, actual.OriginalPath);
        }
    }
}
