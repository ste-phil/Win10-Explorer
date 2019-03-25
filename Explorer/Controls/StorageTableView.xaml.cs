using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Explorer.Controls
{
    public sealed partial class StorageTableView : Page
    {
        private bool isResizing;
        private Point startPos;
        private int currentColumnIndex;

        private List<FrameworkElement> selectedElements;

        public StorageTableView()
        {
            this.InitializeComponent();

            selectedElements = new List<FrameworkElement>();

            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;

            ContentGrid.SizeChanged += ContentGrid_SizeChanged;
        }

        public MenuFlyout ItemFlyout { get; set; }
        public MenuFlyout MultipleItemFlyout { get; set; }

        public ObservableCollection<FileSystemElement> ItemsSource
        {
            get { return (ObservableCollection<FileSystemElement>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<FileSystemElement>),
        typeof(StorageTableView), new PropertyMetadata(DependencyProperty.UnsetValue));

        public ObservableCollection<FileSystemElement> SelectedItems
        {
            get { return (ObservableCollection<FileSystemElement>)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(nameof(SelectedItems), typeof(ObservableCollection<FileSystemElement>),
            typeof(StorageTableView), new PropertyMetadata(new ObservableCollection<FileSystemElement>()));

        public FileSystemElement DoubleTappedItem
        {
            get { return (FileSystemElement)GetValue(DoubleTappedItemProperty); }
            set { SetValue(DoubleTappedItemProperty, value); }
        }

        public static readonly DependencyProperty DoubleTappedItemProperty = DependencyProperty.Register(nameof(DoubleTappedItem), typeof(FileSystemElement),
            typeof(StorageTableView), new PropertyMetadata(DependencyProperty.UnsetValue));


        private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            for (int i = 0; i < ContentGrid.ColumnDefinitions.Count; i++)
            {
                HeaderGrid.ColumnDefinitions[i].Width = new GridLength(ContentGrid.ColumnDefinitions[i].ActualWidth);
                ContentGrid.ColumnDefinitions[i].Width = new GridLength(ContentGrid.ColumnDefinitions[i].ActualWidth);
            }

            ContentGrid.SizeChanged -= ContentGrid_SizeChanged;
        }

        private void HeaderColumn_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var fe = ((FrameworkElement)sender);
            var columnHeader = fe.Parent;

            fe.CapturePointer(e.Pointer);

            currentColumnIndex = (int) columnHeader.GetValue(Grid.ColumnProperty);
            startPos = Window.Current.CoreWindow.PointerPosition;
            isResizing = true;
        }

        private void HeaderColumn_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var fe = ((FrameworkElement)sender);

            isResizing = false;
            fe.ReleasePointerCapture(e.Pointer);
        }

        private void HeaderColumn_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isResizing)
            {
                var pointerPos = Window.Current.CoreWindow.PointerPosition;
                var width = HeaderGrid.ColumnDefinitions[currentColumnIndex].ActualWidth;

                var headerColumn = HeaderGrid.ColumnDefinitions[currentColumnIndex];
                var contentColumn = ContentGrid.ColumnDefinitions[currentColumnIndex];

                var newWidth = width + pointerPos.X - startPos.X;
                if (newWidth >= contentColumn.MinWidth)
                {
                    headerColumn.Width = new GridLength(newWidth);
                    contentColumn.Width = new GridLength(newWidth);
                    startPos = pointerPos;
                }
            }
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            //TODO: Add focus field to store which item is currently focused

            //if (ItemsSource.Count == 0) return;

            //var lastItemIndex = SelectedItems.Count == 0 ? -1 : ItemsSource.IndexOf(SelectedItems.Last());

            //if (args.VirtualKey == VirtualKey.Down && lastItemIndex + 1 < ItemsSource.Count)
            //    SelectRow(ItemsSource[lastItemIndex + 1]);
            //else if (args.VirtualKey == VirtualKey.Up && lastItemIndex - 1 >= 0)
            //    SelectRow(ItemsSource[lastItemIndex - 1]);

            //args.Handled = true;
        }

        private void Row_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            var shiftDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var controlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

            //Begin new selection
            if (!controlDown && !shiftDown)
                UnselectOldRows();

            if (shiftDown)
                SelectRowsBetween(item);
            else
                SelectRow(item, hitbox);
        }

        private void Row_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            var controlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (!SelectedItems.Contains(item))
            {
                if (!controlDown)
                    UnselectOldRows();

                SelectRow(item, hitbox);
            }

            if (SelectedItems.Count == 1) ItemFlyout.ShowAt(hitbox, e.GetPosition(hitbox));
            else MultipleItemFlyout.ShowAt(hitbox, e.GetPosition(hitbox));
        }

        private void Row_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            DoubleTappedItem = item;
        }

        private void SelectRow(FileSystemElement fse, FrameworkElement hitbox)
        {
            if (SelectedItems.Contains(fse))
            {
                DeselectRow(fse, hitbox);
                return;
            }

            SelectedItems.Add(fse);
            selectedElements.Add(hitbox);

            hitbox.Style = (Style)Resources["RowSelectedStyle"];
        }

        private void SelectRow(FileSystemElement fse)
        {
            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            var hitbox = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

            if (SelectedItems.Contains(fse))
            {
                DeselectRow(fse, hitbox);
                return;
            }

            SelectedItems.Add(fse);
            selectedElements.Add(hitbox);

            hitbox.Style = (Style)Resources["RowSelectedStyle"];
        }

        private void DeselectRow(FileSystemElement fse, FrameworkElement hitbox)
        {
            SelectedItems.Remove(fse);
            selectedElements.Remove(hitbox);

            hitbox.Style = (Style)Resources["RowDefaultStyle"];
        }

        private void SelectRowsBetween(FileSystemElement fse)
        {
            var lastTappedRowIndex = SelectedItems.Count == 0 ? -1 : ItemsSource.IndexOf(SelectedItems.Last());
            var tappedRowIndex = ItemsSource.IndexOf(fse);

            var from = Math.Min(lastTappedRowIndex, tappedRowIndex);
            var to = Math.Max(lastTappedRowIndex, tappedRowIndex);
            for (int i = from; i <= to; i++)
            {
                if (i == lastTappedRowIndex) continue;

                var item = ItemsSource[i];
                var container = (ContentPresenter) ItemsSourceRowHitbox.ContainerFromItem(item);
                var row = (FrameworkElement) VisualTreeHelper.GetChild(container, 0);

                if (!SelectedItems.Contains(item)) SelectRow(item, row);
            }
        }

        private void UnselectOldRows()
        {
            for (int i = 0; i < selectedElements.Count; i++)
            {
                selectedElements[i].Style = (Style)Resources["RowDefaultStyle"];
            }

            selectedElements.Clear();
            SelectedItems.Clear();
        }
    }
}
