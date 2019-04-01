using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Windows.UI.Xaml.Shapes;

namespace Explorer.Controls
{
    public sealed partial class StorageTableView : Page
    {
        private const string ROW_SELECTED_STYLE_NAME = "RowSelected";
        private const string ROW_DEFAULT_STYLE_NAME = "RowDefault";

        private bool isResizing;
        private Point startPos;
        private int currentColumnIndex;

        private List<FrameworkElement> selectedElements;
        private FrameworkElement focusedRow;

        public StorageTableView()
        {
            this.InitializeComponent();

            selectedElements = new List<FrameworkElement>();

            PreviewKeyDown += Window_KeyDown;
            ContentGrid.SizeChanged += ContentGrid_SizeChanged;

            GotFocus += StorageTableView_GotFocus;
            LostFocus += StorageTableView_LostFocus;
        }

        #region Properties

        public MenuFlyout ItemFlyout { get; set; }
        public MenuFlyout MultipleItemFlyout { get; set; }
        public MenuFlyout BackgroundFlyout { get; set; }
        public FileSystemElement FocusedItem { get; set; }

        public ObservableCollection<FileSystemElement> ItemsSource
        {
            get { return (ObservableCollection<FileSystemElement>)GetValue(ItemsSourceProperty); }
            set
            {
                if (ItemsSource != null) ItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
                SetValue(ItemsSourceProperty, value);
                ItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
            }
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

        #endregion

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                UnselectOldRows();
            }
        }

        #region TableHeader Actions

