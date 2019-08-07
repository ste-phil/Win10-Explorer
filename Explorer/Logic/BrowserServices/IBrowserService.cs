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
    public interface IBrowserService
    {
        ObservableCollection<FileSystemElement> FileSystemElements { get; set; }

        void OpenFileSystemElement(FileSystemElement fse);
        void RefetchThumbnails(ThumbnailFetchOptions thumbnailOptions);
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
