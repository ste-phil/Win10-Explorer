using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Explorer.Controls
{
    public sealed partial class TableView : UserControl
    {
        private class RowHolder
        {
            public Rectangle Hitbox { get; set; }
            public Rectangle Background { get; set; }
            public List<UIElement> RowElements { get; set; } = new List<UIElement>();

            public void SetGridPos(int row, int column, int columnSpan = 1)
            {
                Grid.SetRow(Hitbox, row);
                Grid.SetColumn(Hitbox, column);
                Grid.SetColumnSpan(Hitbox, columnSpan);

                Grid.SetRow(Background, row);
                Grid.SetColumn(Background, column);
                Grid.SetColumnSpan(Background, columnSpan);
            }
        }

        public event EventHandler<FileSystemElement> ItemSelected;
        public event EventHandler<FileSystemElement> ItemDoubleClicked;

        private ObservableCollection<FileSystemElement> itemSource;

        private List<RowHolder> Rows;
        private int selectedRow = -1;

        private int clickCount;
        private DateTime clickTime;

        private TextBlock textBlockNoContent;


        public ObservableCollection<FileSystemElement> ItemsSource
        {
            get { return (ObservableCollection<FileSystemElement>) GetValue(ItemsSourceProperty); }
            set { 
                if (ItemsSource != null)
                    ItemsSource.CollectionChanged -= ItemsSource_CollectionChanged; 

                SetValue(ItemsSourceProperty, value); 
                ItemsSource_Changed();
            }
        }


        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<FileSystemElement>),
        typeof(TableView), new PropertyMetadata(DependencyProperty.UnsetValue));

        public MenuFlyout ItemFlyout { get; set; }

        public static readonly DependencyProperty ItemFlyoutProperty = DependencyProperty.Register(nameof(ItemFlyout), typeof(MenuFlyout),
       typeof(TableView), new PropertyMetadata(DependencyProperty.UnsetValue));

        public FileSystemElement SelectedItem
        {
            get { return (FileSystemElement) GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value);}
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(FileSystemElement),
            typeof(TableView), new PropertyMetadata(DependencyProperty.UnsetValue));

        public TableView()
        {
            this.InitializeComponent();

            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto});
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            textBlockNoContent = new TextBlock { FontSize = 32, Text = "No content", VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
            Grid.SetColumnSpan(textBlockNoContent, 5);
            Grid.SetColumn(textBlockNoContent, 0);

            Rows = new List<RowHolder>();
        }

        private void ItemsSource_Changed()
        {
            TableGrid.Children.Clear();
            TableGrid.RowDefinitions.Clear();
            Rows.Clear();

            for (int i = 0; i < ItemsSource.Count; i++)
            {
                AddRowDefinition();
                AddRow(i);
            }

            if (SelectedItem != null) HighlightRow(ItemsSource.IndexOf(SelectedItem));

            ItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
        }

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)  //List has been cleared
            {
                TableGrid.Children.Clear();
                TableGrid.RowDefinitions.Clear();
                Rows.Clear();

                TableGrid.Children.Add(textBlockNoContent);
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var row = Rows[e.OldStartingIndex + i];
                    var rowElements = row.RowElements;
                    foreach (var elem in rowElements)
                    {
                        TableGrid.Children.Remove(elem);
                    }

                    TableGrid.Children.Remove(row.Background);
                    TableGrid.Children.Remove(row.Hitbox);
                    TableGrid.RowDefinitions.RemoveAt(i);
                }

                return;
            }

            TableGrid.Children.Remove(textBlockNoContent);

            for (int i = 0; i < e.NewItems.Count; i++)
            {
                AddRowDefinition();
                AddRow(e.NewStartingIndex + i);
            }

        }

        private void AddRow(int row)
        {
            var brush = new SolidColorBrush { Color = Colors.Transparent };
            var revealBrush = new RevealBorderBrush { Color = Colors.Transparent, TargetTheme = Application.Current.RequestedTheme};

            var rh = new RowHolder
            {
                Background = new Rectangle { Margin = new Thickness(1), Stroke = revealBrush, StrokeThickness = 1 },
                Hitbox = new Rectangle { Fill = brush }
            };

            //rh.Hitbox.PointerPressed += Row_PointerPressed;
            rh.Hitbox.Tapped += Row_Tapped;
            rh.Hitbox.RightTapped += Row_RightTapped;
            rh.Hitbox.DoubleTapped += Row_DoubleTapped;
            rh.SetGridPos(row, 0, 5);

            TableGrid.Children.Add(rh.Background);

            AddSymbolCell(rh, row, 0, ItemsSource[row].IsFolder ? Symbol.Folder : Symbol.Document);
            AddCell(rh, row, 1, ItemsSource[row].Name);
            AddCell(rh, row, 2, ItemsSource[row].SizeString);
            AddCell(rh, row, 3, ItemsSource[row].DateModifiedString);
            AddCell(rh, row, 4, ItemsSource[row].Type.ToString());

            TableGrid.Children.Add(rh.Hitbox);
            Rows.Add(rh);
        }

        
        private void AddRowDefinition()
        {
            var rd = new RowDefinition { Height = GridLength.Auto };
            TableGrid.RowDefinitions.Add(rd);
        }

        private void AddCell(RowHolder rh, int row, int column, string text)
        {
            var content = new TextBlock();
            content.Text = text;
            content.Margin = new Thickness(0, 5, 0, 5);

            Grid.SetRow(content, row);
            Grid.SetColumn(content, column);
            
            rh.RowElements.Add(content);
            TableGrid.Children.Add(content);
        }

        private void AddSymbolCell(RowHolder rh, int row, int column, Symbol symbol)
        {
            var content = new SymbolIcon();
            content.Symbol = symbol;
            Grid.SetRow(content, row);
            Grid.SetColumn(content, column);

            rh.RowElements.Add(content);
            TableGrid.Children.Add(content);
        }

        private void Row_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var i = FindSelectedIndex((Rectangle)sender);
            SelectRow(i);
        }

        private void Row_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ItemDoubleClicked?.Invoke(sender, ItemsSource[selectedRow]);
        }
        
        private void Row_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var i = FindSelectedIndex((Rectangle)sender);
            var tappedItem = (UIElement)e.OriginalSource;

            SelectRow(i);
            ItemFlyout.ShowAt(tappedItem, e.GetPosition(tappedItem));
        }

       
        private void SelectRow(int index)
        {
            if (index == selectedRow) return;   //Only select if row changed

            //Unhighlight old row if there is one highlighted
            if (selectedRow != -1 && selectedRow < Rows.Count) Rows[selectedRow].Background.Style = null;

            selectedRow = index;
            SelectedItem = ItemsSource[index];
            ItemSelected?.Invoke(this, ItemsSource[index]);
            HighlightRow(index);
        }
        
        private void HighlightRow(int index)
        {
            if (index < 0 || index >= Rows.Count) return;

            Rows[index].Background.Style = (Style)Resources["RowSelectedHighlight"];
        }


        private int FindSelectedIndex(Rectangle hitbox)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Hitbox == hitbox) return i;
            }

            return -1;
        }
    }
}
