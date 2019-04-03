using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace Explorer.Logic
{
    public class FileSystemService
    {
        public event EventHandler FolderContentChangedEvent;

        private string currentPath;

        private StorageItemQueryResult itemQuery;
        //private StorageFolderQueryResult folderQuery;
        //private StorageFileQueryResult fileQuery;

        private Stopwatch s = new Stopwatch();
        private CancellationTokenSource cts;

        private bool refreshRunning;

        public ObservableCollection<FileSystemElement> Items;

        public FileSystemService()
        {
            Items = new ObservableCollection<FileSystemElement>();
        }

        public async Task LoadFolderAsync(string path)
        {
            s.Restart();

            cts?.Cancel();  //Cancel previously scheduled browse
            cts = new CancellationTokenSource();    //Create new cancel token for this request

            if (currentPath == path) await ReloadFolderAsync(cts.Token);
            else await SwitchFolderAsync(path, cts.Token);

            currentPath = path;
        }

        private async Task ReloadFolderAsync(CancellationToken cancellationToken)
        {
            AddStorageItems(await itemQuery.GetItemsAsync(), cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task SwitchFolderAsync(string path, CancellationToken cancellationToken)
        {
            var folder = await FileSystem.GetFolderAsync(path);
            var indexedState = await folder.GetIndexedStateAsync();

            var queryOptions = new QueryOptions();
            queryOptions.SetThumbnailPrefetch(ThumbnailMode.ListView, 20, ThumbnailOptions.UseCurrentScale);
            queryOptions.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties,
                new string[] { "System.DateModified", "System.ContentType", "System.Size", "System.FileExtension" });
            if (indexedState == IndexedState.FullyIndexed)
            {
                queryOptions.IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties;
            }
            else
            {
                queryOptions.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            }
            itemQuery = folder.CreateItemQueryWithOptions(queryOptions);

            AddStorageItems(await itemQuery.GetItemsAsync(), cancellationToken);
            itemQuery.ContentsChanged += ItemQuery_ContentsChanged;

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async void ItemQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (refreshRunning) return;
            refreshRunning = true;

            //itemQuery.ApplyNewQueryOptions(new QueryOptions());
            var items = await itemQuery.GetItemsAsync();

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                AddDifference(items, cts.Token);
            });

            refreshRunning = false;
        }

        private void AddDifference(IReadOnlyList<IStorageItem> newList, CancellationToken cancellationToken)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < newList.Count; j++)
                {
                    if (Items[i].Name == newList[j].Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    Items.Remove(Items[i]);
                }
            }

            for (int i = 0; i < newList.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < Items.Count; j++)
                {
                    if (Items[j].Name == newList[i].Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    AddStorageItem(newList[i]);
                }
            }

        } 

        private void AddStorageItems(IReadOnlyList<IStorageItem> items, CancellationToken cancellationToken)
        {
            Items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Items.Clear();
                    return;
                }

                AddStorageItem(items[i]);
            }
        }

        private async void AddStorageItem(IStorageItem item)
        {
            if (item is StorageFile file) await AddFileAsync(file);
            else if (item is StorageFolder folder) await AddFolderAsync(folder);
        }

        private async Task AddFileAsync(StorageFile item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();
            var image = new BitmapImage();
            var ti = await item.GetThumbnailAsync(ThumbnailMode.ListView, 30, ThumbnailOptions.ResizeThumbnail);
            if (ti != null)
            {
                await image.SetSourceAsync(ti.CloneStream());
            }

            Items.Add(new FileSystemElement
            {
                IsFolder = false,
                Name = item.Name,
                Size = basicProps.Size,
                DisplayType = item.DisplayType,
                Type = item.FileType,
                DateModified = basicProps.DateModified,
                Path = item.Path,
                Image = image
            });
        }

        private async Task AddFolderAsync(StorageFolder item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();
            var image = new BitmapImage();
            var ti = await item.GetThumbnailAsync(ThumbnailMode.ListView, 30, ThumbnailOptions.ResizeThumbnail);
            if (ti != null)
            {
                await image.SetSourceAsync(ti.CloneStream());
            }

            Items.Add(new FileSystemElement
            {
                IsFolder = true,
                Name = item.Name,
                Size = basicProps.Size,
                DisplayType = "Folder",
                DateModified = basicProps.DateModified,
                Path = item.Path,
                Image = image
            });
        }

        //public async Task<FileSystemElement[]> ReloadFolderAsync()
        //{
        //    var folders = await folderQuery.GetFoldersAsync();
        //    var files = await fileQuery.GetFilesAsync();

        //    var result = new FileSystemElement[folders.Count + files.Count];
        //    queryTasks[0] = LoadFoldersAsync(result, 0);
        //    queryTasks[1] = LoadFilesAsync(result, folders.Count);
        //    await Task.WhenAll(queryTasks);

        //    s.Stop();
        //    Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");

        //    return result;
        //}

        //public async Task<FileSystemElement[]> SwitchFolderAsync(string path)
        //{
        //    var folder = await FileSystem.GetFolderAsync(path);

        //    folderQuery = folder.CreateFolderQuery();
        //    fileQuery = folder.CreateFileQuery();

        //    var folders = await folderQuery.GetFoldersAsync();
        //    var files = await fileQuery.GetFilesAsync();

        //    var result = new FileSystemElement[folders.Count + files.Count];
        //    queryTasks[0] = LoadFoldersAsync(result, 0);
        //    queryTasks[1] = LoadFilesAsync(result, folders.Count);
        //    await Task.WhenAll(queryTasks);

        //    s.Stop();
        //    Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");

        //    return result;
        //}

        //private async Task LoadFoldersAsync(FileSystemElement[] arr, int startIndex)
        //{
        //    var folders = await folderQuery.GetFoldersAsync();
        //    for (int i = 0; i < folders.Count; i++)
        //    {
        //        StorageFolder element = folders[i];
        //        var props = await element.GetBasicPropertiesAsync();

        //        arr[startIndex + i] = new FileSystemElement
        //        {
        //            Name = element.Name,
        //            Size = props.Size,
        //            Type = element.Attributes,
        //            DateModified = props.DateModified,
        //            Path = element.Path,
        //        };
        //    }
        //}

        //private async Task LoadFilesAsync(FileSystemElement[] arr, int startIndex)
        //{
        //    var files = await fileQuery.GetFilesAsync();

        //    for (int i = 0; i < files.Count; i++)
        //    {
        //        StorageFile element = files[i];
        //        var props = await element.GetBasicPropertiesAsync();

        //        arr[startIndex + i] = new FileSystemElement
        //        {
        //            Name = element.Name,
        //            Size = props.Size,
        //            Type = element.Attributes,
        //            DateModified = props.DateModified,
        //            Path = element.Path,
        //        };
        //    }
        //}
    }
}
