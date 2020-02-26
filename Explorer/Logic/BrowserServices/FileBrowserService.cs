using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Explorer.Entities;
using Explorer.Logic;
using Explorer.Logic.FileSystemService;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using static Explorer.Logic.FileSystemRetrieveService;

namespace Explorer.Models
{
    public class FileBrowserService : IBrowserService
    {
        protected readonly FileSystemRetrieveService retrieveService;
        protected readonly FileSystemOperationService operationSerivce;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        public Features Features => FeatureBuilder.All;

        public FileBrowserService(ObservableCollection<FileSystemElement> elements)
        {
            FileSystemElements = elements;
            retrieveService = new FileSystemRetrieveService(FileSystemElements, Window.Current.CoreWindow.Dispatcher);
            operationSerivce = FileSystemOperationService.Instance;
        }

        public async void RefetchThumbnails(ThumbnailFetchOptions thumbnailOptions)
        {
            await retrieveService.RefetchThumbnails(thumbnailOptions);
        }

        

        public void CancelLoading()
        {
            retrieveService.CancelLoading();
        }

        public async void LoadFolder(FileSystemElement fse, ThumbnailFetchOptions thumbnailOptions)
        {
            await retrieveService.LoadFolderAsync(fse.Path, thumbnailOptions);
        }

        public async void SearchAsync(string search)
        {
            await retrieveService.SearchFolder(search);
        }

        public async void CreateFile(string fileName)
        {
            await retrieveService.CreateFile(fileName);
        }

        public async void CreateFolder(string folderName)
        {
            await retrieveService.CreateFolder(folderName);
        }

        public virtual async void DeleteFileSystemElement(FileSystemElement fse, bool permanently = false)
        {
            await retrieveService.DeleteFileSystemElement(fse, permanently);
        }

        public async void OpenFileSystemElement(FileSystemElement fse)
        {
            if (fse.DisplayType == "Application") await FileSystem.LaunchExeAsync(fse.Path);
            else await FileSystem.OpenFileWithDefaultApp(fse.Path);
        }

        public async void OpenFileSystemElementWith(FileSystemElement fse)
        {
            var zfe = (ZipFileElement)fse;
            var file = await FileSystem.CreateStorageFile(ApplicationData.Current.TemporaryFolder, zfe.Name, zfe.ElementStream);

            await FileSystem.OpenFileWith(file.Path);
        }

        public async void RenameFileSystemElement(FileSystemElement fse, string newName)
        {
            await FileSystem.RenameStorageItemAsync(fse, newName);
        }

        public async void CopyFileSystemElement(Collection<FileSystemElement> files)
        {
            var dataPackage = new DataPackage();

            var itemsToCopy = await FileSystem.GetStorageItemsAsync(files);
            dataPackage.SetStorageItems(itemsToCopy);
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            Clipboard.SetContent(dataPackage);
            //Clipboard.Flush();
        }

        public async void CutFileSystemElement(Collection<FileSystemElement> files)
        {
            var dataPackage = new DataPackage();

            var itemsToCopy = await FileSystem.GetStorageItemsAsync(files);
            dataPackage.SetStorageItems(itemsToCopy);
            dataPackage.RequestedOperation = DataPackageOperation.Move;

            Clipboard.SetContent(dataPackage);
            //Clipboard.Flush();
        }

        public async void PasteFileSystemElement(FileSystemElement folder)
        {
            var data = Clipboard.GetContent();

            if (data.Contains(StandardDataFormats.StorageItems))
            {
                var items = await data.GetStorageItemsAsync();
                if (data.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                {
                    await operationSerivce.BeginCopyOperation(folder, items.ToList());
                    data.ReportOperationCompleted(DataPackageOperation.Copy);
                }
                else if (data.RequestedOperation.HasFlag(DataPackageOperation.Move))
                {
                    await operationSerivce.BeginMoveOperation(folder, items.ToList());
                    data.ReportOperationCompleted(DataPackageOperation.Move);
                }
            }
        }

        public async void DragStorageItems(DataPackage dataPackage, DragUI dragUI, Collection<FileSystemElement> draggedItems)
        {
            var fse = draggedItems[0];
            if (!fse.IsFolder)
            {
                var storageItem = await FileSystem.GetFileAsync(fse);
                dataPackage.SetStorageItems(new List<IStorageItem> { storageItem }, false);

                var ti = await storageItem.GetThumbnailAsync(ThumbnailMode.SingleItem, 30);
                if (ti != null)
                {
                    var stream = ti.CloneStream();
                    var img = new BitmapImage();

                    await img.SetSourceAsync(stream);

                    dataPackage.RequestedOperation = DataPackageOperation.Move;
                    dragUI.SetContentFromBitmapImage(img, new Point(-1, 0));

                    //args.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    //args.DragUI.SetContentFromDataPackage();
                }
            }
            else
            {
                var storageItem = await FileSystem.GetFolderAsync(fse);
                dataPackage.SetStorageItems(new List<IStorageItem> { storageItem }, false);

                var ti = await storageItem.GetThumbnailAsync(ThumbnailMode.SingleItem, 30);
                if (ti != null)
                {
                    var stream = ti.CloneStream();
                    var img = new BitmapImage();

                    await img.SetSourceAsync(stream);

                    dataPackage.RequestedOperation = DataPackageOperation.Move;
                    dragUI.SetContentFromBitmapImage(img, new Point(-1, 0));

                    //args.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    //args.DragUI.SetContentFromDataPackage();
                }
            }
        }

        public void DropStorageItems(FileSystemElement droppedTo, IEnumerable<IStorageItem> droppeditems)
        {
            throw new NotImplementedException();
        }
    }
}
