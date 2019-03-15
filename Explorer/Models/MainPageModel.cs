using System;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Explorer.Models
{
    public class MainPageModel : BaseModel
    {
        public ObservableCollection<NavigationViewItemBase> NavigationItems { get; set; }


        private string path;
        private FileSystemElement currentFolder;

        private int historyPosition;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }


        public List<FileSystemElement> History { get; set; }

        public MainPageModel()
        {
            NavigationItems = new ObservableCollection<NavigationViewItemBase>();
            FileSystemElements = new ObservableCollection<FileSystemElement>();
            CurrentFolder = new FileSystemElement { Path = "C:" };

            NavigateBack = new Command(x => NavigateToNoHistory(History[--HistoryPosition]), () => HistoryPosition > 0);
            NavigateForward = new Command(x => NavigateToNoHistory(History[++HistoryPosition]), () => HistoryPosition < History.Count - 1);

            History = new List<FileSystemElement>();
            HistoryPosition = -1;

            AddDrivesToNavigation();
            NavigateTo(CurrentFolder);
        }

        public FileSystemElement CurrentFolder
        {
            get { return currentFolder; }
            private set { currentFolder = value; OnPropertyChanged(); OnPropertyChanged("Path"); }
        }

        public string Path
        {
            get { return path; }
            set { path = value; OnPropertyChanged(); }
        }

        public int HistoryPosition
        {
            get { return historyPosition; }
            set { historyPosition = value; OnPropertyChanged(); NavigateBack.CanExceuteChanged(); NavigateForward.CanExceuteChanged(); }
        }

        public Command NavigateBack { get; }

        public Command NavigateForward { get; }

        private void AddDrivesToNavigation()
        {
            var drives = FileSystem.GetDrives();

            for (int i = 0; i < drives.Length; i++)
            {
                //Inaccesible due to UWP permission stuff
                //var rootPath = $"{drives[i].VolumeLabel}:\\";
                var displayName = $"{drives[i].Name}"; //  ({rootPath})

                SymbolIcon icon;
                switch (drives[i].DriveType)
                {
                    case DriveType.Removable:
                        icon = new SymbolIcon((Symbol)0xE88E);
                        break;
                    case DriveType.CDRom:
                        icon = new SymbolIcon((Symbol)0xE958);
                        break;
                    case DriveType.Network:
                        icon = new SymbolIcon((Symbol)0xE969);
                        break;
                    default:
                        icon = new SymbolIcon((Symbol)0xEDA2);
                        break;
                }

                NavigationItems.Add(new NavigationViewItem { Tag = displayName, Content = displayName, Icon = icon});
            }
        }


        /// <summary>
        /// Navigate to the passed Folder
        /// </summary>
        /// <param name="fse"></param>
        public void NavigateTo(FileSystemElement fse)
        {
            Path = fse.Path;

            History.RemoveRange(HistoryPosition + 1, History.Count - (HistoryPosition + 1));
            History.Insert(HistoryPosition + 1, fse);
            HistoryPosition++;

            LoadFolderAsync(fse);
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
            catch (Exception e)
            {
                //await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));
                //Show error that path was not found
            }
        }

        /// <summary>
        /// Event called in UI that navigates to the passed FileSystemElement
        /// </summary>
        public void NavigateNextFSE(object sender, FileSystemElement fileSystemElement)
        {
            NavigateTo(fileSystemElement);
        }

        public void NavigateNavigationFSE(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var path = args.InvokedItemContainer.Tag.ToString();

            NavigateTo(new FileSystemElement {Path = path});
        }
    }
}