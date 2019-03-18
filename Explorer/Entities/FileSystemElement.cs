using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Explorer.Logic;

namespace Explorer.Entities
{
    public class FileSystemElement
    {
        public FileAttributes Type { get; set; }
        public string Name { get; set; }
        public ulong Size { get; set; }
        public DateTimeOffset DateModified { get; set; }
        public string Path { get; set; }

        public string DateModifiedString => DateModified.ToString("dd.MM.yyyy HH:MM");
    }

    public class Folder : FileSystemElement
    {
        public Folder(string name)
        {
            this.Name = name;
        }

        public Folder Parent { get; set; }

        public List<FileSystemElement> Children { get; set; }
    }

    public class File : FileSystemElement
    {
        public File(string name, ulong size, DateTimeOffset dateModified)
        {
            this.Name = name;
            this.Size = size;
            this.DateModified = dateModified;
        }

        public ulong Size { get; set; }
        public string Type { get; set; }
        public DateTimeOffset DateModified { get; set; }
    }
}
