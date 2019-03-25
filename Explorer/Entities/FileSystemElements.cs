using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Explorer.Entities
{
    public class FileSystemElement
    {
        public Windows.Storage.FileAttributes Type { get; set; }
        public string Name { get; set; }
        public ulong Size { get; set; }
        public DateTimeOffset DateModified { get; set; }
        public string Path { get; set; }

        public string DateModifiedString => DateModified.ToString("dd.MM.yyyy HH:MM");
        public bool IsFolder => Type.HasFlag(Windows.Storage.FileAttributes.Directory);
        public string SizeString => GetReadableSize(Size);
        public Symbol Icon => IsFolder ? Symbol.Folder : Symbol.Document;

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
