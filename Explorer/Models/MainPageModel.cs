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

namespace Explorer.Models
{
    public class MainPageModel : BaseModel
    {
        public FileBrowserModel currentFileBrowser;

        public ObservableCollection<object> NavigationItems { get; set; }
        public ObservableCollection<FileBrowserModel> FileBrowserModels { get; set; }

        public MainPageModel()
        {
            NavigationItems = new ObservableCollection<object>();
            FileBrowserModels = new ObservableCollection<FileBrowserModel>();

            AddTabCommand = new Command(x => FileBrowserModels.Add(new FileBrowserModel()), () => true);

            AddDrivesToNavigationAsync();
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
            var drives = await FileSystem.GetDrives();
            for (int i = 0; i < drives.Length; i++)
            {
                NavigationItems.Add(drives[i]);
            }
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