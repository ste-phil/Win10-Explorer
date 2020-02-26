using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic.FileSystemService;
using Explorer.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Explorer.Controls
{
    public sealed partial class FSEBrowser : UserControl
    {
        public class ViewMode : ObservableEntity
        {
            private ThumbnailMode type;
            private string icon;
            private Visibility visibility;

            public ThumbnailMode Type
            {
                get { return type; }
                set { type = value; OnPropertyChanged(); }
            }

            public string Icon
            {
                get { return icon; }
                set { icon = value; OnPropertyChanged(); }
            }

            public Visibility Visibility
            {
                get { return visibility; }
                set { visibility = value; OnPropertyChanged(); }
            }

            public ViewMode(ThumbnailMode type, string icon, Visibility visibility = Visibility.Collapsed)
            {
                Type = type;
                Icon = icon;
                Visibility = visibility;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<FileSystemElement> RequestedTabOpen;
        public event FSEEventHandler FavoriteAdded;

        private MenuFlyout browserMenuFlyout;
        private MenuFlyout browserMultipleMenuFlyout;
        private MenuFlyout browserBackgrondMenuFlyout;

        public FSEBrowser()
        {
            this.InitializeComponent();
            ((FrameworkElement)Content).DataContext = this;
        }

        #region Properties
        #region Dependency Properties
        public FSEBrowserModel ViewModel
        {
            get { return (FSEBrowserModel)GetValue(ViewModelProperty); }
            set
            {
                SetValue(ViewModelProperty, value);

                if (ViewModel != null)
                {
                    ViewModel.FileBrowserWidth = ActualWidth;
                    ViewModel.FavoriteAddRequested += (FileSystemElement fse) => FavoriteAdded?.Invoke(fse);
                    ViewModel.BrowserFeaturesChanged += ViewModel_BrowserFeaturesChanged;
                    Bindings.Update();
                }
            }
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(FSEBrowserModel), typeof(FSEBrowser), new PropertyMetadata(null));

        #endregion

        //public MenuFlyout BrowserMenuFlyout
        //{
        //    get => browserMenuFlyout;
        //    set { browserMenuFlyout = value; OnPropertyChanged(); }
        //}

        //public MenuFlyout BrowserMultipleMenuFlyout
        //{
        //    get => browserMultipleMenuFlyout;
        //    set { browserMultipleMenuFlyout = value; OnPropertyChanged(); }
        //}

        //public MenuFlyout BrowserBackgrondMenuFlyout
        //{
        //    get => browserBackgrondMenuFlyout;
        //    set { browserBackgrondMenuFlyout = value; OnPropertyChanged(); }
        //}

        #endregion

        #region Feature check
        private void ViewModel_BrowserFeaturesChanged(Features ops)
        {
            var notSupportedFeatureFlags = ((~ops) & FeatureBuilder.All).GetIndividualFlags().ToArray();
            TableView.ItemFlyout = RemoveMenuFlyoutFeatures(notSupportedFeatureFlags, (MenuFlyout)Resources["DefaultBrowserFlyout"]);
            TableView.MultipleItemFlyout = RemoveMenuFlyoutFeatures(notSupportedFeatureFlags, (MenuFlyout)Resources["DefaultBrowserMultipleFlyout"]);
            TableView.BackgroundFlyout = RemoveMenuFlyoutFeatures(notSupportedFeatureFlags, (MenuFlyout)Resources["DefaultBrowserBackgroundFlyout"]);

            //TODO: BUILD UI DEPENDENT TO FEATURES
        }

        private MenuFlyout RemoveMenuFlyoutFeatures(Enum[] featuresToRemove, MenuFlyout flyout)
        {
            var x = new List<MenuFlyoutItemBase>();
            foreach (MenuFlyoutItemBase item in flyout.Items)
            {
                for (int i = 0; i < featuresToRemove.Count(); i++)
                {
                    if ((string)item.Tag == $"Feature_{featuresToRemove[i].ToString()}") x.Add(item);
                }
            }

            for (int i = 0; i < x.Count; i++)
            {
                flyout.Items.Remove(x[i]);
            }


            //Check if seperators are useless now
            bool removed = true;
            while (removed)
            {
                removed = false;

                for (int i = 0; i < flyout.Items.Count; i++)
                {
                    if (flyout.Items[i] is MenuFlyoutSeparator && (flyout.Items[i + 1] is MenuFlyoutSeparator || i == flyout.Items.Count - 1))
                    {
                        flyout.Items.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
            

            //Add Item to indicate that no features are available
            if (flyout.Items.Count == 0)
            {
                flyout.Items.Add(new MenuFlyoutItem { Text = "No actions are available", Icon = new SymbolIcon(Symbol.Important) });
            }

            return flyout;
        }
        #endregion

        private void OpenPowershell_Clicked(object sender, RoutedEventArgs e)
        {
            FileSystem.LaunchExeAsync("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", $"-noexit -command \"cd {ViewModel.Path}\"");
        }

        private void TextBoxPath_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var chosen = (FileSystemElement)args.ChosenSuggestion;
            if (chosen != null)
                ViewModel.NavigateOrOpen(chosen);
            else
                ViewModel.NavigateOrOpen(args.QueryText);
        }

        private void StorageTableView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            ViewModel.KeyDown(e.Key);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel != null) ViewModel.FileBrowserWidth = e.NewSize.Width;
        }

        private void TableView_RequestedTabOpen(object sender, FileSystemElement e)
        {
            RequestedTabOpen?.Invoke(sender, e);
        }

        private void TableView_DoubleTappedItem(object sender, FileSystemElement e)
        {
            ViewModel.NavigateOrOpen(e);
        }

        private void PathBox_NavigationRequested(object sender, string path)
        {
            ViewModel.NavigateOrOpen(path);
        }

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
    }
}
