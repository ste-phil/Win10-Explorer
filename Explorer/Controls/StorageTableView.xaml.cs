using Explorer.Entities;
using Explorer.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.BulkAccess;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Explorer.Controls
{
    public sealed partial class StorageTableView : Page
    {
        public event EventHandler<FileSystemElement> RequestedTabOpen;

        private const string ROW_SELECTED_STYLE_NAME = "RowSelected";
        private const string ROW_DEFAULT_STYLE_NAME = "RowDefault";
        private const string ROW_DROP_STYLE_NAME = "RowDrop";

        //Resizing columns stuff
        private bool isResizing;
        private Point startPos;
        private int currentColumnIndex;

        //Selection rect stuff
        private bool isDraggingSelection;
        private PointerPoint pressedPos;
        private ulong lastMoveTimestamp;
        private int pointerOverRowIndex;

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
            set
            {
                if (SelectedItems != null) SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
                SetValue(SelectedItemsProperty, value);
                SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
            }
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

        /// <summary>
        /// Needed to sync selection changes between the different ViewModes of the FileBrowser (e.g TableView, PictureView)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < selectedElements.Count; i++)
                {
                    var hitbox = selectedElements[i];

                    if (hitbox == focusedRow) hitbox.Style = (Style)Resources[ROW_DEFAULT_STYLE_NAME + "FocusedStyle"];
                    else hitbox.Style = (Style)Resources[ROW_DEFAULT_STYLE_NAME + "Style"];
                }

                selectedElements.Clear();
                return;
            }

            if (e.OldItems != null)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var fse = (FileSystemElement)e.OldItems[i];
                    var hitbox = selectedElements.FirstOrDefault(f => f.Tag == fse);
                    if (hitbox == null) continue;

                    StyleHitbox(fse, hitbox, ROW_DEFAULT_STYLE_NAME);
                }
            }

            for (int i = 0; i < SelectedItems.Count; i++)
            {
                var fse = SelectedItems[i];
                var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
                if (container == null) continue;
                var hitbox = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

                selectedElements.Add(hitbox);

                //When item which is going to be selected and is focused apply different style
                if (fse == FocusedItem) hitbox.Style = (Style)Resources["RowSelectedFocusedStyle"];
                else hitbox.Style = (Style)Resources["RowSelectedStyle"];
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
                    var width = ContentGrid.ColumnDefinitions[i].ActualWidth - 200;
                    if (width <= 0) width = ContentGrid.ColumnDefinitions[i].ActualWidth;

                    HeaderGrid.ColumnDefinitions[i].Width = new GridLength(width);
                    ContentGrid.ColumnDefinitions[i].Width = new GridLength(width);
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

            currentColumnIndex = (int)columnHeader.GetValue(Grid.ColumnProperty);
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
            if (ItemsSource != null && ItemsSource.Count > 0 && FocusedItem != null) FocusRow(FocusedItem);
        }

        private void StorageTableView_LostFocus(object sender, RoutedEventArgs e)
        {
            RemoveFocus();
        }

        #region User Events

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
                    else ToggleSelect(FocusedItem);
                    break;
                //Open/Navigate currently focused item
                case VirtualKey.Enter:
                    DoubleTappedItem = FocusedItem;
                    break;
                case VirtualKey.Menu:
                    if (focusedRow != null)
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
                        ToggleSelect(ItemsSource[index]);

                        //Select LastFocusedItem if its not selected
                        if (!SelectedItems.Contains(ItemsSource[lastFocusedIndex]))
                            ToggleSelect(ItemsSource[lastFocusedIndex]);
                    }

                    FocusRow(ItemsSource[index], true);

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
                    TryScrollFocusSelect(ItemsSource[i]);
                    foundNextItem = true;
                    break;
                }
            }

            if (!foundNextItem && firstItemIndex != -1)
            {
                UnselectOldRows();
                TryScrollFocusSelect(ItemsSource[firstItemIndex]);
            }
        }

        private void Row_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var hitbox = (FrameworkElement)sender;
            var item = (FileSystemElement)hitbox.Tag;

            var point = e.GetCurrentPoint(hitbox);
            if (point.PointerDevice.PointerDeviceType == PointerDeviceType.Mouse)
            {
                if (point.Properties.IsMiddleButtonPressed && item.IsFolder)
                {
                    RequestedTabOpen?.Invoke(this, item);
                }
            }
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
                ToggleSelect(item, hitbox);

            FocusRow(item);
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

                ToggleSelect(item, hitbox);
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

        private void OpenItemFlyout(FileSystemElement fse, FrameworkElement row, Point position)
        {
            if (SelectedItems.Count == 1) ItemFlyout.ShowAt(row, position);
            else MultipleItemFlyout.ShowAt(row, position);
        }
        #endregion

        private void TryScrollFocusSelect(FileSystemElement fse)
        {
            //Check if the element is even rendered
            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            if (container == null)
            {
                ScrollTo(fse);
                return;
            }

            var childTransform = container.TransformToVisual(ScrollViewer);
            var rectangle = childTransform.TransformBounds(new Rect(new Point(0, 0), container.RenderSize));
            var scrollViewerRect = new Rect(new Point(0, 0), ScrollViewer.RenderSize);

            //Check if the elements Rect intersects with that of the scrollviewer's
            scrollViewerRect.Intersect(rectangle);

            //Not in view
            if (scrollViewerRect.IsEmpty)
            {
                ScrollTo(fse);
            }

            var hitbox = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);
            ToggleSelect(fse, hitbox);
            FocusRow(fse, hitbox);
        }

        private void SelectRow(FileSystemElement fse)
        {
            if (SelectedItems.Contains(fse)) return;

            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            if (container == null) return;
            var hitbox = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

            SelectedItems.Add(fse);
            selectedElements.Add(hitbox);

            //When item which is going to be selected and is focused apply different style
            if (fse == FocusedItem) hitbox.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else hitbox.Style = (Style)Resources["RowSelectedStyle"];
        }

        private void SelectRow(FileSystemElement fse, FrameworkElement hitbox)
        {
            if (SelectedItems.Contains(fse)) return;

            SelectedItems.Add(fse);
            selectedElements.Add(hitbox);

            //When item which is going to be selected and is focused apply different style
            if (fse == FocusedItem) hitbox.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else hitbox.Style = (Style)Resources["RowSelectedStyle"];
        }


        private void ToggleSelect(FileSystemElement fse, FrameworkElement hitbox)
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

        private void ToggleSelect(FileSystemElement fse)
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

        private void DeselectRow(FileSystemElement fse)
        {
            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            var hitbox = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

            SelectedItems.Remove(fse);
            selectedElements.Remove(hitbox);

            StyleHitbox(fse, hitbox, ROW_DEFAULT_STYLE_NAME);
        }

        private void DeselectRow(FileSystemElement fse, FrameworkElement hitbox)
        {
            SelectedItems.Remove(fse);
            selectedElements.Remove(hitbox);

            StyleHitbox(fse, hitbox, ROW_DEFAULT_STYLE_NAME);
        }

        private void SelectRowsBetween(FileSystemElement fse)
        {
            var lastTappedRowIndex = FocusedItem == null ? 0 : ItemsSource.IndexOf(FocusedItem); //SelectedItems.Count == 0 ? -1 : ItemsSource.IndexOf(SelectedItems.Last());
            var tappedRowIndex = ItemsSource.IndexOf(fse);

            var from = Math.Min(lastTappedRowIndex, tappedRowIndex);
            var to = Math.Max(lastTappedRowIndex, tappedRowIndex);
            for (int i = from; i <= to; i++)
            {
                //if (i == lastTappedRowIndex) continue;

                var item = ItemsSource[i];
                var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(item);
                var row = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

                if (!SelectedItems.Contains(item)) ToggleSelect(item, row);
            }
        }

        private void UnselectOldRows()
        {
            if (selectedElements.Count == 0 && SelectedItems.Count == 0) return;

            for (int i = 0; i < selectedElements.Count; i++)
            {
                var hitbox = selectedElements[i];
                if (i >= SelectedItems.Count)        //Could be that the selectedItem doesnt exist anymore but rows still need to be recolored
                {
                    hitbox.Style = (Style)Resources["RowDefaultStyle"];
                    continue;
                }

                var fse = SelectedItems[i];

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

        private void StyleHitbox(FrameworkElement hitbox, string style)
        {
            if (hitbox == focusedRow) hitbox.Style = (Style)Resources[style + "FocusedStyle"];
            else hitbox.Style = (Style)Resources[style + "Style"];
        }

        #region Focus Methods
        private void FocusRow(FileSystemElement fse, bool usedKeyboard = false)
        {
            //Remove focus highlight from old row
            //If there has already been a focused element
            if (focusedRow != null)
            {
                if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedStyle"];
                else focusedRow.Style = (Style)Resources["RowDefaultStyle"];
            }

            //Scroll to focused item when the user navigates through files with the keyboard
            if (usedKeyboard)
            {
                ScrollTo(fse);
            }

            //Fetch row (border) from hitboxes (see xaml)
            var container = (ContentPresenter)ItemsSourceRowHitbox.ContainerFromItem(fse);
            if (container == null) return;

            var row = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);

            //Store FileSystemElement(for checking if its selected) and Border (for styling)
            FocusedItem = fse;
            focusedRow = row;

            //Apply focused style to new row
            if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else focusedRow.Style = (Style)Resources["RowDefaultFocusedStyle"];
        }

        private void FocusRow(FileSystemElement fse, FrameworkElement hitbox, bool usedKeyboard = false)
        {
            RemoveFocus();

            //Scroll to focused item when the user navigates through files with the keyboard
            if (usedKeyboard)
            {
                ScrollTo(fse);
            }

            //Store FileSystemElement(for checking if its selected) and Border (for styling)
            FocusedItem = fse;
            focusedRow = hitbox;

            //Apply focused style to new row
            if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedFocusedStyle"];
            else focusedRow.Style = (Style)Resources["RowDefaultFocusedStyle"];
        }

        /// <summary>
        /// Remove focus highlight from focusedRow, if it is focused
        /// </summary>
        /// <param name="fse"></param>
        private void RemoveFocus()
        {
            if (focusedRow != null)
            {
                if (SelectedItems.Contains(FocusedItem)) focusedRow.Style = (Style)Resources["RowSelectedStyle"];
                else focusedRow.Style = (Style)Resources["RowDefaultStyle"];
            }

            FocusedItem = null;
            focusedRow = null;
        }
        #endregion

        private void ScrollTo(FileSystemElement fse)
        {
            ScrollTo(ItemsSource.IndexOf(fse));
        }

        private void ScrollTo(int index)
        {
            ScrollViewer.ChangeView(null, 30 * index, null);
        }

        #region Drag&Drop

        private async void Row_Drop(object sender, DragEventArgs e)
        {
            var row = (Border)sender;
            var fse = (FileSystemElement)row.Tag;

            StyleHitbox(row, ROW_DEFAULT_STYLE_NAME);
            if (fse.IsFolder && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();

                //FileSystem.MoveStorageItemsAsync(fse, items.ToList());
            }
        }

        private void Row_DragOver(object sender, DragEventArgs e)
        {
            var row = (Border)sender;
            var fse = (FileSystemElement)row.Tag;

            if (fse.IsFolder && e.DataView.Contains(StandardDataFormats.StorageItems))
                e.AcceptedOperation = DataPackageOperation.Move;

            StyleHitbox(row, ROW_DROP_STYLE_NAME);
        }

        private void Row_DragLeave(object sender, DragEventArgs e)
        {
            var row = (Border)sender;
            StyleHitbox(row, ROW_DEFAULT_STYLE_NAME);
        }

        private async void Row_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            //Cancel drawing selection rect when item is dragged
            isDraggingSelection = false;
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;

            var row = (Border)sender;
            var fse = (FileSystemElement)row.Tag;

            var deferral = args.GetDeferral();
            if (!fse.IsFolder)
            {
                var storageItem = await FileSystem.GetFileAsync(fse);
                args.Data.SetStorageItems(new List<IStorageItem> { storageItem }, false);

                var ti = await storageItem.GetThumbnailAsync(ThumbnailMode.SingleItem, 30);
                if (ti != null)
                {
                    var stream = ti.CloneStream();
                    var img = new BitmapImage();

                    await img.SetSourceAsync(stream);

                    args.Data.RequestedOperation = DataPackageOperation.Move;
                    args.DragUI.SetContentFromBitmapImage(img, new Point(-1, 0));

                    //args.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    //args.DragUI.SetContentFromDataPackage();
                }
            }
            else
            {
                var storageItem = await FileSystem.GetFolderAsync(fse);
                args.Data.SetStorageItems(new List<IStorageItem> { storageItem }, false);

                var ti = await storageItem.GetThumbnailAsync(ThumbnailMode.SingleItem, 30);
                if (ti != null)
                {
                    var stream = ti.CloneStream();
                    var img = new BitmapImage();

                    await img.SetSourceAsync(stream);

                    args.Data.RequestedOperation = DataPackageOperation.Move;
                    args.DragUI.SetContentFromBitmapImage(img, new Point(-1, 0));

                    //args.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    //args.DragUI.SetContentFromDataPackage();
                }
            }

            //args.DragUI.SetContentFromDataPackage();
            deferral.Complete();
        }
        #endregion

        #region SelectionRect
        private void ContentGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(ContentGrid);
            if (!isDraggingSelection || pointer.Timestamp == lastMoveTimestamp) return;

            var startPos = pressedPos.Position;
            var newPos = pointer.Position;

            var newSizeX = newPos.X - startPos.X;       //Get the move delta x
            if (newSizeX < 0)       //Drag left from start pos
            {
                Canvas.SetLeft(SelectionRect, newPos.X);
                SelectionRect.Width = -newSizeX;
            }
            else                    //Drag right from start pos
            {
                SelectionRect.Width = newPos.X - startPos.X;
            }

            var newSizeY = newPos.Y - startPos.Y;       //Get the move delta y
            if (newSizeY < 0)       //Drag up from start pos
            {
                Canvas.SetTop(SelectionRect, newPos.Y);
                SelectionRect.Height = -newSizeY;
            }
            else                    //Drag down from start pos
            {
                SelectionRect.Height = newPos.Y - startPos.Y;
            }

            var rowHeight = 30;
            var currentPointerOverRowIndex = (int)newPos.Y / rowHeight;
            if (pointerOverRowIndex != currentPointerOverRowIndex)  //If pointer is still over the same row dont do anything
            {
                var topPos = Canvas.GetTop(SelectionRect);
                var from = (int)topPos / rowHeight;      //start index to select rows (30 is rowHeight)
                var to = (int)(topPos + SelectionRect.Height) / rowHeight + 1;            //end index

                var controlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                if (!controlDown) UnselectOldRows();
                for (int i = from; i < to; i++)
                {
                    if (i >= 0 && i < ItemsSource.Count)
                        SelectRow(ItemsSource[i]);
                }
            }

            lastMoveTimestamp = pointer.Timestamp;
            pointerOverRowIndex = currentPointerOverRowIndex;
        }

        private void ContentGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(ContentGrid);

            //if (pointer.Properties.IsRightButtonPressed)
            //{
            isDraggingSelection = true;
            pressedPos = e.GetCurrentPoint((FrameworkElement)sender);
            ContentGrid.CapturePointer(e.Pointer);

            Canvas.SetLeft(SelectionRect, pressedPos.Position.X);
            Canvas.SetTop(SelectionRect, pressedPos.Position.Y);
            //}
        }

        private void ContentGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            isDraggingSelection = false;
            ContentGrid.ReleasePointerCapture(e.Pointer);

            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }
        #endregion
    }
}