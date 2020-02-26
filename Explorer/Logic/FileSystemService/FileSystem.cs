using Explorer.Entities;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;
using FileAttributes = Windows.Storage.FileAttributes;

namespace Explorer.Logic.FileSystemService
{
    public static partial class FileSystem
    {
        private static StorageFolder recyclingBinFolder;

        public static StorageFolder AppDataFolder = ApplicationData.Current.LocalFolder;

        public static StorageFolder RecyclingBinFolder
        {
            get
            {
                if (recyclingBinFolder == null)
                    recyclingBinFolder = CreateOrOpenRecyclingBin();

                return recyclingBinFolder;
            }
            private set => recyclingBinFolder = value;
        }

        private static StorageFolder CreateOrOpenRecyclingBin()
        {
            var array = AppDataFolder.Path.Split('\\');
            var username = array[2];
            string userFolderPath = @"C:\Users\" + username;

            var userFolder = StorageFolder.GetFolderFromPathAsync(userFolderPath).AsTask().Result;
            return userFolder.CreateFolderAsync("Recycling Bin", CreationCollisionOption.OpenIfExists).AsTask().Result;
        }

        public static async Task<Drive[]> GetDrivesAsync()
        {
            const string k_freeSpace = "System.FreeSpace";
            const string k_totalSpace = "System.Capacity";
            const string k_driveName = "System.FolderNameDisplay";

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            var drives = new Drive[allDrives.Length];

            for (int i = 0; i < allDrives.Length; i++)
            {
                DriveInfo d = allDrives[i];
                try
                {
                    //Inaccesible due to UWP permission stuff
                    //var rootPath = $"{drives[i].VolumeLabel}:\\";
                    StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(d.RootDirectory.FullName);
                    var props = await folder.Properties.RetrievePropertiesAsync(new string[] { k_freeSpace, k_totalSpace, k_driveName });
                    
                    drives[i] = new Drive
                    {
                        Name = (string)props[k_driveName],
                        DriveLetter = Convert.ToString(d.Name[0]),
                        RootDirectory = d.RootDirectory.FullName,
                        DriveType = d.DriveType,
                        FreeSpace = (ulong)props[k_freeSpace],
                        TotalSpace = (ulong)props[k_totalSpace]
                    };
                }
                catch (Exception)
                {
                    Debug.WriteLine(String.Format("Couldn't get info for drive {0}", d.Name));
                }
            }

            return drives;
        }

        public static async Task<BasicProperties> GetPropertiesOfFile(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            return await file.GetBasicPropertiesAsync();
        }

        public async static Task<BitmapImage> GetFileExtensionThumbnail(string extension, ThumbnailMode mode, uint size, ThumbnailOptions options)
        {
            if (extension == null || extension == "") return null;

            var dummy = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("dummy." + extension, CreationCollisionOption.ReplaceExisting); //may overwrite existing
            var thumbnail = await dummy.GetThumbnailAsync(mode, size, options);
            if (thumbnail == null) return null;

            var bitmap = new BitmapImage();
            bitmap.SetSource(thumbnail.CloneStream());

            await dummy.DeleteAsync();
            return bitmap;
        }

        #region Copy/Move

        public static async Task MoveStorageItemsAsync(FileSystemElement folder, List<IStorageItem> storageItems)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            var tasks = new Task[storageItems.Count];
            var targetFolder = await GetFolderAsync(folder);
            for (int i = 0; i < storageItems.Count; i++)
            {
                tasks[i] = CopyStorageItemAsync(targetFolder, storageItems[i]);
            }
            await Task.WhenAll(tasks);

            for (int i = 0; i < storageItems.Count; i++)
            {
                tasks[i] = DeleteStorageItemAsync(storageItems[i].Path, storageItems[i].Attributes.HasFlag(FileAttributes.Directory));
            }
            await Task.WhenAll(tasks);

            s.Stop();
            Debug.WriteLine("Move took: " + s.ElapsedMilliseconds + "ms");
        }

