using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorer.Logic.History
{
    public interface IFileSystemElementOperation
    {
        int ElementId { get; set; }
        string Path { get; set; }
        string Name { get; set; }
    }

    public class FileSystemElementCreateOperation : IFileSystemElementOperation
    {
        public int ElementId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
    }

    public class FileSystemElementDeleteOperation : IFileSystemElementOperation
    {
        public int ElementId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
    }

    public class FileSystemElementRenameOperation : IFileSystemElementOperation
    {
        public int ElementId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public string OriginalName { get; set; }
    }

    public class FileSystemElementMoveOperation : IFileSystemElementOperation
    {
        public int ElementId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public string OriginalName { get; set; }
        public string OriginalPath { get; set; }
    }

    public class FileSystemElementPasteOperation : IFileSystemElementOperation
    {
        public int ElementId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public string OriginalName { get; set; }
        public string OriginalPath { get; set; }
    }
}
