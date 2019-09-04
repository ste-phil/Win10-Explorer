using Explorer.Entities;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace Explorer.Logic.FileSystemService
{
    public static partial class FileSystem
    {

        #region Open
        public static async Task<bool> StorageItemExists(StorageFolder folder, string name)
        {
            return (await folder.TryGetItemAsync(name)) != null;
        }

        public static async void OpenFileWithDefaultApp(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            await Launcher.LaunchFileAsync(file);
        }

        public static async void OpenFileWith(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            await Launcher.LaunchFileAsync(file, new LauncherOptions { DisplayApplicationPicker = true });
        }
        #endregion

        #region Create
        public static async Task<StorageFile> CreateOrOpenFileAsync(StorageFolder folder, string name, CreationCollisionOption option = CreationCollisionOption.OpenIfExists)
        {
            return await folder.CreateFileAsync(name, option);
        }

        public static async Task<StorageFile> CreateStorageFile(StorageFolder folder, string name, Stream stream)
        {
            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);

            stream.Position = 0;
            using (var writeStream = await file.OpenStreamForWriteAsync())
            {
                await stream.CopyToAsync(writeStream);
                await writeStream.FlushAsync();
            }

            return file;
        }

        public static async Task<StorageFolder> CreateFolder(StorageFolder folder, string folderName, CreationCollisionOption option = CreationCollisionOption.GenerateUniqueName)
        {
            return await folder.CreateFolderAsync(folderName, option);
        }
        #endregion

        #region Delete

        public static async Task DeleteStorageItemAsync(FileSystemElement fse, bool permanently = false)
        {
            HistoryService.Instance.AddDeleteOperation(fse);

            var storageItem = await GetStorageItemAsync(fse);
            if (permanently) await DeleteStorageItemAsync(storageItem);
            else await FileSystemOperationService.Instance.BeginMoveOperation(new FileSystemElement { Path = RecyclingBinFolder.Path }, storageItem);
        }

        public static async Task DeleteFolderAsync(string path)
        {
            var si = await GetFolderAsync(path);
            await si.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public static async Task DeleteFileAsync(string path)
        {
            var si = await GetFileAsync(path);
            await si.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public static async Task DeleteStorageItemAsync(IStorageItem storageItem)
        {
            await storageItem.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public static async Task DeleteStorageItemAsync(string path, bool folder)
        {
            if (folder) await DeleteFolderAsync(path);
            else await DeleteFileAsync(path);
        }

        
        #endregion

        public static async Task RenameStorageItemAsync(FileSystemElement fse, string newName)
        {
            try
            {
                var file = await GetStorageItemAsync(fse);
                await file.RenameAsync(newName);
            }
            catch(Exception) {}
        }
    }
}