        public static async Task CopyStorageItemsAsync(FileSystemElement folder, List<IStorageItem> storageItems)
        {
            //var ops = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            Stopwatch s = new Stopwatch();
            s.Start();

            //var actions = new Action[storageItems.Count];
            //var targetFolder = await GetFolderAsync(folder);
            //for (int i = 0; i < storageItems.Count; i++)
            //{
            //    actions[i] = async () => await CopyStorageItemAsync(targetFolder, storageItems[i]);
            //}

            //Parallel.Invoke(ops, actions);






            var tasks = new Task[storageItems.Count];
            var targetFolder = await GetFolderAsync(folder);
            for (int i = 0; i < storageItems.Count; i++)
            {
                tasks[i] = CopyStorageItemAsync(targetFolder, storageItems[i]);
            }
            await Task.WhenAll(tasks);

            s.Stop();
            Debug.WriteLine("Copy took: " + s.ElapsedMilliseconds + "ms");
        }

        public static async Task CopyStorageItemAsync(StorageFolder target, IStorageItem storageItem)
        {
            if (storageItem.Attributes.HasFlag(FileAttributes.Directory))
            {
                var storageFolder = (StorageFolder)storageItem;
                var queryResult = GetFolderQuery(storageFolder, FolderDepth.Shallow, IndexerOption.UseIndexerWhenAvailable);

                //Create folder
                var copiedFolder = await target.CreateFolderAsync(storageFolder.Name, CreationCollisionOption.GenerateUniqueName);

                uint index = 0;
                const uint stepSize = 100;

                IReadOnlyList<IStorageItem> storageItems = await queryResult.GetItemsAsync(index, stepSize);
                while (storageItems.Count != 0 || index < 10000)
                {
                    foreach (IStorageItem file in storageItems)
                    {
                        _ = CopyStorageItemAsync(copiedFolder, file);
                    }

                    index += stepSize;
                    storageItems = await queryResult.GetItemsAsync(index, stepSize);
                }
            }
            else
            {
                var file = (StorageFile)storageItem;
                await file.CopyAsync(target, file.Name, NameCollisionOption.GenerateUniqueName);
            }
        }

        public static async Task MoveStorageItemAsync(StorageFolder target, IStorageItem storageItem)
        {
            if (storageItem.Attributes.HasFlag(FileAttributes.Directory))
            {
                var storageFolder = (StorageFolder)storageItem;
                var queryResult = GetFolderQuery(storageFolder, FolderDepth.Shallow, IndexerOption.UseIndexerWhenAvailable);

                //Create folder
                var copiedFolder = await target.CreateFolderAsync(storageFolder.Name, CreationCollisionOption.GenerateUniqueName);

                uint index = 0;
                const uint stepSize = 100;

                IReadOnlyList<IStorageItem> storageItems = await queryResult.GetItemsAsync(index, stepSize);
                while (storageItems.Count != 0 || index < 10000)
                {
                    foreach (IStorageItem file in storageItems)
                    {
                        _ = MoveStorageItemAsync(copiedFolder, file);
                    }

                    index += stepSize;
                    storageItems = await queryResult.GetItemsAsync(index, stepSize);
                }
            }
            else
            {
                var file = (StorageFile)storageItem;
                await file.MoveAsync(target);
            }
        }

        #endregion

        #region Get Files/Folders Actions
        public static async Task<StorageFile> GetFileAsync(FileSystemElement fse)
        {
            return await StorageFile.GetFileFromPathAsync(fse.Path);
        }

        public static async Task<StorageFile> GetFileAsync(string path)
        {
            return await StorageFile.GetFileFromPathAsync(path);
        }

        public static async Task<StorageFolder> GetFolderAsync(FileSystemElement fse)
        {
            return await StorageFolder.GetFolderFromPathAsync(fse.Path);
        }

        public static async Task<StorageFolder> GetFolderAsync(string path)
        {
            return await StorageFolder.GetFolderFromPathAsync(path);
        }


