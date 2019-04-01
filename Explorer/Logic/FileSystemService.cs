using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace Explorer.Logic
{
    public class FileSystemService
    {
        public event EventHandler FolderContentChangedEvent;

        private string currentPath;

        private StorageItemQueryResult itemQuery;
        private StorageFolderQueryResult folderQuery;
        private StorageFileQueryResult fileQuery;

        private Task[] queryTasks;
        private Stopwatch s = new Stopwatch();
        private CancellationTokenSource cts;

        public ObservableCollection<FileSystemElement> Items;

        public FileSystemService()
        {
            queryTasks = new Task[2];
            Items = new ObservableCollection<FileSystemElement>();
        }

        private async Task<FileSystemElement[]> LoadFolderAsyncOld(string path)
        {
            s.Restart();

            if (currentPath == path) return await ReloadFolderAsync();
            return await SwitchFolderAsync(path);
        }

        public async Task LoadFolderAsync(string path)
        {
            s.Restart();

            cts?.Cancel();  //Cancel previously scheduled browse
            cts = new CancellationTokenSource();    //Create new cancel token for this request

            if (currentPath == path) await ReloadFolderAsync2(cts.Token);
            else await SwitchFolderAsync2(path, cts.Token);

            currentPath = path;
        }

        private async Task ReloadFolderAsync2(CancellationToken cancellationToken)
        {
            AddStorageItems(await itemQuery.GetItemsAsync(), cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async Task SwitchFolderAsync2(string path, CancellationToken cancellationToken)
        {
            var folder = await FileSystem.GetFolderAsync(path);
            itemQuery = folder.CreateItemQuery();

            AddStorageItems(await itemQuery.GetItemsAsync(), cancellationToken);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");
        }

        private async void AddStorageItems(IReadOnlyList<IStorageItem> items, CancellationToken cancellationToken)
        {
            Items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Items.Clear();
                    return;
                }

                var element = items[i];
                var props = await element.GetBasicPropertiesAsync();

                Items.Add(new FileSystemElement
                {
                    Name = element.Name,
                    Size = props.Size,
                    Type = element.Attributes,
                    DateModified = props.DateModified,
                    Path = element.Path,
                });
            }
        }

        public async Task<FileSystemElement[]> ReloadFolderAsync()
        {
            var folders = await folderQuery.GetFoldersAsync();
            var files = await fileQuery.GetFilesAsync();

            var result = new FileSystemElement[folders.Count + files.Count];
            queryTasks[0] = LoadFoldersAsync(result, 0);
            queryTasks[1] = LoadFilesAsync(result, folders.Count);
            await Task.WhenAll(queryTasks);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");

            return result;
        }

        public async Task<FileSystemElement[]> SwitchFolderAsync(string path)
        {
            var folder = await FileSystem.GetFolderAsync(path);

            folderQuery = folder.CreateFolderQuery();
            fileQuery = folder.CreateFileQuery();

            var folders = await folderQuery.GetFoldersAsync();
            var files = await fileQuery.GetFilesAsync();

            var result = new FileSystemElement[folders.Count + files.Count];
            queryTasks[0] = LoadFoldersAsync(result, 0);
            queryTasks[1] = LoadFilesAsync(result, folders.Count);
            await Task.WhenAll(queryTasks);

            s.Stop();
            Debug.WriteLine("Load took: " + s.ElapsedMilliseconds + "ms");

            return result;
        }

        private async Task LoadFoldersAsync(FileSystemElement[] arr, int startIndex)
        {
            var folders = await folderQuery.GetFoldersAsync();
            for (int i = 0; i < folders.Count; i++)
            {
                StorageFolder element = folders[i];
                var props = await element.GetBasicPropertiesAsync();

                arr[startIndex + i] = new FileSystemElement
                {
                    Name = element.Name,
                    Size = props.Size,
                    Type = element.Attributes,
                    DateModified = props.DateModified,
                    Path = element.Path,
                };
            }
        }

        private async Task LoadFilesAsync(FileSystemElement[] arr, int startIndex)
        {
            var files = await fileQuery.GetFilesAsync();

            for (int i = 0; i < files.Count; i++)
            {
                StorageFile element = files[i];
                var props = await element.GetBasicPropertiesAsync();

                arr[startIndex + i] = new FileSystemElement
                {
                    Name = element.Name,
                    Size = props.Size,
                    Type = element.Attributes,
                    DateModified = props.DateModified,
                    Path = element.Path,
                };
            }
        }
    }
}
