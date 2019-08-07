using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Explorer.Entities;
using Explorer.Models;
using System.Diagnostics;
using Windows.Storage.FileProperties;
using System.Threading.Tasks;
using Windows.UI.Core;
using Explorer.Logic;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Explorer.Controls
{
    public sealed partial class FSEBrowser : UserControl
    {
        public event EventHandler<FileSystemElement> RequestedTabOpen;
        public event FSEEventHandler FavoriteAdded;

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

        public FSEBrowserModel ViewModel
        {
            get { return (FSEBrowserModel) GetValue(ViewModelProperty); }
            set
            {
                SetValue(ViewModelProperty, value);

                if (ViewModel != null)
                {
                    ViewModel.FileBrowserWidth = ActualWidth;
                    //ViewModel.RenameDialog = TextDialog;
                    ViewModel.DialogService = DialogService;

                    ViewModel.FavoriteAddRequested += (FileSystemElement fse) => FavoriteAdded?.Invoke(fse);
                    Bindings.Update();
                }
            }
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof (FSEBrowserModel), typeof (FSEBrowser), new PropertyMetadata(null));

        public DialogService DialogService { get; set; } = new DialogService();


        public FSEBrowser()
        {
            this.InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = this;

            DialogService.TextDialog = TextDialog;
        }

        private void OpenPowershell_Clicked(object sender, RoutedEventArgs e)
        {
            FileSystem.LaunchExeAsync("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", $"-noexit -command \"cd {ViewModel.Path}\"");
        }

        private void TextBoxPath_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var chosen = (FileSystemElement) args.ChosenSuggestion;
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


        private void TextBoxPath_GotFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.TextBoxPathIsFocused = true;
        }

        private void TextBoxPath_LostFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.TextBoxPathIsFocused = false;
        }
    }
}