        /// <summary>
        /// Tries to get the folder/file from the given path.
        /// </summary>
        /// <param name="path">The full path to the element</param>
        /// <returns>The folder/file or null if not found</returns>
        public static async Task<FileSystemElement> GetFileSystemElementAsync(string path)
        {
            try
            {
                var folder = await GetFolderAsync(path);
                return new FileSystemElement(folder.Name, folder.Path, folder.DateCreated, 0);
            }
            catch(Exception) { }
            
            try
            {
                var file = await GetFileAsync(path);
                var props = await file.GetBasicPropertiesAsync();
                var tn = await file.GetThumbnailAsync(ThumbnailMode.ListView);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(tn.CloneStream());
                return new FileSystemElement(file.Name, file.Path, props.DateModified, props.Size, bitmap, file.FileType, file.DisplayType);
            }
            catch (Exception) { }

            return null;
        }

        public static async Task<IStorageItem> GetStorageItemAsync(FileSystemElement fse)
        {
            if (fse.IsFolder)
                return await GetFolderAsync(fse);
            return await GetFileAsync(fse);
        }

        public static async Task<IStorageItem[]> GetStorageItemsAsync(Collection<FileSystemElement> fses)
        {
            var sis = new IStorageItem[fses.Count];
            for (int i = 0; i < fses.Count; i++)
            {
                sis[i] = await GetStorageItemAsync(fses[i]);
            }
            return sis;
        }

        #endregion

        public static async Task<StorageItemQueryResult> GetItemsQuery(string folder, FolderDepth depth, IndexerOption indexer)
        {
            return (await GetFolderAsync(folder)).CreateItemQuery();
        }

        public static StorageItemQueryResult GetFolderQuery(StorageFolder folder, FolderDepth depth, IndexerOption indexer)
        {
            QueryOptions query = new QueryOptions(CommonFileQuery.DefaultQuery, new List<string>())
            {
                FolderDepth = depth,
                //ApplicationSearchFilter = "System.Security.EncryptionOwners:[]",  // Filter out all files that have WIP enabled
                IndexerOption = indexer
            };

            return folder.CreateItemQueryWithOptions(query);
        }

