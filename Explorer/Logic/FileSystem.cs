using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Xaml.Media.Animation;
using File = Explorer.Entities.File;
using FileAttributes = Windows.Storage.FileAttributes;

namespace Explorer.Logic
{
    public class FileSystem
    {
        public static DriveInfo[] GetDrives()
        {
            return DriveInfo.GetDrives();
        }

        public static async void OpenFileWithDefaultApp(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            await Launcher.LaunchFileAsync(file);
        }

        public static async void OpenFileWith(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            await Launcher.LaunchFileAsync(file, new LauncherOptions{DisplayApplicationPicker = true});
        }

        public static async void DeleteStorageItem(FileSystemElement fse)
        {
            var storageItem = await GetStorageItemAsync(fse);
            await storageItem.DeleteAsync();
        }

        public static async void RenameStorageItem(FileSystemElement fse, string newName)
        {
            var file = await GetStorageItemAsync(fse);
            await file.RenameAsync(newName);
        }

        public static async Task<BasicProperties> GetPropertiesOfFile(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            return await file.GetBasicPropertiesAsync();
        }

        public static async Task<IStorageItem> GetFileAsync(FileSystemElement fse)
        {
            return await StorageFile.GetFileFromPathAsync(fse.Path);
        }

        public static async Task<IStorageItem> GetFolderAsync(FileSystemElement fse)
        {
            return await StorageFolder.GetFolderFromPathAsync(fse.Path);
        }

        public static async Task<IStorageItem> GetStorageItemAsync(FileSystemElement fse)
        {
            if (fse.Type.HasFlag(FileAttributes.Directory))
                return await GetFolderAsync(fse);
            return await GetFileAsync(fse);
        }


        public static async Task<IEnumerable<FileSystemElement>> GetFolderContentSimple(string path)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);

            var itemsList = await folder.GetItemsAsync();
            var resultList = new List<FileSystemElement>(itemsList.Count);
            foreach (var element in itemsList)
            {
                var props = await element.GetBasicPropertiesAsync();

                resultList.Add(new FileSystemElement
                {
                    Name = element.Name,
                    Size = props.Size,
                    Type = element.Attributes,
                    DateModified = props.DateModified,
                    Path = element.Path
                });
            }

            return resultList;
        }

        public static async Task<Folder[]> GetFoldersSimpleAsync(StorageFolder folder)
        {
            return (await folder.GetFoldersAsync()).Select(x => new Folder(x.Name)).ToArray();
        }

        public static async Task<File[]> GetFilesSimpleAsync(StorageFolder folder)
        {
            var storageFiles = await folder.GetFilesAsync();
            var returnFiles = new File[storageFiles.Count];

            uint i = 0;
            foreach (var file in storageFiles)
            {
                var props = await file.GetBasicPropertiesAsync();
                returnFiles[i] = new File(file.Name, props.Size, props.DateModified);
                i++;
            }

            return returnFiles;
        }

        public static async void LaunchExeAsync(string appPath, string arguments)
        {
            Debug.WriteLine("Launching EXE in FullTrustProcess");
            ApplicationData.Current.LocalSettings.Values["LaunchPath"] = appPath;
            ApplicationData.Current.LocalSettings.Values["LaunchArguments"] = arguments;
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        //public static async IEnumerable<FileSystemElement> GetFolderContent(string path)
        //{
        //    var storageFolder = await StorageFolder.GetFolderFromPathAsync(path);   //Get the folder

        //    // Check if the folder is indexed before doing anything. 
        //    IndexedState folderIndexedState = await storageFolder.GetIndexedStateAsync();
        //    if (folderIndexedState == IndexedState.NotIndexed || folderIndexedState == IndexedState.Unknown)
        //    {
        //        // Only possible in indexed directories.  
        //        return;
        //    }

        //    QueryOptions query = new QueryOptions()
        //    {
        //        FolderDepth = FolderDepth.Shallow,
        //        ApplicationSearchFilter = "System.Security.EncryptionOwners:[]",  // Filter out all files that have WIP enabled
        //        IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties
        //    };

        //    //query.FileTypeFilter.Add(".jpg");
        //    //string[] otherProperties = new string[]
        //    //{
        //    //SystemProperties.GPS.LatitudeDecimal,
        //    //SystemProperties.GPS.LongitudeDecimal
        //    //};

        //    query.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, Array.Empty<string>());
        //    SortEntry sortOrder = new SortEntry()
        //    {
        //        AscendingOrder = true,
        //        PropertyName = "System.FileName" // FileName property is used as an example. Any property can be used here.  
        //    };
        //    query.SortOrder.Add(sortOrder);

        //    // Create the query and get the results 
        //    uint index = 0;
        //    const uint stepSize = 100;
        //    if (!storageFolder.AreQueryOptionsSupported(query))
        //    {
        //        query.SortOrder.Clear();
        //        throw new Exception("Querying for a sort order is not supported in this location");
        //    }

        //    StorageFileQueryResult queryResult = storageFolder.CreateFileQueryWithOptions(query);
        //    IReadOnlyList<StorageFile> images = await queryResult.GetFilesAsync(index, stepSize);
        //    while (images.Count != 0 || index < 10000)
        //    {
        //        foreach (StorageFile file in images)
        //        {
        //            // With the OnlyUseIndexerAndOptimizeForIndexedProperties set, this won't  
        //            // be async. It will run synchronously. 
        //            var basicProps = await file.GetBasicPropertiesAsync();

        //            yield return new FileSystemElement()
        //            {
        //                Name 
        //            };
        //            // Build the UI 
        //            //log(String.Format("{0} at {1}, {2}",
        //            //     file.Path,
        //            //     imageProps.Latitude,
        //            //     imageProps.Longitude));
        //            //}
        //            index += stepSize;
        //            images = await queryResult.GetFilesAsync(index, stepSize);
        //        }
        //    }
        //}
    }
}
