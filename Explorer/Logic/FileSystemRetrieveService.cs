using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace Explorer.Logic
{
    public class FileSystemRetrieveService
    {
        public class ThumbnailFetchOptions
        {
            public ThumbnailMode Mode { get; set; } = ThumbnailMode.ListView;
            public uint Size { get; set; } = 20;
            public ThumbnailOptions Scale { get; set; }
        }

        private string currentPath;

        private StorageFolderQueryResult folderQuery;
        private StorageFileQueryResult fileQuery;

        private QueryOptions folderQueryOptions;
        private QueryOptions fileQueryOptions;

        private Stopwatch s = new Stopwatch();
        private CancellationTokenSource cts;

        private bool refreshRunning;
        private ThumbnailFetchOptions thumbnailOptions;

        private int loadedFolderCount;
        private List<StorageFile> loadedFiles;

        public ObservableCollection<FileSystemElement> Items { get; set; }

        public FileSystemRetrieveService()
        {
            Items = new ObservableCollection<FileSystemElement>();
            loadedFiles = new List<StorageFile>();

            fileQueryOptions = new QueryOptions();
            folderQueryOptions = new QueryOptions();

            var props = new string[]
            {
                "System.DateModified",
                "System.ContentType",
                "System.Size",
                "System.FileExtension",
                //"System.FolderNameDisplay",
                //"System.ItemNameDisplay"
            };

            fileQueryOptions.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, props);
            //folderQueryOptions.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, props);
        }

        public async Task LoadFolderAsync(string path, ThumbnailFetchOptions thumbnailOptions = default)
        {
            this.thumbnailOptions = thumbnailOptions;

            s.Restart();

            cts?.Cancel();  //Cancel previously scheduled browse
            cts = new CancellationTokenSource();    //Create new cancel token for this request

            if (currentPath == path) await ReloadFolderAsync(path, cts.Token);
            else await SwitchFolderAsync(path, cts.Token);
        }

        public async Task RefetchThumbnails(ThumbnailFetchOptions thumbnailOptions = default)
        {
            this.thumbnailOptions = thumbnailOptions;

            for (int i = loadedFolderCount; i < loadedFiles.Count; i++)
            {
                if (cts.Token.IsCancellationRequested) break;

                var ti = await loadedFiles[i].GetThumbnailAsync(thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
                if (ti != null)
                {
                    try
                    {
                        await Items[i].Image.SetSourceAsync(ti.CloneStream());
                    } catch(Exception) { /*Supress Task canceled exception*/ }
                }
            }
        }

        public void Clear()
        {
            loadedFolderCount = 0;
            Items.Clear();
            loadedFiles.Clear();
        }

        private async Task ReloadFolderAsync(string path, CancellationToken cancellationToken)
        {
            currentPath = path;

            folderQuery.ApplyNewQueryOptions(folderQueryOptions);
            fileQuery.ApplyNewQueryOptions(fileQueryOptions);

            var folders = await folderQuery.GetFoldersAsync();
            var files = await fileQuery.GetFilesAsync();

            Clear();
            await AddFoldersAsync(folders, cancellationToken);
            await AddFilesAsync(files, cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task SwitchFolderAsync(string path, CancellationToken cancellationToken)
        {
            currentPath = path;

            var folder = await FileSystem.GetFolderAsync(path);
            var indexedState = await folder.GetIndexedStateAsync();

            fileQueryOptions.SetThumbnailPrefetch(thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
            if (indexedState == IndexedState.FullyIndexed)
            {
                fileQueryOptions.IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties;
                folderQueryOptions.IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties;
            }
            else
            {
                fileQueryOptions.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
                folderQueryOptions.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            }

            folderQuery = folder.CreateFolderQueryWithOptions(folderQueryOptions);
            fileQuery = folder.CreateFileQueryWithOptions(fileQueryOptions);

            var folders = await folderQuery.GetFoldersAsync();
            var files = await fileQuery.GetFilesAsync();

            Clear();
            await AddFoldersAsync(folders, cancellationToken);
            await AddFilesAsync(files, cancellationToken);

            fileQuery.ContentsChanged += ItemQuery_ContentsChanged;

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async void ItemQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (refreshRunning) return;
            refreshRunning = true;

            //itemQuery.ApplyNewQueryOptions(new QueryOptions());
            var items = await fileQuery.GetFilesAsync();

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await AddDifference(items, cts.Token);
            });

            refreshRunning = false;
        }

        private async Task AddDifference(IReadOnlyList<IStorageItem> newList, CancellationToken cancellationToken)
        {
            var currentFiles = Items.Where(i => !i.IsFolder).ToList();
            for (int i = 0; i < currentFiles.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < newList.Count; j++)
                {
                    if (currentFiles[i].Name == newList[j].Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    Items.Remove(currentFiles[i]);
                }
            }

            for (int i = 0; i < newList.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < currentFiles.Count; j++)
                {
                    if (currentFiles[j].Name == newList[i].Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    await AddStorageItemAsync(newList[i]);
                }
            }

        }

        private async Task AddFoldersAsync(IReadOnlyList<StorageFolder> items, CancellationToken cancellationToken)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Clear();
                    break;
                }

                await AddFolderAsync(items[i]);
            }
        }

        private async Task AddFilesAsync(IReadOnlyList<StorageFile> items, CancellationToken cancellationToken)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Clear();
                    break;
                }

                await AddFileAsync(items[i]);
            }
        }

        private async Task AddStorageItemsAsync(IReadOnlyList<IStorageItem> items, CancellationToken cancellationToken)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Clear();
                    break;
                }

                await AddStorageItemAsync(items[i]);
            }
        }

        private async Task AddStorageItemAsync(IStorageItem item)
        {
            if (item is StorageFile file) await AddFileAsync(file);
            else if (item is StorageFolder folder) await AddFolderAsync(folder);
        }


        private async Task AddFileAsync(StorageFile item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();
            var image = new BitmapImage();

            var ti = await item.GetThumbnailAsync(thumbnailOptions.Mode, thumbnailOptions.Size, ThumbnailOptions.UseCurrentScale);
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

            loadedFiles.Add(item);
        }

        private async Task AddFolderAsync(StorageFolder item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();

            Items.Add(new FileSystemElement
            {
                IsFolder = true,
                Name = item.Name,
                Size = basicProps.Size,
                DisplayType = "Folder",
                DateModified = basicProps.DateModified,
                Path = item.Path
            });

            loadedFolderCount++;
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
