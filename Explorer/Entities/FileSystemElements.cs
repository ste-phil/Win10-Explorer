using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Explorer.Entities
{
    public class FileSystemElement : ObservableEntity
    {
        private string name;
        private string lowerName;
        private string path;
        private DateTimeOffset dateModified;
        private ulong size;
        private string displayType;
        private bool isFolder;
        private BitmapImage image;
        private string type;

        public FileSystemElement()
        {
            
        }

        /// <summary>
        /// Creates a folder
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <param name="dateModified"></param>
        /// <param name="size"></param>
        /// <param name="displayType"></param>
        /// <param name="isFolder"></param>
        /// <param name="image"></param>
        /// <param name="type"></param>
        public FileSystemElement(string name, string path, DateTimeOffset dateModified, ulong size)
        {
            IsFolder = true;
            Name = name;
            Path = path;
            DateModified = dateModified;
            Size = size;
            DisplayType = "Folder";
        }

        /// <summary>
        // Creates a file 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <param name="dateModified"></param>
        /// <param name="size"></param>
        /// <param name="displayType"></param>
        /// <param name="isFolder"></param>
        /// <param name="image"></param>
        /// <param name="type"></param>
        public FileSystemElement(string name, string path, DateTimeOffset dateModified, ulong size, BitmapImage image, string type, string displayType)
        {
            Name = name;
            Path = path;
            DateModified = dateModified;
            Size = size;
            Image = image;
            Type = type;
            DisplayType = displayType;
        }


        #region Properties
        public string DisplayType
        {
            get { return displayType; }
            set { displayType = value; OnPropertyChanged(); }
        }
        public string Type
        {
            get { return type; }
            set { type = value; OnPropertyChanged(); }
        }
        public ulong Size
        {
            get { return size; }
            set { size = value; OnPropertyChanged(); }
        }

        public DateTimeOffset DateModified
        {
            get { return dateModified; }
            set { dateModified = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get { return path; }
            set { path = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get { return name; }
            set { name = value; lowerName = value.ToLower(); OnPropertyChanged(); OnPropertyChanged("LowerName"); }
        }

        public BitmapImage Image
        {
            get { return image; }
            set { image = value; OnPropertyChanged(); }
        }

        public bool IsFolder
        {
            get { return isFolder; }
            set { isFolder = value; OnPropertyChanged(); }
        }

        public string DateModifiedString => DateModified.ToString("dd.MM.yyyy HH:MM");
        public string SizeString => GetReadableSize(Size);
        public Symbol Icon => IsFolder ? Symbol.Folder : Symbol.Document;
        public string LowerName => lowerName;

        public bool IsArchive => Type == ".zip" || Type == ".rar" || Type == ".GZip" || Type == ".tar.gz";
        #endregion

        private string GetReadableSize(double bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return $"{bytes:0.##} {sizes[order]}";
        }
    }
    
    public class ZipFileElement : FileSystemElement
    {
        private string elementKey;
        private int elementDepth;
        private Stream elementStream;

        /// <summary>
        /// Creates a folder
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <param name="dateModified"></param>
        /// <param name="size"></param>
        /// <param name="displayType"></param>
        /// <param name="isFolder"></param>
        /// <param name="image"></param>
        /// <param name="type"></param>
        public ZipFileElement(string name, string path, DateTimeOffset dateModified, ulong size, string elementKey, int elementDepth)
            :base(name, path, dateModified, size)
        {
            ElementKey = elementKey;
            ElementDepth = elementDepth;
        }

        /// <summary>
        // Creates a file 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <param name="dateModified"></param>
        /// <param name="size"></param>
        /// <param name="displayType"></param>
        /// <param name="isFolder"></param>
        /// <param name="image"></param>
        /// <param name="type"></param>
        public ZipFileElement(string name, string path, DateTimeOffset dateModified, ulong size, BitmapImage image, string type, string displayType, string elementKey, int elementDepth, Stream elementStream)
            : base(name, path, dateModified, size, image, type, displayType)
        {
            ElementKey = elementKey;
            ElementDepth = elementDepth;
            ElementStream = elementStream;
        }

        public Stream ElementStream
        {
            get { return elementStream; }
            set { elementStream = value; OnPropertyChanged(); }
        }

        public string ElementKey
        {
            get { return elementKey; }
            set { elementKey = value; OnPropertyChanged(); }
        }

        public int ElementDepth
        {
            get { return elementDepth; }
            set { elementDepth = value; OnPropertyChanged(); }
        }
    }

    public class Drive
    {
        public string Name { get; set; }
        public string DriveLetter { get; set; }
        public string RootDirectory { get; set; }
        public DriveType DriveType { get; set; }
        public ulong FreeSpace { get; set; }
        public ulong TotalSpace { get; set; }

        public string FreeSpaceString => GetReadableSize(FreeSpace);
        public string TotalSpaceString => GetReadableSize(TotalSpace);

        public double UsedSpacePercent => (TotalSpace - FreeSpace) / (double)TotalSpace * 100;

        public Brush SpaceColorBrush=> UsedSpacePercent >= 90 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.DeepSkyBlue);

        public Symbol Icon 
        {
            get
            {
                Symbol icon;
                switch (DriveType)
                {
                    case DriveType.Removable:
                        icon = (Symbol)0xE88E;
                        break;
                    case DriveType.CDRom:
                        icon = (Symbol)0xE958;
                        break;
                    case DriveType.Network:
                        icon = (Symbol)0xE969;
                        break;
                    default:
                        icon = (Symbol)0xEDA2;
                        break;
                }
                return icon;
            }
        }

        public SymbolIcon SymbolIcon
        {
            get
            {
                SymbolIcon icon;
                switch (DriveType)
                {
                    case DriveType.Removable:
                        icon = new SymbolIcon((Symbol)0xE88E);
                        break;
                    case DriveType.CDRom:
                        icon = new SymbolIcon((Symbol)0xE958);
                        break;
                    case DriveType.Network:
                        icon = new SymbolIcon((Symbol)0xE969);
                        break;
                    default:
                        icon = new SymbolIcon((Symbol)0xEDA2);
                        break;
                }
                return icon;
            }
        }

        private string GetReadableSize(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes = bytes / 1024;
            }

            return $"{bytes:0.##} {sizes[order]}";
        }
    }
}