        public static async Task<IEnumerable<FileSystemElement>> GetFolderContentSimple(string path)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);

            var itemsList = await folder.GetItemsAsync();
            var resultList = new List<FileSystemElement>(itemsList.Count);
            foreach (var element in itemsList)
            {
                var props = await element.GetBasicPropertiesAsync();

                resultList.Add(new FileSystemElement(element.Name, element.Path, props.DateModified, props.Size));
            }

            return resultList;
        }

        public static async void LaunchExeAsync(string appPath, string arguments = "")
        {
            Debug.WriteLine("Launching EXE in FullTrustProcess");
            ApplicationData.Current.LocalSettings.Values["LaunchPath"] = appPath;
            ApplicationData.Current.LocalSettings.Values["LaunchArguments"] = arguments;
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        #region Fast folder move doesnt work due to UWP Permission stuff => PLS MICROSOFT let good programms do some fast copying
        public static async Task MoveFolderFast(IStorageFolder source, IStorageFolder destination)
        {
            await Task.Run(() =>
            {
                MoveContextImpl(new DirectoryInfo(source.Path), destination);
            });
        }

        private static void MoveContextImpl(DirectoryInfo sourceFolderInfo, IStorageFolder destination)
        {
            var tasks = new List<Task>();
            var destinationAccess = destination as IStorageFolderHandleAccess;

            foreach (var item in sourceFolderInfo.EnumerateFileSystemInfos())
            {
                if ((item.Attributes & System.IO.FileAttributes.Directory) != 0)
                {
                    tasks.Add(destination.CreateFolderAsync(item.Name, CreationCollisionOption.ReplaceExisting).AsTask().ContinueWith((destinationSubFolder) =>
                    {
                        MoveContextImpl((DirectoryInfo)item, destinationSubFolder.Result);
                    }));
                }
                else
                {
                    if (destinationAccess == null)
                    {
                        // Slower, pre 14393 OS build path
                        tasks.Add(WindowsRuntimeStorageExtensions.OpenStreamForWriteAsync(destination, item.Name, CreationCollisionOption.ReplaceExisting).ContinueWith((openTask) =>
                        {
                            using (var stream = openTask.Result)
                            {
                                var sourceBytes = File.ReadAllBytes(item.FullName);
                                stream.Write(sourceBytes, 0, sourceBytes.Length);
                            }

                            File.Delete(item.FullName);
                        }));
                    }
                    else
                    {
                        int hr = destinationAccess.Create(item.Name, HANDLE_CREATION_OPTIONS.CREATE_ALWAYS, HANDLE_ACCESS_OPTIONS.WRITE, HANDLE_SHARING_OPTIONS.SHARE_NONE, HANDLE_OPTIONS.NONE, IntPtr.Zero, out SafeFileHandle file);
                        if (hr < 0)
                            Marshal.ThrowExceptionForHR(hr);

                        using (file)
                        {
                            using (var stream = new FileStream(file, FileAccess.Write))
                            {
                                var sourceBytes = File.ReadAllBytes(item.FullName);
                                stream.Write(sourceBytes, 0, sourceBytes.Length);
                            }
                        }

                        File.Delete(item.FullName);
                    }
                }
            }

            Task.WaitAll(tasks.ToArray());
        }


        [ComImport]
        [Guid("DF19938F-5462-48A0-BE65-D2A3271A08D6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IStorageFolderHandleAccess
        {
            [PreserveSig]
            int Create(
                [MarshalAs(UnmanagedType.LPWStr)] string fileName,
                HANDLE_CREATION_OPTIONS creationOptions,
                HANDLE_ACCESS_OPTIONS accessOptions,
                HANDLE_SHARING_OPTIONS sharingOptions,
                HANDLE_OPTIONS options,
                IntPtr oplockBreakingHandler,
                out SafeFileHandle interopHandle); // using Microsoft.Win32.SafeHandles
        }

        internal enum HANDLE_CREATION_OPTIONS : uint
        {
            CREATE_NEW = 0x1,
            CREATE_ALWAYS = 0x2,
            OPEN_EXISTING = 0x3,
            OPEN_ALWAYS = 0x4,
            TRUNCATE_EXISTING = 0x5,
        }

        [Flags]
        internal enum HANDLE_ACCESS_OPTIONS : uint
        {
            NONE = 0,
            READ_ATTRIBUTES = 0x80,
            // 0x120089
            READ = SYNCHRONIZE | READ_CONTROL | READ_ATTRIBUTES | FILE_READ_EA | FILE_READ_DATA,
            // 0x120116
            WRITE = SYNCHRONIZE | READ_CONTROL | FILE_WRITE_ATTRIBUTES | FILE_WRITE_EA | FILE_APPEND_DATA | FILE_WRITE_DATA,
            DELETE = 0x10000,

            READ_CONTROL = 0x00020000,
            SYNCHRONIZE = 0x00100000,
            FILE_READ_DATA = 0x00000001,
            FILE_WRITE_DATA = 0x00000002,
            FILE_APPEND_DATA = 0x00000004,
            FILE_READ_EA = 0x00000008,
            FILE_WRITE_EA = 0x00000010,
            FILE_EXECUTE = 0x00000020,
            FILE_WRITE_ATTRIBUTES = 0x00000100,
        }

        [Flags]
        internal enum HANDLE_SHARING_OPTIONS : uint
        {
            SHARE_NONE = 0,
            SHARE_READ = 0x1,
            SHARE_WRITE = 0x2,
            SHARE_DELETE = 0x4
        }

        [Flags]
        internal enum HANDLE_OPTIONS : uint
        {
            NONE = 0,
            OPEN_REQUIRING_OPLOCK = 0x40000,
            DELETE_ON_CLOSE = 0x4000000,
            SEQUENTIAL_SCAN = 0x8000000,
            RANDOM_ACCESS = 0x10000000,
            NO_BUFFERING = 0x20000000,
            OVERLAPPED = 0x40000000,
            WRITE_THROUGH = 0x80000000
        }

        #endregion
    }
}
