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

        private async Task AddDrivesToNavigationAsync()
        {
            await semaphore.WaitAsync();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
            { 
                NavigationItems.Clear();
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
            var item = ((StackPanel)args.InvokedItem).Tag;
            if (item is Drive drive)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = drive.RootDirectory, Name = drive.Name });
            }
        }
    }
}