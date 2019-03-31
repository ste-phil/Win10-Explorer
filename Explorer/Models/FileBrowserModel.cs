using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using FileAttributes = Windows.Storage.FileAttributes;
using Newtonsoft.Json;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.System;

namespace Explorer.Models
{
    public delegate void FSEEventHandler(FileSystemElement fse);

    public class FileBrowserModel : BaseModel
    {
        public event FSEEventHandler FavoriteAddRequested;

        //Used for windows share
        private static DataTransferManager dataTransferManager;
        private static DataPackage sharedData;

        private string path;
        private bool pathIncreased;
        private FileSystemElement currentFolder;

        //Tracks the history position
        private List<FileSystemElement> history;
        private int historyPosition;

        private ObservableCollection<FileSystemElement> selectedItems;
        private FileSystemElement doubleTappedItem;
        private string renameName;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        public ObservableCollection<FileSystemElement> PathSuggestions { get; set; }
        [JsonIgnore] public ContentDialog RenameDialog { get; set; }
        public bool TextBoxPathIsFocused { get; set; }

        public FileBrowserModel()
        {
            FileSystemElements = new ObservableCollection<FileSystemElement>();
            CurrentFolder = new FileSystemElement { Path = "C:", Name = "Windows" };
            SelectedItems = new ObservableCollection<FileSystemElement>();

            NavigateBack = new Command(() => NavigateToNoHistory(history[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(() => NavigateToNoHistory(history[++HistoryPosition]), () => HistoryPosition < history.Count - 1);

            history = new List<FileSystemElement>();
            HistoryPosition = -1;

            PathSuggestions = new ObservableCollection<FileSystemElement>();

            //For sharing files, only one object for all fileBrowsers
            if (dataTransferManager == null)
            {
                dataTransferManager = DataTransferManager.GetForCurrentView();
                dataTransferManager.DataRequested += OnShareRequested;
            }

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;

            NavigateTo(CurrentFolder);
        }

        #region Properties

        public FileSystemElement CurrentFolder
        {
            get { return currentFolder; }
            private set
            {
                currentFolder = value;
                OnPropertyChanged();
                OnPropertyChanged("Path");
                OnPropertyChanged("Name");
                UpdatePathSuggestions();
            }
        }

        public string Name
        {
            get { return currentFolder.Name; }
            set { currentFolder.Name = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get { return path; }
            set
            {
                //Check if path got longer
                pathIncreased = (path == null && value != null) || value.Length > path.Length;

                path = value;
                OnPropertyChanged();
                UpdatePathSuggestions();
            }
        }

        public int HistoryPosition
        {
            get { return historyPosition; }
            private set { historyPosition = value; OnPropertyChanged(); NavigateBack.CanExceuteChanged(); NavigateForward.CanExceuteChanged(); }
        }

        public ObservableCollection<FileSystemElement> SelectedItems
        {
            get { return selectedItems; }
            set { selectedItems = value; OnPropertyChanged(); }
        }

        public FileSystemElement DoubleTappedItem
        {
            get { return doubleTappedItem; }
            set { doubleTappedItem = value; OnPropertyChanged(); NavigateOrOpen(doubleTappedItem); }
        }

        public string RenameName
        {
            get { return renameName; }
            set { renameName = value; OnPropertyChanged(); }
        }
        
        public Command NavigateBack { get; }

        public Command NavigateForward { get; }

        #endregion

        /// <summary>
        /// Clears the navigation history
        /// </summary>
        public void ClearHistory()
        {
            history.Clear();
            HistoryPosition = -1;
        }

        /// <summary>
        /// Navigate to the passed Folder
        /// </summary>
        /// <param name="fse"></param>
        public void NavigateTo(FileSystemElement fse)
        {
            Path = fse.Path;

            history.RemoveRange(HistoryPosition + 1, history.Count - (HistoryPosition + 1));
            history.Insert(HistoryPosition + 1, fse);
            HistoryPosition++;

            LoadFolderAsync(fse);
        }

        /// <summary>
        /// Opens or navigates to the passed file system element
        /// </summary>
        public void NavigateOrOpen(FileSystemElement fse)
        {
            if (fse == null) return;

            if (fse.IsFolder) NavigateTo(fse);
            else FileSystem.OpenFileWithDefaultApp(fse.Path);
        }

        /// <summary>
        /// Navigate without history to the passed Folder (internal use e.g. nav buttons)
        /// </summary>
        /// <param name="fse"></param>
        private void NavigateToNoHistory(FileSystemElement fse)
        {
            Path = fse.Path;

            LoadFolderAsync(fse);
        }

        /// <summary>
        /// Launches an executable file
        /// </summary>
        /// <param name="path">The path to the executable</param>
        /// <param name="arguments">The launch arguments passed to the executable</param>
        public void LaunchExe(string path, string arguments = null)
        {
            FileSystem.LaunchExeAsync(path, arguments);
        }

        /// <summary>
        /// Loads the folder's contents
        /// </summary>
        private async void LoadFolderAsync(FileSystemElement fse)
        {
            try
            {
                var files = await Task.Run(() => FileSystem.GetFolderContentSimple(fse.Path));
                if (Path != fse.Path) return;

                FileSystemElements.Clear();
                foreach (var file in files)
                {
                    FileSystemElements.Add(file);
                }

                CurrentFolder = fse;

            }
            catch (Exception)
            {
                //await Windows.System.Launcher.LaunchUriAsync(new Uri(“ms-settings:privacy-broadfilesystemaccess”));
                //Show error that path was not found
            }
        }

        /// <summary>
        /// Called from system when DataTransferManager.ShowShareUI has been called
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnShareRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            args.Request.Data = sharedData;
        }

        /// <summary>
        /// Opens or navigates to the currently selected file system element
        /// </summary>
        public void NavigateOrOpenSelected()
        {
            if (SelectedItems.Count != 1) return;

            var selectedItem = SelectedItems[0];
            if (selectedItem.IsFolder) NavigateTo(selectedItem);
            else FileSystem.OpenFileWithDefaultApp(selectedItem.Path);
        }

        /// <summary>
        /// Brings up the application picker to open the selected file system element
        /// </summary>
        public void OpenFileWithSelected()
        {
            if (SelectedItems.Count != 1) return;

            var selectedItem = SelectedItems[0];
            if (!selectedItem.Type.HasFlag(FileAttributes.Directory)) FileSystem.OpenFileWith(selectedItem.Path);
        }

        /// <summary>
        /// Shares the currently selected file system Element via the new Windows share feature
        /// </summary>
        public async void ShareStorageItem()
        {
            DataTransferManager.ShowShareUI();
            sharedData = new DataPackage();

            if (SelectedItems.Count == 1) sharedData.Properties.Title = SelectedItems[0].Name;
            else sharedData.Properties.Title = "Sharing multiple items";

            sharedData.Properties.Description = "This element will be shared";
            sharedData.SetStorageItems(await FileSystem.GetStorageItemsAsync(SelectedItems));
        }

        /// <summary>
        /// Renames the currently selected file system element
        /// </summary>
        public async Task RenameStorageItemSelectedAsync()
        {
            if (SelectedItems.Count == 0) return;

            RenameName = SelectedItems[0].Name;

            var result = await RenameDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var userInputName = RenameName;
                    for (int i = 0; i < selectedItems.Count; i++)
                    {
                        var newName = selectedItems.Count == 1 ? userInputName : $"{userInputName}_{i + 1}";

                        await FileSystem.RenameStorageItemAsync(SelectedItems[i], newName);

                        SelectedItems[i].Name = newName;
                        SelectedItems[i].Path = SelectedItems[i].Path.Substring(0, SelectedItems[i].Path.LastIndexOf("\\") + 1) + newName;
                    }


                    OnPropertyChanged("FileSystemElements");
                }
                catch (Exception) { }
            }

            RenameName = "";
        }

        /// <summary>
        /// Deletes the currently selected file system element
        /// </summary>
        public void DeleteStorageItemSelected()
        {
            try
            {
                FileSystem.DeleteStorageItemsAsync(SelectedItems);
                FileSystemElements.RemoveRange(SelectedItems);
                SelectedItems.Clear();
            } catch(Exception) { /*e.g. UnauthorizedAccessException*/}
        }

        public async void CopyStorageItemSelected()
        {
            var dataPackage = new DataPackage();

            var itemsToCopy = await FileSystem.GetStorageItemsAsync(SelectedItems);
            dataPackage.SetStorageItems(itemsToCopy);
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            Clipboard.SetContent(dataPackage);
            //Clipboard.Flush();
        }

        public async void CutStorageItemSelected()
        {
            var dataPackage = new DataPackage();

            var itemsToCopy = await FileSystem.GetStorageItemsAsync(SelectedItems);
            dataPackage.SetStorageItems(itemsToCopy);
            dataPackage.RequestedOperation = DataPackageOperation.Move;

            Clipboard.SetContent(dataPackage);
            //Clipboard.Flush();
        }

        public async void PasteStorageItemSelected()
        {
            var data = Clipboard.GetContent();

            if (data.Contains(StandardDataFormats.StorageItems))
            {
                var items = await data.GetStorageItemsAsync();
                if (data.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                {
                    await FileSystem.CopyStorageItemsAsync(CurrentFolder, items.ToList());
                    data.ReportOperationCompleted(DataPackageOperation.Copy);
                }
                else if (data.RequestedOperation.HasFlag(DataPackageOperation.Move))
                {
                    await FileSystem.MoveStorageItemsAsync(CurrentFolder, items.ToList());
                    data.ReportOperationCompleted(DataPackageOperation.Move);
                }
            }

            //var src = await FileSystem.GetFolderAsync(@"C:\Users\phste\Downloads\CopyTest");
            //var dest = await FileSystem.GetFolderAsync(@"C:\Users\phste\Downloads\CopyTestResult");
            //FileSystem.CopyStorageItemsAsync(new FileSystemElement { Path = @"C:\Users\phste\Downloads\CopyTestResult" }, new Collection<IStorageItem> { src });
        }

        /// <summary>
        /// Shows a popup with the properties of the selected file system element
        /// </summary>
        public async void ShowPropertiesStorageItemSelected()
        {
            //var props = await FileSystem.GetPropertiesOfFile(SelectedItems.Path);
        }

        /// <summary>
        /// Invokes event to tell subscribers to add selected fse to favorites
        /// </summary>
        public void FavoriteItemSelected()
        {
            FavoriteAddRequested?.Invoke(SelectedItems[0]);
        }

        /// <summary>
        /// Reloads the currently selected Folder
        /// </summary>
        public void Refresh()
        {
            LoadFolderAsync(CurrentFolder);
        }

        /// <summary>
        /// Event called in UI that navigates to the passed FileSystemElement
        /// </summary>
        public void NavigateNextFSE(object sender, FileSystemElement fileSystemElement)
        {
            //if (fileSystemElement.IsFolder) NavigateTo(fileSystemElement);
            //else FileSystem.OpenFileWithDefaultApp(SelectedItems.Path);
        }

        public async void UpdatePathSuggestions()
        {
            //Skip if Path has not been initialized or not focues
            if (!TextBoxPathIsFocused || (Path == null || Path == "")) return;

            var folders = Path.Split('\\');
            var searchString = folders[folders.Length - 1];

            string path = Path;
            if (!FileSystem.DirectoryExists(path) && folders.Length > 1) path = Path.Substring(0, Path.LastIndexOf("\\"));

            try
            {
                var folderContent = await FileSystem.GetFolderContentSimple(path);
                PathSuggestions.Clear();
                foreach (FileSystemElement fse in folderContent)
                {
                    if (fse.Name.Contains(searchString))
                        PathSuggestions.Add(fse);
                }
            }
            catch (Exception)
            {
                PathSuggestions.Clear();
            }
        }

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.Left:
                    NavigateBack.ExecuteWhen();
                    break;
                case VirtualKey.Right:
                    NavigateForward.ExecuteWhen();
                    break;
                case VirtualKey.Back:
                    NavigateBack.ExecuteWhen();
                    break;
                case VirtualKey.F5:
                    Refresh();
                    break;
                case VirtualKey.F2:
                    await RenameStorageItemSelectedAsync();
                    break;
            }
        }
    }
}
