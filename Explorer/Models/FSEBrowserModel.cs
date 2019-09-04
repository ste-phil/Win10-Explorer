using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using Newtonsoft.Json;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.System;
using static Explorer.Logic.FileSystemRetrieveService;
using Windows.Storage.FileProperties;
using static Explorer.Controls.FSEBrowser;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Explorer.Controls;
using Windows.UI.Xaml.Controls;
using Explorer.Logic.FileSystemService;

namespace Explorer.Models
{
    public delegate void FSEEventHandler(FileSystemElement fse);
    public delegate void FileDragEvent(DataPackage dataPackage, DragUI dragUI);
    public delegate void FileDropEvent(FileSystemElement droppedTo, IEnumerable<IStorageItem> droppeditems);

    public class FSEBrowserModel : BaseModel
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

        //Model for the different dialogs
        private DialogModel dialog;

        private ObservableCollection<FileSystemElement> selectedItems;

        private short viewModeCurrent;
        private ViewMode[] viewModes;
        private double gridViewItemWidth;

        public IBrowserService BrowserService;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }
        public ObservableCollection<FileSystemElement> PathSuggestions { get; set; }
        public bool TextBoxPathIsFocused { get; set; }
        public double FileBrowserWidth { get; set; }

        public FSEBrowserModel(FileSystemElement folder)
        {
            CurrentFolder = folder;

            NavigateBack = new Command(() => NavigateToNoHistory(history[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(() => NavigateToNoHistory(history[++HistoryPosition]), () => HistoryPosition < history.Count - 1);
            ToggleView = new Command(async () => await ToggleViewMode(), () => true);

            Init();
        }

        public FSEBrowserModel()
        {
            //Set start folder to windows disk if it has not been set
            CurrentFolder = new FileSystemElement { Path = "C:", Name = "Windows" };

            NavigateBack = new Command(() => NavigateToNoHistory(history[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(() => NavigateToNoHistory(history[++HistoryPosition]), () => HistoryPosition < history.Count - 1);
            ToggleView = new Command(async () => await ToggleViewMode(), () => true);

            Init();
        }

        private void Init()
        {
            FileSystemElements = new ObservableCollection<FileSystemElement>();
            SelectedItems = new ObservableCollection<FileSystemElement>();

            history = new List<FileSystemElement>();
            HistoryPosition = -1;

            BrowserService = new FileBrowserService(FileSystemElements);
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
                OnPropertyChanged("SearchPlaceholder");
                UpdatePathSuggestions();
            }
        }

        public string Name
        {
            get { return currentFolder?.Name; }
            set { currentFolder.Name = value; OnPropertyChanged(); OnPropertyChanged("SearchPlaceholder"); }
        }

        public string SearchPlaceholder => "Search " + Name;

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

        public DialogModel Dialog
        {
            get { return dialog; }
            set { dialog = value; OnPropertyChanged(); }
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
        /// Navigate without history to the passed Folder (internal use e.g. nav buttons)
        /// </summary>
        /// <param name="fse"></param>
        private void NavigateToNoHistory(FileSystemElement fse)
        {
            CheckChangeFSEBrowserImplementation(fse);

            Path = fse.Path;
            LoadFolder(fse);
        }

        /// <summary>
        /// Navigate to the passed Folder
        /// </summary>
        /// <param name="fse"></param>
        public void NavigateTo(FileSystemElement fse)
        {
            CheckChangeFSEBrowserImplementation(fse);

            Path = fse.Path;

            history.RemoveRange(HistoryPosition + 1, history.Count - (HistoryPosition + 1));
            history.Insert(HistoryPosition + 1, fse);
            HistoryPosition++;

            LoadFolder(fse);
        }

        /// <summary>
        /// Opens or navigates to the passed file system element
        /// </summary>
        public void NavigateOrOpen(FileSystemElement fse)
        {
            if (fse == null) return;

            if (fse.IsFolder || fse.IsArchive) NavigateTo(fse);
            else BrowserService.OpenFileSystemElement(fse);
        }

        public async void NavigateOrOpen(string path)
        {
            var fse = await FileSystem.GetFileSystemElementAsync(path);
            if (fse == null) Path = CurrentFolder.Path; //Change path to current folder when passed path was invalid
            else NavigateOrOpen(fse);   //Try to navigate to passed path
        }


        private void CheckChangeFSEBrowserImplementation(FileSystemElement fse)
        {
            if (!(fse is ZipFileElement) && !fse.IsArchive && BrowserService is ZipBrowserService)
            {
                BrowserService = new FileBrowserService(FileSystemElements);
            }
            else if (fse.IsArchive && BrowserService is FileBrowserService)
            {
                BrowserService = new ZipBrowserService(FileSystemElements);
            }
        }

        public void Search(string search)
        {
            BrowserService.SearchAsync(search);
        }

        /// <summary>
        /// Returns Thumbnail options for the currently selected viewmode
        /// </summary>
        /// <returns>The options which should be used for the current view</returns>
        private ThumbnailFetchOptions GetThumbnailFetchOptions()
        {
            //Set thumbnail size depending in which viewmode the user is
            //but only when it has been set, else enable TableView
            uint thumbnailSize = 20;
            var mode = ThumbnailMode.ListView;
            if (ViewMode != null && ViewMode.Type == ThumbnailMode.PicturesView)
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
        private void LoadFolder(FileSystemElement fse)
        {
            try
            {
                BrowserService.CancelLoading();

                var options = GetThumbnailFetchOptions();
                BrowserService.LoadFolder(fse, options);

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
            LoadFolder(CurrentFolder);
        }

        #region File CRUD Methods

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

            Dialog = Dialogs.ShowEditDialog("Rename", "Rename", "Cancel", SelectedItems[0].Name, userInputName =>
            {
                for (int i = 0; i < SelectedItems.Count; i++)
                {
                    var newName = SelectedItems.Count == 1 ? userInputName : $"{userInputName}_{i + 1}";

                    BrowserService.RenameFileSystemElement(SelectedItems[i], newName);

                    SelectedItems[i].Name = newName;
                    SelectedItems[i].Path = SelectedItems[i].Path.Substring(0, SelectedItems[i].Path.LastIndexOf("\\") + 1) + newName;
                }
            });
        }

        /// <summary>
        /// Deletes the currently selected file system element
        /// </summary>
        public void DeleteStorageItemSelected()
        {
            while (SelectedItems.Count > 0)
            {
                BrowserService.DeleteFileSystemElement(SelectedItems[0]);
                Notifications.Show($"Deleted {SelectedItems[0].Name}", Symbol.Delete, "Undo", () => { });
                SelectedItems.RemoveAt(0);
            }

        }

        public void CopyStorageItemSelected()
        {
            BrowserService.CopyFileSystemElement(SelectedItems);
        }

        public void CutStorageItemSelected()
        {
            BrowserService.CutFileSystemElement(SelectedItems);
        }

        public void PasteStorageItemSelected()
        {
            BrowserService.PasteFileSystemElement(CurrentFolder);
        }

        public void PasteStorageItem(IEnumerable<IStorageItem> items)
        {
            //BrowserService.PasteFileSystemElement()
        }

        public async void CreateFolder()
        {
            Dialog = Dialogs.ShowEditDialog("Create Folder", "Create", "Cancel", "", folderName =>
            {
                if (folderName != null) BrowserService.CreateFolder(folderName);
            });
        }

        public async void CreateFile()
        {
            Dialog = Dialogs.ShowEditDialog("Create File", "Create", "Cancel", "", fileName =>
            {
                if (fileName != null) BrowserService.CreateFile(fileName);
            });
        }

        /// <summary>
        /// Shows a popup with the properties of the selected file system element
        /// </summary>
        public async void ShowPropertiesStorageItemSelected()
        {
            if (SelectedItems.Count != 1) return;

            var fse = SelectedItems[0];
            Dialog = await Dialogs.ShowPropertiesDialog(fse, async x =>
            {
                await FileSystem.RenameStorageItemAsync(fse, fse.Name);
            });
        }

        public void ShowHistoryStorageItemSelected()
        {
            var selectedElement = SelectedItems[0];

            var history = HistoryService.Instance.GetHistory(selectedElement);
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
            BrowserService.RefetchThumbnails(options);
        }


        public async void UpdatePathSuggestions()
        {
            //Skip if Path has not been initialized or not focused
            if (!TextBoxPathIsFocused || (Path == null || Path == "")) return;

            var folders = Path.Split('\\');
            var searchString = folders[folders.Length - 1].ToLower();

            string path = Path;
            if (folders.Length > 1) path = Path.Substring(0, Path.LastIndexOf("\\"));

            try
            {
                var folderContent = await FileSystem.GetFolderContentSimple(path);
                PathSuggestions.Clear();
                foreach (FileSystemElement fse in folderContent)
                {
                    if (fse.Name.ToLower().Contains(searchString))
                        PathSuggestions.Add(fse);
                }

                if (PathSuggestions.Count == 1 && PathSuggestions[0].Name.ToLower() == searchString) PathSuggestions.RemoveAt(0);
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

        public void DropStorageItem(FileSystemElement droppedTo, IEnumerable<IStorageItem> droppeditems)
        {
            BrowserService.DropStorageItems(droppedTo, droppeditems);
        }

        public void DragStorageItemSelected(DataPackage dataPackage, DragUI dragUI)
        {
            if (SelectedItems.Count == 0) return;

            BrowserService.DragStorageItems(dataPackage, dragUI, SelectedItems);
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
                case VirtualKey.A:
                    if (SelectedItems.Count == FileSystemElements.Count)
                        SelectedItems.Clear();
                    else
                    {
                        for (int i = 0; i < FileSystemElements.Count; i++)
                        {
                            if (!SelectedItems.Contains(FileSystemElements[i]))
                                SelectedItems.Add(FileSystemElements[i]);
                        }
                    }

                    break;
            }
        }
    }
}
