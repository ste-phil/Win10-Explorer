using Explorer.Entities;
using Explorer.Helper;
using Explorer.Models;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Explorer.Controls
{
    public sealed partial class PathBox : UserControl
    {
        public event EventHandler<string> NavigationRequested;

        #region Dependency Properties
        public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
            "Path", typeof(string), typeof(PathBox), new PropertyMetadata(null));

        public static readonly DependencyProperty IsTextBoxPathEnabledProperty = DependencyProperty.Register(
            "IsTextBoxPathEnabled", typeof(bool), typeof(PathBox), new PropertyMetadata(false));

        #endregion

        private Visibility textPathVisibility;
        private Visibility folderListVisibility;
        private ObservableCollection<Tuple<int, string>> openedFolders;

        public PathBox()
        {
            TextPathVisibility = Visibility.Visible;
            this.InitializeComponent();

            openedFolders = new ObservableCollection<Tuple<int, string>>();
            TextPathVisibility = Visibility.Collapsed;
            FolderListVisibility = Visibility.Visible;
        }

        #region Properties
        public ObservableCollection<FileSystemElement> PathSuggestions { get; set; }

        public string Path
        {
            get { return (string)GetValue(PathProperty); }
            set { SetValue(PathProperty, value); UpdatePathFolders(); }
        }

        public bool IsTextBoxPathEnabled
        {
            get { return (bool)GetValue(IsTextBoxPathEnabledProperty); }
            set { SetValue(IsTextBoxPathEnabledProperty, value); }
        }

        public Visibility TextPathVisibility
        {
            get { return textPathVisibility; }
            set { textPathVisibility = value; }
        }

        public Visibility FolderListVisibility
        {
            get { return folderListVisibility; }
            set { folderListVisibility = value; }
        }
        #endregion

        public void UpdatePathFolders()
        {
            var pathItems = Path.Split("\\", StringSplitOptions.RemoveEmptyEntries);

            openedFolders.Clear();
            for (int i = 0; i < pathItems.Length; i++)
            {
                openedFolders.Add(new Tuple<int, string>(i, pathItems[i]));
            }
        }

        public void NavigateToFolder(int openedFolderIndex)
        {
            var path = string.Join("\\", openedFolders.Select(s => s.Item2).ToArray(), 0, openedFolderIndex + 1);

            NavigationRequested?.Invoke(this, path);
        }

        #region Events

        private void Folder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var btn = (Button)sender;

            var index = (int)btn.Tag;
            NavigateToFolder(index);

            e.Handled = true;
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.Handled) return;

            TextPathVisibility = Visibility.Visible;
            FolderListVisibility = Visibility.Collapsed;
            IsTextBoxPathEnabled = true;
            Bindings.Update();

            TextBoxPath.LayoutUpdated += TextBoxPath_LayoutUpdated;
        }

        private void TextBoxPath_LayoutUpdated(object sender, object e)
        {
            TextBoxPath.Focus(FocusState.Programmatic);
            TextBoxPath.LayoutUpdated -= TextBoxPath_LayoutUpdated;
        }

        private void TextPath_LostFocus(object sender, RoutedEventArgs e)
        {
            TextPathVisibility = Visibility.Collapsed;
            FolderListVisibility = Visibility.Visible;
            IsTextBoxPathEnabled = false;
            
            Bindings.Update();
        }

        private void TextBoxPath_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var chosen = (FileSystemElement)args.ChosenSuggestion;
            if (chosen != null)
                NavigationRequested?.Invoke(this, chosen.Path);
            else
                NavigationRequested?.Invoke(this, args.QueryText);
        }

        #endregion

    }
}