        private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            for (int i = 0; i < ContentGrid.ColumnDefinitions.Count; i++)
            {
                //Shrinken down name column by the size of the NavigatioViewPanel
                if (i == 1)
                {
                    HeaderGrid.ColumnDefinitions[i].Width = new GridLength(ContentGrid.ColumnDefinitions[i].ActualWidth - 200);
                    ContentGrid.ColumnDefinitions[i].Width = new GridLength(ContentGrid.ColumnDefinitions[i].ActualWidth - 200);
                    continue;
                }

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

        #endregion

        private void StorageTableView_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ItemsSource != null && ItemsSource.Count > 0 && FocusedItem == null) FocusRow(ItemsSource[0]);
        }

        private void StorageTableView_LostFocus(object sender, RoutedEventArgs e)
        {
            RemoveFocus(FocusedItem);
        }

        private void Window_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (ItemsSource.Count == 0) return;

            var shiftDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var ctrlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            switch (args.Key)
            {
                //Abort selection
                case VirtualKey.Escape:
                    UnselectOldRows();
                    break;
                //Select currently focused item
                case VirtualKey.Space:
                    if (shiftDown) SelectRowsBetween(FocusedItem);
                    else SelectRow(FocusedItem);
                    break;
                //Open/Navigate currently focused item
                case VirtualKey.Enter:
                    DoubleTappedItem = FocusedItem;
                    break;
                case VirtualKey.Menu:
                    OpenItemFlyout(FocusedItem, focusedRow, new Point(0, focusedRow.Height));
                    break;
                //Move focus to next/previous element and keep it in view
                case VirtualKey.Up:
                case VirtualKey.Down:
                case VirtualKey.W:
                case VirtualKey.S:
                    //If there is a focused item take index of it
                    //If there is no focused item and no SelectedItems begin from start
                    //If there is no focused item but SelectedItems get the index of the last
                    var lastFocusedIndex = FocusedItem == null ?
                        SelectedItems.Count == 0 ? -1 : ItemsSource.IndexOf(SelectedItems.Last())
                        : ItemsSource.IndexOf(FocusedItem);

                    var up = args.Key == VirtualKey.Up || args.Key == VirtualKey.W;

                    var index = up ? lastFocusedIndex - 1 : lastFocusedIndex + 1;                  //Find next index depending on which button has been pressed
                    var condBoundings = up ? index >= 0 : index < ItemsSource.Count;               //Check bounding 0 <= index < ItemsSource.Count 
                    
                    if (!condBoundings) break;                                  //Cancel move focus if next item would be out of bounds
                    if (ctrlDown)
                    {
                        //Select next row if ctrl was pressed
                        SelectRow(ItemsSource[index]);            

                        //Select LastFocusedItem if its not selected
                        if (!SelectedItems.Contains(ItemsSource[lastFocusedIndex]))
                            SelectRow(ItemsSource[lastFocusedIndex]);
                    }

                    FocusRow(ItemsSource[index]);

                    args.Handled = true;                                    //Set event as handled (Prevents ScrollViewer from scrolling down/up)
                    break;
            }

            //Don't find FileSystemElemets which start with key pressed if modifiers have been pressed
            if (ctrlDown || shiftDown) return;

            var focusItemIndex = ItemsSource.IndexOf(FocusedItem);
            var key = args.Key.ToString().ToLower();
            var firstItemIndex = -1;
            var foundNextItem = false;
            for (int i = 0; i < ItemsSource.Count; i++)
            {
                if (ItemsSource[i].Name.ToLower().StartsWith(key))
                {
                    //To begin from first item again if last item with the key was found
                    if (firstItemIndex == -1) firstItemIndex = i;

                    //Needs to be after currently focused item
                    if (i <= focusItemIndex) continue;

                    UnselectOldRows();
                    SelectRow(ItemsSource[i]);
                    FocusRow(ItemsSource[i]);
                    foundNextItem = true;
                    break;
                }
            }

            if (!foundNextItem && firstItemIndex != -1)
            {
                UnselectOldRows();
                SelectRow(ItemsSource[firstItemIndex]);
                FocusRow(ItemsSource[firstItemIndex]);
            }
        }

        private void Row_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            var shiftDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var controlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

            FocusRow(item);

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

            FocusRow(item);
            if (!SelectedItems.Contains(item))
            {
                if (!controlDown)
                    UnselectOldRows();

                SelectRow(item, hitbox);
            }

            OpenItemFlyout(item, hitbox, e.GetPosition(hitbox));
        }

        private void Row_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            DoubleTappedItem = item;
        }

        private void OpenBackgroundFlyout(object sender, RightTappedRoutedEventArgs e)
        {
            var s = (FrameworkElement)sender;
            BackgroundFlyout.ShowAt(s, e.GetPosition(s));
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

            //When item which is going to be selected and is focused apply different style
            if (fse == FocusedItem) hitbox.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else hitbox.Style = (Style)Resources["RowSelectedStyle"];
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

            StyleHitbox(fse, hitbox, ROW_SELECTED_STYLE_NAME);
        }


        private void DeselectRow(FileSystemElement fse, FrameworkElement hitbox)
        {
            SelectedItems.Remove(fse);
            selectedElements.Remove(hitbox);

            StyleHitbox(fse, hitbox, ROW_DEFAULT_STYLE_NAME);
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
                var fse = SelectedItems[i];
                var hitbox = selectedElements[i];

                StyleHitbox(fse, hitbox, ROW_DEFAULT_STYLE_NAME);
            }

            selectedElements.Clear();
            SelectedItems.Clear();
        }

        private void StyleHitbox(FileSystemElement fse, FrameworkElement hitbox, string style)
        {
            if (fse == FocusedItem) hitbox.Style = (Style)Resources[style + "FocusedStyle"];
            else hitbox.Style = (Style)Resources[style + "Style"];
        }

        private void FocusRow(FileSystemElement fse)
        {
            //Remove focus highlight from old row
            //If there has already been a focused element
            if (focusedRow != null)
            {
                if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedStyle"];
                else focusedRow.Style = (Style)Resources["RowDefaultStyle"];
            }

            //Fetch row (border) from hitboxes (see xaml)
            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            var row = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

            //Store FileSystemElement(for checking if its selected) and Border (for styling)
            FocusedItem = fse;
            focusedRow = row;

            //Apply focused style to new row
            if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else focusedRow.Style = (Style)Resources["RowDefaultFocusedStyle"];
        }

        private void RemoveFocus(FileSystemElement fse)
        {
            if (focusedRow != null)
            {
                if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedStyle"];
                else focusedRow.Style = (Style)Resources["RowDefaultStyle"];
            }

            FocusedItem = null;
            focusedRow = null;
        }

        private void OpenItemFlyout(FileSystemElement fse, FrameworkElement row, Point position)
        {
            if (SelectedItems.Count == 1) ItemFlyout.ShowAt(row, position);
            else MultipleItemFlyout.ShowAt(row, position);
        }
    }
}
