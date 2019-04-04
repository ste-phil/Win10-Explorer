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

namespace Explorer.Controls
{
    public sealed partial class FileBrowser : UserControl
    {
        public static ViewMode DefaultViewMode = new ViewMode(ThumbnailMode.ListView, "", null);

        public class ViewMode
        {
            public ThumbnailMode Type { get; set; }
            public string Icon { get; set; }
            public FrameworkElement Element { get; set; }

            public ViewMode(ThumbnailMode type, string icon, FrameworkElement element)
            {
                Type = type;
                Icon = icon;
                Element = element;
            }
        }

        public event FSEEventHandler FavoriteAdded;

        private ViewMode[] viewModes;

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
                    ViewModel.ViewModes = viewModes;

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

            viewModes = new ViewMode[]
            {
                new ViewMode(ThumbnailMode.ListView, "\uF0E2", TableView),
                new ViewMode(ThumbnailMode.PicturesView, "\uE8FD", GridView),
            };

            ((FrameworkElement) this.Content).DataContext = this;
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
