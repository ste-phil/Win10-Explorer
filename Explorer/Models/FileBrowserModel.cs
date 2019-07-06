using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using Newtonsoft.Json;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.System;
using static Explorer.Logic.FileSystemRetrieveService;
using static Explorer.Controls.FileBrowser;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Input;

namespace Explorer.Models
{
    public delegate void FSEEventHandler(FileSystemElement fse);

    public class FileBrowserModel : BaseModel
    {
        public event FSEEventHandler FavoriteAddRequested;

        //Used for windows share
        private static DataTransferManager dataTransferManager;
        private static DataPackage sharedData;

        private FileSystemRetrieveService retrieveService;
        private FileSystemOperationService operationSerivce;

        private string path;
        private bool pathIncreased;
        private FileSystemElement currentFolder;

        //Tracks the history position
        private List<FileSystemElement> history;
        private int historyPosition;

        private ObservableCollection<FileSystemElement> selectedItems;
        private FileSystemElement doubleTappedItem;
        private string renameName;

        private short viewModeCurrent;
        private ViewMode[] viewModes;
        private double gridViewItemWidth;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        public ObservableCollection<FileSystemElement> PathSuggestions { get; set; }
        [JsonIgnore] public ContentDialog RenameDialog { get; set; }
        public bool TextBoxPathIsFocused { get; set; }

        public double FileBrowserWidth { get; set; }

        public FileBrowserModel()
        {
            //Set start folder to windows disk
            CurrentFolder = new FileSystemElement { Path = "C:", Name = "Windows" };
            SelectedItems = new ObservableCollection<FileSystemElement>();

            NavigateBack = new Command(() => NavigateToNoHistory(history[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(() => NavigateToNoHistory(history[++HistoryPosition]), () => HistoryPosition < history.Count - 1);
            ToggleView = new Command(async () => await ToggleViewMode(), () => true);

            history = new List<FileSystemElement>();
            HistoryPosition = -1;

            retrieveService = new FileSystemRetrieveService();
            FileSystemElements = retrieveService.Items;

            operationSerivce = FileSystemOperationService.Instance;

            PathSuggestions = new ObservableCollection<FileSystemElement>();

            //For sharing files, only one object for all fileBrowsers
            if (dataTransferManager == null)
            {
                dataTransferManager = DataTransferManager.GetForCurrentView();
                dataTransferManager.DataRequested += OnShareRequested;
            }

            ViewModes = new ViewMode[]
            {
                new ViewMode(ThumbnailMode.ListView, "\uF0E2", Visibility.Visible),
                new ViewMode(ThumbnailMode.PicturesView, "\uE8FD")
            };

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
            get { return currentFolder?.Name; }
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

        public short ViewModeCurrent
        {
            get { return viewModeCurrent; }
            set { viewModeCurrent = value; OnPropertyChanged(); OnPropertyChanged("ViewModeIcon"); }
        }

        public ViewMode[] ViewModes
        {
            get { return viewModes; }
            set { viewModes = value; OnPropertyChanged(); OnPropertyChanged("ViewModeIcon"); }
        }

        public double GridViewItemWidth
        {
            //MinWidth = 1 for GridView
            get { return gridViewItemWidth <= 0 ? 1 : gridViewItemWidth; }
            set { gridViewItemWidth = value; OnPropertyChanged(); }
        }

        public string ViewModeIcon => ViewMode?.Icon;

        public ViewMode ViewMode => ViewModes?[ViewModeCurrent];

        public Command NavigateBack { get; }

        public Command NavigateForward { get; }

        public Command ToggleView { get; }

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
            else
            {
                if (fse.Type == ".exe") FileSystem.LaunchExeAsync(fse.Path, "");
                else FileSystem.OpenFileWithDefaultApp(fse.Path);
            }
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

        private ThumbnailFetchOptions GetThumbnailFetchOptions()
        {
            //Set thumbnail size depending in which viewmode the user is
            //but only when it has been set, else enable TableView
            uint thumbnailSize = 20;
            var mode = ThumbnailMode.ListView;
            if (ViewMode != null)
            {
                GridViewItemWidth = FileBrowserWidth / 3 - 50;
                thumbnailSize = (uint)GridViewItemWidth - 50;
                mode = ViewMode.Type;
            }

            return new ThumbnailFetchOptions
            {
                Mode = mode,
                Size = thumbnailSize,
                Scale = ThumbnailOptions.UseCurrentScale
            };
        }

        /// <summary>
        /// Loads the folder's contents
        /// </summary>
        private async void LoadFolderAsync(FileSystemElement fse)
        {
            try
            {
                var options = GetThumbnailFetchOptions();
                await retrieveService.LoadFolderAsync(fse.Path, options);

                if (Path != fse.Path) return;

                CurrentFolder = fse;
            }
            catch (Exception)
            {
                //await Windows.System.Launcher.LaunchUriAsync(new Uri(“ms-settings:privacy-broadfilesystemaccess”));
                //Show error that path was not found
            }
        }

        /// <summary>
        /// Reloads the currently selected Folder
        /// </summary>
        public void Refresh()
        {
            LoadFolderAsync(CurrentFolder);
        }

        #region Selected FileSystemElements Methods

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
            if (!selectedItem.IsFolder) FileSystem.OpenFileWith(selectedItem.Path);
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
        public async void RenameStorageItemSelectedAsync()
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
        public async void DeleteStorageItemSelected()
        {
            try
            {
                await FileSystem.DeleteStorageItemsAsync(SelectedItems);
                FileSystemElements.RemoveRange(SelectedItems);
            }
            catch (Exception) { /*e.g. UnauthorizedAccessException*/}
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
                    await operationSerivce.BeginCopyOperation(items.ToList(), CurrentFolder);
                    data.ReportOperationCompleted(DataPackageOperation.Copy);
                }
                else if (data.RequestedOperation.HasFlag(DataPackageOperation.Move))
                {
                    await operationSerivce.BeginMoveOperation(items.ToList(), CurrentFolder);
                    data.ReportOperationCompleted(DataPackageOperation.Move);
                }
            }
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

        #endregion

        public async Task ToggleViewMode()
        {
            //Hide old view
            ViewMode.Visibility = Visibility.Collapsed;

            //Cycle through modes
            //Reset when exceeded
            if (ViewModeCurrent < ViewModes.Length - 1) ViewModeCurrent += 1;
            else ViewModeCurrent = 0;

            //Show new view
            ViewMode.Visibility = Visibility.Visible;

            var options = GetThumbnailFetchOptions();
            await retrieveService.RefetchThumbnails(options);

            //var fse = new FileSystemElement();
            //SelectedItems.Add(fse);
            //SelectedItems.Remove(fse);

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
        /// Provides different key shortcuts
        /// </summary>
        /// <param name="key"></param>
        public void KeyDown(VirtualKey key)
        {
            switch (key)
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
                    RenameStorageItemSelectedAsync();
                    break;
                case VirtualKey.Delete:
                    DeleteStorageItemSelected();
                    break;
            }

            var ctrlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (!ctrlDown) return;

            switch (key)
            {
                case VirtualKey.C:
                    CopyStorageItemSelected();
                    break;
                case VirtualKey.X:
                    CutStorageItemSelected();
                    break;
                case VirtualKey.V:
                    PasteStorageItemSelected();
                    break;
            }
        }
    }
}
