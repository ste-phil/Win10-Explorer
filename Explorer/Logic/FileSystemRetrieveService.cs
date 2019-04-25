using Explorer.Entities;
using Explorer.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
    public class FileSystemRetrieveService
    {
        private const int FILE_RETRIEVE_STEPSIZE = 200;
        private readonly CoreDispatcher dispatcher;

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
        private CancellationTokenSource browseCts;
        private CancellationTokenSource searchCts;

        private bool refreshRunning;
        private ThumbnailFetchOptions thumbnailOptions;
        private string currentSearch;

        private int folderCount;
        private List<StorageFile> files;
        private List<FileSystemElement> items;

        public ObservableCollection<FileSystemElement> ViewItems { get; set; }
        //public ObservableDictionary<string, FileSystemElement> ViewItems { get; set; }

        public FileSystemRetrieveService(CoreDispatcher dispatcher)
        {
            ViewItems = new ObservableRangeCollection<FileSystemElement>();
            //ViewItems = new ObservableDictionary<string, FileSystemElement>();
            items = new List<FileSystemElement>();
            files = new List<StorageFile>();

            currentSearch = "";

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
            this.dispatcher = dispatcher;
            //folderQueryOptions.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, props);
        }

        public async Task LoadFolderAsync(string path, ThumbnailFetchOptions thumbnailOptions = default)
        {
            this.thumbnailOptions = thumbnailOptions;

            s.Restart();

            browseCts?.Cancel();  //Cancel previously scheduled browse
            browseCts = new CancellationTokenSource();    //Create new cancel token for this request

            if (currentPath == path) await ReloadFolderAsync(path, browseCts.Token);
            else await SwitchFolderAsync(path, browseCts.Token);
        }

        private async Task ReloadFolderAsync(string path, CancellationToken cancellationToken)
        {
            currentPath = path;

            folderQuery.ApplyNewQueryOptions(folderQueryOptions);
            fileQuery.ApplyNewQueryOptions(fileQueryOptions);

            Clear();
            await LoadFoldersAsync(cancellationToken);
            await LoadFilesAsync(cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        public void Clear()
        {
            folderCount = 0;
            ViewItems.Clear();
            files.Clear();
            items.Clear();
        }

        public async Task RefetchThumbnails(ThumbnailFetchOptions thumbnailOptions = default)
        {
            this.thumbnailOptions = thumbnailOptions;

            for (int i = folderCount; i < files.Count; i++)
            {
                if (browseCts.Token.IsCancellationRequested) break;

                var ti = await files[i].GetThumbnailAsync(thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
                if (ti != null)
                {
                    try
                    {
                        await ViewItems[i].Image.SetSourceAsync(ti.CloneStream());
                    }
                    catch (Exception) { /*Supress Task canceled exception*/ }
                }
            }
        }

        /// <summary>
        /// Deletes passed FileSystemElement and removes it from the list
        /// </summary>
        /// <param name="fse"></param>
        /// <returns></returns>
        public async Task DeleteFileSystemElement(FileSystemElement fse)
        {
            try
            {
                await FileSystem.DeleteStorageItemAsync(fse);

                ViewItems.Remove(fse);
                if (!fse.IsFolder) files.Remove(files.First(s => s.Path == fse.Path));
            }
            catch (Exception) { /*e.g. NotFound / Unauthorized Exception*/}
        }

        public async Task SearchFolder(string search)
        {
            s.Restart();

            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();

            //Lower search input to find not capitalized results
            search = search.ToLower();

            //Deep search
            if (Regex.Match(search, @"^d\s{1}").Success) await Task.Run(() => SearchFolderDeepAsync(search, searchCts.Token));
            //Shallow search
            else
            {
                //If no input found reset search
                if (search == "") RestoreItems();
                //When the user adds one character to the existing search
                else if (search.Length > currentSearch.Length && search.Contains(currentSearch)) await Task.Run(() => LimitSearchFolderShallowAsync(search, searchCts.Token));
                //When the user remove one character from the existing search
                else if (search.Length < currentSearch.Length && currentSearch.Contains(search)) await Task.Run(() => ExpandSearchFolderShallowAsync(search, searchCts.Token));
                //If the use pastes a completely new search
                else
                {
                    ViewItems.Clear();
                    await ExpandSearchFolderShallowAsync(search, searchCts.Token);
                }
            }
            

            currentSearch = search;

            //else if (Regex.Match(search, @"^d\s{1}").Success) await SearchFolderDeep(search, searchCts.Token);
            //else await SearchFolderShallow(search, searchCts.Token);

            s.Stop();
            Debug.WriteLine("******Search took: " + s.ElapsedMilliseconds + "ms");
            Debug.WriteLine("----");
        }

        private async Task LimitSearchFolderShallowAsync(string search, CancellationToken token)
        {
            var s2 = new Stopwatch();
            s2.Restart();
            for (int i = ViewItems.Count - 1; i >= 0; i--)
            {
                if (token.IsCancellationRequested) return;
                if (!ViewItems[i].LowerName.Contains(search))
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Low, () => {
                        if (i < ViewItems.Count)
                            ViewItems.RemoveAt(i);
                    });
                }
            }

            s2.Stop();
            Debug.WriteLine("*LimitSearch took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task ExpandSearchFolderShallowAsync(string search, CancellationToken token)
        {
            var s2 = new Stopwatch();
            s2.Restart();
            for (int i = 0; i < items.Count; i++)
            {
                FileSystemElement fse = items[i];
                if (token.IsCancellationRequested) return;
                if (fse.LowerName.Contains(search) && !ViewItems.Contains(fse)) await dispatcher.RunAsync(CoreDispatcherPriority.Low, () => ViewItems.Add(fse)); 
            }

            s2.Stop();
            Debug.WriteLine("ExpandSearch took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task SearchFolderDeepAsync(string search, CancellationToken token)
        {

        }

        #region Internal Methods

        private void RestoreItems()
        {
            ViewItems.Clear();
            foreach (FileSystemElement fse in items)
            {
                ViewItems.Add(fse);
            }
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

            Clear();
            await LoadFoldersAsync(cancellationToken);
            await LoadFilesAsync(cancellationToken);

            //var folders = await folderQuery.GetFoldersAsync();
            //var files = await fileQuery.GetFilesAsync();

            //await AddFoldersAsync(folders, cancellationToken);
            //await AddFilesAsync(files, cancellationToken);

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
                await AddDifference(items, browseCts.Token);
            });

            refreshRunning = false;
        }

        private async Task AddDifference(IReadOnlyList<IStorageItem> newList, CancellationToken cancellationToken)
        {
            var currentFiles = ViewItems.Where(i => !i.IsFolder).ToList();
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

                    var fileToRemove = currentFiles[i];
                    ViewItems.Remove(fileToRemove);
                    files.Remove(files.First(f => f.Path == fileToRemove.Path));
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

        private async Task LoadFoldersAsync(CancellationToken cancellationToken)
        {
            uint si = 0;
            IReadOnlyList<StorageFolder> folders;
            do
            {
                if (cancellationToken.IsCancellationRequested) return;

                folders = await folderQuery.GetFoldersAsync(si, FILE_RETRIEVE_STEPSIZE);
                await AddFoldersAsync(folders, cancellationToken);
                si += FILE_RETRIEVE_STEPSIZE;
            } while (folders.Count != 0);
        }

        private async Task LoadFilesAsync(CancellationToken cancellationToken)
        {
            uint si = 0;
            IReadOnlyList<StorageFile> files;
            do
            {
                if (cancellationToken.IsCancellationRequested) return;

                files = await fileQuery.GetFilesAsync(si, FILE_RETRIEVE_STEPSIZE);
                await AddFilesAsync(files, cancellationToken);
                si += FILE_RETRIEVE_STEPSIZE;
            } while (files.Count != 0);
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
                _ = image.SetSourceAsync(ti.CloneStream());
            }

            var fse = new FileSystemElement(item.Name, item.Path, basicProps.DateModified, basicProps.Size, image, item.DisplayType, item.FileType);
            if (currentSearch == "") ViewItems.Add(fse);

            items.Add(fse);
            files.Add(item);
        }

        private async Task AddFolderAsync(StorageFolder item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();

            var fse = new FileSystemElement(item.Name, item.Path, basicProps.DateModified, basicProps.Size);
            if (currentSearch == "") ViewItems.Add(fse);

            items.Add(fse);
            folderCount++;
        }
        #endregion
    }
}
