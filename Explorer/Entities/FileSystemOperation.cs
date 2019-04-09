using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Explorer.Entities
{
    public enum FileSystemOperations { Copy, Move }

    public class FileSystemOperation
    {
        public FileSystemOperations Operation { get; set; }
        public bool Determined { get; set; }
        public int Percentage { get; set; }
        public string SourceItemsString { get; set; }
        public FileSystemElement TargetFolder { get; set; }

        public FileSystemOperation(FileSystemOperations operation, string sourceItemString, FileSystemElement targetFolder)
        {
            Operation = operation;
            SourceItemsString = sourceItemString;
            TargetFolder = targetFolder;
        }
    }
}
