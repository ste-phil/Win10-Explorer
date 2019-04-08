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

namespace Explorer.Controls
{
    public sealed partial class FileBrowser : UserControl
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

        public event FSEEventHandler FavoriteAdded;

        public FileBrowserModel ViewModel
        {
            get { return (FileBrowserModel) GetValue(ViewModelProperty); }
            set
            {
                SetValue(ViewModelProperty, value);

                if (ViewModel != null)
                {
                    ViewModel.FileBrowserWidth = ActualWidth;
                    ViewModel.RenameDialog = RenameDialog;

                    ViewModel.FavoriteAddRequested += (FileSystemElement fse) => FavoriteAdded?.Invoke(fse);
                    Bindings.Update();
                }
            }
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof (FileBrowserModel), typeof (FileBrowser), new PropertyMetadata(null));

        public FileBrowser()
        {
            this.InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = this;
        }

        private void OpenPowershell_Clicked(object sender, RoutedEventArgs e)
        {
            ViewModel.LaunchExe("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", $"-noexit -command \"cd {ViewModel.Path}\"");
        }

        private void TextBoxPath_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var chosen = (FileSystemElement) args.ChosenSuggestion;
            if (chosen != null)
                ViewModel.NavigateOrOpen(chosen);
            else
                ViewModel.NavigateTo(new FileSystemElement { Path = args.QueryText });
        }

        private void StorageTableView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            ViewModel.KeyDown(e.Key);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel != null) ViewModel.FileBrowserWidth = e.NewSize.Width;
        }
    }
}
