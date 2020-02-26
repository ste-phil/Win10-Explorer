using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using static Explorer.Logic.FileSystemRetrieveService;

namespace Explorer.Models
{
    [Flags]
    public enum Features
    {
        None = 0,
        Copy = 1,
        Cut = 2,
        Paste = 4,
        Delete = 8,
        Rename = 16,
        Favorite = 32,
        History = 64,
        Properties = 128,
        Open = 256,
        Share = 512,
        Search = 1024,
    }

    public static class FeatureBuilder
    {
        public static Features All => (Features)2047;
    }

    public interface IBrowserService
    {
        ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        Features Features { get; }

        void OpenFileSystemElement(FileSystemElement fse);
        void RefetchThumbnails(ThumbnailFetchOptions thumbnailOptions);
        void CancelLoading();
        void LoadFolder(FileSystemElement fse, ThumbnailFetchOptions thumbnailOptions);
        void SearchAsync(string search);
        
        void RenameFileSystemElement(FileSystemElement fse, string newName);
        void DeleteFileSystemElement(FileSystemElement fse);
        void CreateFolder(string folderName);
        void CreateFile(string fileName);

        void CopyFileSystemElement(Collection<FileSystemElement> files);
        void CutFileSystemElement(Collection<FileSystemElement> files);
        void PasteFileSystemElement(FileSystemElement currentFolder);
        void DragStorageItems(DataPackage dataPackage, DragUI dragUI, Collection<FileSystemElement> draggedItems);
        void DropStorageItems(FileSystemElement droppedTo, IEnumerable<IStorageItem> droppeditems);
    }
}
