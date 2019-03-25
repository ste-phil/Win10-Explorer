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
using Windows.UI.Xaml.Media.Animation;
using Explorer.Controls;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.UI.Core;
using Windows.System.UserProfile;

namespace Explorer.Models
{
    public class MainPageModel : BaseModel
    {
        private object addDriveLock = new object();
        private CoreDispatcher dispatcher;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public FileBrowserModel currentFileBrowser;

        public ObservableCollection<object> NavigationItems { get; set; }
        public ObservableCollection<FileBrowserModel> FileBrowserModels { get; set; }

        public MainPageModel()
        {
            NavigationItems = new ObservableCollection<object>();
            FileBrowserModels = new ObservableCollection<FileBrowserModel>();

            AddTabCommand = new Command(x => FileBrowserModels.Add(new FileBrowserModel()), () => true);
            dispatcher = Window.Current.CoreWindow.Dispatcher;

            AddKnownFoldersToNavigation();
            AddDrivesToNavigationAsync();

            var dws = DeviceWatcherService.Instance;
            dws.DeviceChanged += async (s, e) => await AddDrivesToNavigationAsync();
        }

        public FileBrowserModel CurrentFileBrowser
        {
            get { return currentFileBrowser; }
            set { currentFileBrowser = value; OnPropertyChanged();}
        }

        public FileBrowserModel SelectedTab
        {
            get { return CurrentFileBrowser; }
            set { CurrentFileBrowser = value; }
        }

        public Command AddTabCommand { get; }

        private void AddKnownFoldersToNavigation()
        {
            NavigationItems.Add(new NavigationLink(Symbol.GoToStart, "Desktop", EnvironmentPaths.DesktopPath));
            NavigationItems.Add(new NavigationLink(Symbol.Download, "Downloads", EnvironmentPaths.DownloadsPath));
            NavigationItems.Add(new NavigationLink(Symbol.Document, "Documents", EnvironmentPaths.DocumentsPath));
            NavigationItems.Add(new NavigationLink(Symbol.Pictures, "Pictures", EnvironmentPaths.PicturesPath));
            NavigationItems.Add(new NavigationLink(Symbol.Video, "Videos", EnvironmentPaths.VideosPath));
            NavigationItems.Add(new NavigationLink(Symbol.MusicInfo, "Music", EnvironmentPaths.MusicPath));
            NavigationItems.Add(new NavigationLink((Symbol)0xE753, "OneDrive Personal", EnvironmentPaths.OneDrivePath));

            NavigationItems.Add(new NavigationViewItemSeparator());
        }

        private async Task AddDrivesToNavigationAsync()
        {
            await semaphore.WaitAsync();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
            {
                //Remove only drive navigation items
                var staticNavigationIndices = 9;
                for (int i = staticNavigationIndices; i < NavigationItems.Count; i++)
                {
                    NavigationItems.RemoveAt(staticNavigationIndices);
                }

                var drives = await FileSystem.GetDrivesAsync();
                for (int i = 0; i < drives.Length; i++)
                {
                    NavigationItems.Add(drives[i]);
                }

                semaphore.Release();
            });
        }

        public void NavigateNavigationFSE(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var item = ((NavigationViewItem)args.InvokedItemContainer).Tag;
            if (item is Drive drive)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = drive.RootDirectory, Name = drive.Name });
            }
            else if (item is NavigationLink link)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = link.Path, Name = link.Name });
            }
        }
    }
}