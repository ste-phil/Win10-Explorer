using Explorer.Entities;
using Explorer.Logic.FileSystemService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private StorageFolder currentFolder;

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
        private bool deepSearchRunning;

        private int folderCount;
        private List<StorageFile> files;
        private List<FileSystemElement> items;

        public ObservableCollection<FileSystemElement> ViewItems { get; set; }

        public FileSystemRetrieveService(ObservableCollection<FileSystemElement> viewItems, CoreDispatcher dispatcher)
        {
            ViewItems = viewItems;
            items = new List<FileSystemElement>();
            files = new List<StorageFile>();

            currentSearch = "";
            this.dispatcher = dispatcher;

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
            if (thumbnailOptions == default) this.thumbnailOptions = new ThumbnailFetchOptions();
            else this.thumbnailOptions = thumbnailOptions;

            s.Restart();

            browseCts?.Cancel();                          //Cancel previously scheduled browse
            browseCts = new CancellationTokenSource();    //Create new cancel token for this request
            deepSearchRunning = false;                    //Current load might be a deep search so stop it

            folderQueryOptions.FolderDepth = FolderDepth.Shallow;  
            fileQueryOptions.FolderDepth = FolderDepth.Shallow;

            if (currentPath == path) await ReloadFolderAsync(path, browseCts.Token);
            else await SwitchFolderAsync(path, browseCts.Token);
        }

        public async Task LoadFolderDeepAsync(string path, ThumbnailFetchOptions thumbnailOptions = default)
        {
            if (thumbnailOptions == default) this.thumbnailOptions = new ThumbnailFetchOptions();
            else this.thumbnailOptions = thumbnailOptions;

            s.Restart();

            browseCts?.Cancel();                          //Cancel previously scheduled browse
            browseCts = new CancellationTokenSource();    //Create new cancel token for this request

            folderQueryOptions.FolderDepth = FolderDepth.Deep;      //Request any subfolders
            fileQueryOptions.FolderDepth = FolderDepth.Deep;        //Request any files in subfolders

            deepSearchRunning = true;
            if (currentPath == path) await ReloadFolderAsync(path, browseCts.Token);
            else await SwitchFolderAsync(path, browseCts.Token);
        }

        public void CancelLoading()
        {
            browseCts?.Cancel();
            s.Stop();
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

            for (int i = 0; i < files.Count; i++)
            {
                if (browseCts.Token.IsCancellationRequested) break;

                var ti = await files[i].GetThumbnailAsync(thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
                if (ti != null)
                {
                    try
                    {
                        await ViewItems[folderCount + i].Image.SetSourceAsync(ti.CloneStream());
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
                items.Remove(fse);
                if (!fse.IsFolder) files.Remove(files.First(s => s.Path == fse.Path));
            }
            catch (Exception e) {
                /*e.g. NotFound / Unauthorized Exception*/
                if (e is FileNotFoundException)
                {
                    ViewItems.Remove(fse);
                    items.Remove(fse);
                    if (!fse.IsFolder) files.Remove(files.First(s => s.Path == fse.Path));
                }
            }
        }

        public async Task<bool> CreateFolder(string name)
        {
            try
            {
                var folder = await FileSystem.CreateFolder(currentFolder, name);
                await AddFolderAsync(folder);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CreateFile(string name)
        {
            try
            {
                var file = await FileSystem.CreateOrOpenFileAsync(currentFolder, name);
                await AddFileAsync(file);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SearchFolder(string search)
        {
            s.Restart();

            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();

            //Lower search input to find not capitalized results
            var oldSearch = currentSearch;
            currentSearch = search.ToLower();

            //Begin deep search when user puts d_* and if its not already running
            if (Regex.Match(currentSearch, @"^d\s{1}").Success)
            {
                currentSearch = currentSearch.Substring(2, currentSearch.Length - 2);
                if (!deepSearchRunning) _ = LoadFolderDeepAsync(currentPath);
            }

            //Searchs currently loaded items
            //If no input found reset search
            if (currentSearch == "") RestoreItems();
            //When the user adds one character to the existing search
            else if (currentSearch.Length > oldSearch.Length && currentSearch.Contains(oldSearch)) await Task.Run(() => LimitSearchFolderShallowAsync(currentSearch, searchCts.Token));
            //When the user remove one character from the existing search
            else if (currentSearch.Length < oldSearch.Length && oldSearch.Contains(currentSearch)) await Task.Run(() => ExpandSearchFolderShallowAsync(currentSearch, searchCts.Token));
            //If the use pastes a completely new search
            else
            {
                ViewItems.Clear();
                await ExpandSearchFolderShallowAsync(currentSearch, searchCts.Token);
            }
            

            s.Stop();
            Debug.WriteLine("******Search took: " + s.ElapsedMilliseconds + "ms");
            Debug.WriteLine("----");
        }



        #region Internal Methods

        #region Search
        private void RestoreItems()
        {
            ViewItems.Clear();
            foreach (FileSystemElement fse in items)
            {
                ViewItems.Add(fse);
            }
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
        #endregion

        private async Task ReloadFolderAsync(string path, CancellationToken cancellationToken)
        {
            //currentPath = path;

            folderQuery.ApplyNewQueryOptions(folderQueryOptions);
            fileQuery.ApplyNewQueryOptions(fileQueryOptions);

            Clear();
            await LoadFoldersAsync(cancellationToken);
            await LoadFilesAsync(cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task SwitchFolderAsync(string path, CancellationToken cancellationToken)
        {
            currentPath = path;

            currentFolder = await FileSystem.GetFolderAsync(path);
            var indexedState = await currentFolder.GetIndexedStateAsync();

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

            folderQuery = currentFolder.CreateFolderQueryWithOptions(folderQueryOptions);
            fileQuery = currentFolder.CreateFileQueryWithOptions(fileQueryOptions);

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
                    files.Remove(files.FirstOrDefault(f => f.Path == fileToRemove.Path));
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

            BitmapImage image = null;
            var ti = await item.GetThumbnailAsync(thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
            if (ti != null)
            {
                image = new BitmapImage();
                _ = image.SetSourceAsync(ti.CloneStream());
            }

            var fse = new FileSystemElement(item.Name, item.Path, basicProps.DateModified, basicProps.Size, image, item.FileType, item.DisplayType);

            //If there is a search going on check if the items fits the search
            if (currentSearch != "" && fse.LowerName.Contains(currentSearch)) ViewItems.Add(fse);
            else if (currentSearch == "") ViewItems.Add(fse);

            items.Add(fse);
            files.Add(item);
        }

        private async Task AddFolderAsync(StorageFolder item)
        {
            var basicProps = await item.GetBasicPropertiesAsync();

            var fse = new FileSystemElement(item.Name, item.Path, basicProps.DateModified, basicProps.Size);

            //If there is a search going on check if the items fits the search
            if (currentSearch != "" && fse.LowerName.Contains(currentSearch)) ViewItems.Add(fse);
            else if (currentSearch == "") ViewItems.Add(fse);

            items.Add(fse);
            folderCount++;
        }
        #endregion
    }
}
