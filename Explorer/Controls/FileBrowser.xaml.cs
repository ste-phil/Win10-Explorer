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
using Windows.System;
using System.Diagnostics;

namespace Explorer.Controls
{
    public sealed partial class FileBrowser : UserControl
    {
        public event FSEEventHandler FavoriteAdded;

        public FileBrowserModel ViewModel
        {
            get { return (FileBrowserModel) GetValue(ViewModelProperty); }
            set 
            { 
                SetValue(ViewModelProperty, value); 
                Bindings.Update(); 

                if (ViewModel != null)
                {
                    ViewModel.RenameDialog = RenameDialog;
                    ViewModel.FavoriteAddRequested += (FileSystemElement fse) => FavoriteAdded?.Invoke(fse);
                }
            }
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof (FileBrowserModel), typeof (FileBrowser), new PropertyMetadata(null));


        public FileBrowser()
        {
            this.InitializeComponent();
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
    }
}
