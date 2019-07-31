using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Explorer.Models
{
    public class MainPageModel : BaseModel
    {
        private const string FAVORITE_FILE_NAME = "favs";

        private CoreDispatcher dispatcher;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private FileBrowserModel currentFileBrowser;
        private string searchText;

        public MainPageModel()
        {
            NavigationItems = new ObservableCollection<object>();
            FileBrowserModels = new ObservableCollection<FileBrowserModel>();
            FileBrowserModels.CollectionChanged += FileBrowserModels_CollectionChanged;
            Favorites = new ObservableRangeCollection<FavoriteNavigationLink>();
            Favorites.CollectionChanged += Favorites_CollectionChanged;

            AddTabCmd = new Command(() => OpenTab(@switch: true), () => true);
            LaunchUrl = new GenericCommand<string>(async url => await Launcher.LaunchUriAsync(new Uri(url)), url => true);
            dispatcher = Window.Current.CoreWindow.Dispatcher;

            FavNavLinkUpCmd = new GenericCommand<FavoriteNavigationLink>(x => MoveUpFavorite(x), x => Favorites.IndexOf(x) > 0);
            FavNavLinkDownCmd = new GenericCommand<FavoriteNavigationLink>(x => MoveDownFavorite(x), x => Favorites.IndexOf(x) < Favorites.Count - 1);
            FavNavLinkRemoveCmd = new GenericCommand<FavoriteNavigationLink>(x => RemoveFavorite(x), x => true);

            InitNavigationAsync();

            var dws = DeviceWatcherService.Instance;
            dws.DeviceChanged += (s, e) => AddDrivesToNavigation();
        }

        private void FileBrowserModels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("AllowCloseTabs");
        }

        #region Properties

        public ObservableRangeCollection<FavoriteNavigationLink> Favorites { get; set; }
        public ObservableCollection<object> NavigationItems { get; set; }
        public ObservableCollection<FileBrowserModel> FileBrowserModels { get; set; }
        public bool AllowCloseTabs => FileBrowserModels.Count > 1;

        public string SearchText
        {
            get { return searchText; }
            set {
                searchText = value;
                OnPropertyChanged();
                CurrentFileBrowser.SearchFolder(searchText);
            }
        }

        public FileBrowserModel CurrentFileBrowser
        {
            get { return currentFileBrowser; }
            set { currentFileBrowser = value; OnPropertyChanged(); OnPropertyChanged("SearchPlaceholder"); }
        }

        public FileBrowserModel SelectedTab
        {
            get { return CurrentFileBrowser; }
            set { CurrentFileBrowser = value; }
        }

        public ICommand AddTabCmd { get; }
        public ICommand LaunchUrl { get; }
        public GenericCommand<FavoriteNavigationLink> FavNavLinkUpCmd { get; }
        public GenericCommand<FavoriteNavigationLink> FavNavLinkDownCmd { get; }
        public GenericCommand<FavoriteNavigationLink> FavNavLinkRemoveCmd { get; }

        #endregion

        private void InitNavigationAsync()
        {
            _ = LoadFavoritesAsync();
            AddKnownFoldersToNavigation();
            AddDrivesToNavigation();
        }

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

        public void OpenTab(FileSystemElement directory = null, bool @switch = false)
        {
            var newTab = directory == null ? new FileBrowserModel() : new FileBrowserModel(directory);
            FileBrowserModels.Add(newTab);

            if (@switch)
            {
                CurrentFileBrowser = newTab;
                OnPropertyChanged("SelectedTab");
            }
        }

        
        public void CloseTab(FileBrowserModel fbm)
        {
            FileBrowserModels.Remove(fbm);
            if (fbm == CurrentFileBrowser)
            {
                CurrentFileBrowser = FileBrowserModels[FileBrowserModels.Count - 1];
                SelectedTab = FileBrowserModels[FileBrowserModels.Count - 1];
            }
        }

        public void CloseCurrentTab()
        {
            CloseTab(CurrentFileBrowser);
        }


        private async void AddDrivesToNavigation()
        {
            await semaphore.WaitAsync();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                //Count favorites, if favorites are more than 0 add one for the seperator
                var favoriteLinkCount = Favorites.Count != 0 ? Favorites.Count + 1 : 0;

                //Remove only drive navigation items
                var staticNavigationIndices = 8 + favoriteLinkCount;
                while (staticNavigationIndices < NavigationItems.Count)
                {
                    NavigationItems.RemoveAt(staticNavigationIndices);
                }

                var drives = await FileSystem.GetDrivesAsync();
                for (int i = 0; i < drives.Length; i++)
                {
                    NavigationItems.Add(drives[i]);
                }

                //Redirect to first drive (mainly windows) when current drive is not available anymore
                if (CurrentFileBrowser != null && !FileSystem.DirectoryExists(CurrentFileBrowser.Path))
                {
                    var drive = drives[0];

                    CurrentFileBrowser.ClearHistory();
                    CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = drive.RootDirectory, Name = drive.Name });
                }

                semaphore.Release();
            });
        }

        private async Task LoadFavoritesAsync()
        {
            var favs = await FileSystem.DeserializeObject<List<FavoriteNavigationLink>>(FAVORITE_FILE_NAME) ?? new List<FavoriteNavigationLink>();
            foreach (var fav in favs)
            {
                fav.MoveDownCommand = FavNavLinkDownCmd;
                fav.MoveUpCommand = FavNavLinkUpCmd;
                fav.RemoveCommand = FavNavLinkRemoveCmd;
            }

            for (int i = 0; i < favs.Count; i++)
            {
                NavigationItems.Insert(i, favs[i]);
            }

            Favorites.AddRange(favs);
        }

        #region Favorites

        private void Favorites_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            StoreFavorites();
            FavNavLinkUpCmd.CanExceuteChanged();
            FavNavLinkDownCmd.CanExceuteChanged();

            //If you change the Favorites Collection make sure you update the NavigationItems Collection first
            if (e.Action == NotifyCollectionChangedAction.Remove && Favorites.Count == 0) NavigationItems.RemoveAt(0);
            else if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0) NavigationItems.Insert(Favorites.Count, new NavigationViewItemSeparator());
        }

        private void StoreFavorites()
        {
            FileSystem.SerializeObject(Favorites, FAVORITE_FILE_NAME);
        }

        public void AddFavorite(FileSystemElement fse)
        {
            var favLink = new FavoriteNavigationLink(Symbol.OutlineStar, fse.Name, fse.Path, FavNavLinkUpCmd, FavNavLinkDownCmd, FavNavLinkRemoveCmd);

            NavigationItems.Insert(Favorites.Count, favLink);
            Favorites.Add(favLink);
        }

        public void RemoveFavorite(FileSystemElement fse)
        {
            var index = Favorites.IndexOf(x => x.Path == fse.Path);

            NavigationItems.RemoveAt(index);
            Favorites.RemoveAt(index);
        }

        private void RemoveFavorite(FavoriteNavigationLink fnl)
        {
            NavigationItems.Remove(fnl);
            Favorites.Remove(fnl);
        }

        private void MoveUpFavorite(FavoriteNavigationLink fnl)
        {
            var index = Favorites.IndexOf(fnl);

            NavigationItems.Remove(fnl);
            NavigationItems.Insert(index - 1, fnl);

            Favorites.MoveUp(fnl);
        }

        private void MoveDownFavorite(FavoriteNavigationLink fnl)
        {
            var index = Favorites.IndexOf(fnl);

            NavigationItems.Remove(fnl);
            NavigationItems.Insert(index + 1, fnl);

            Favorites.MoveDown(fnl);
        }

        #endregion

        public void FileBrowser_RequestedTabOpen(object sender, FileSystemElement e)
        {
            FileBrowserModels.Add(new FileBrowserModel(e));
        }

        public void NavigateNavigationFSE(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = ((NavigationViewItem)args.SelectedItemContainer).Tag;
            if (item is Drive drive)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = drive.RootDirectory, Name = drive.Name });
            }
            else if (item is NavigationLink link)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = link.Path, Name = link.Name });
            }
            else if (item is FavoriteNavigationLink favLink)
            {
                CurrentFileBrowser.NavigateTo(new FileSystemElement { Path = favLink.Path, Name = favLink.Name });
            }
        }
    }
}