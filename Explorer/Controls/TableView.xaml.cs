using Explorer.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            public Border Background { get; set; }

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
            get { return itemSource; }
            set { itemSource = value; itemSource.CollectionChanged += ItemsSource_CollectionChanged; }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<FileSystemElement>),
        typeof(TableView), new PropertyMetadata(DependencyProperty.UnsetValue));

        public MenuFlyout ItemFlyout { get; set; }

        public static readonly DependencyProperty ItemFlyoutProperty = DependencyProperty.Register(nameof(ItemFlyout), typeof(MenuFlyout),
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

        private void ItemsSource_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null)  //List has been cleared
            {
                TableGrid.Children.Clear();
                TableGrid.RowDefinitions.Clear();
                Rows.Clear();

                TableGrid.Children.Add(textBlockNoContent);
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
                Background = new Border { Margin = new Thickness(1), BorderBrush = revealBrush, BorderThickness = new Thickness(1) },
                Hitbox = new Rectangle { Fill = brush }
            };

            //rh.Hitbox.PointerPressed += Row_PointerPressed;
            rh.Hitbox.Tapped += Row_Tapped;
            rh.Hitbox.RightTapped += Row_RightTapped;
            rh.Hitbox.DoubleTapped += Row_DoubleTapped;
            rh.SetGridPos(row, 0, 5);

            TableGrid.Children.Add(rh.Background);

            AddSymbolCell(row, 0, ItemsSource[row].Type.HasFlag(FileAttributes.Directory) ? Symbol.Folder : Symbol.Document);
            AddCell(row, 1, ItemsSource[row].Name);
            AddCell(row, 2, ItemsSource[row].Size.ToString());
            AddCell(row, 3, ItemsSource[row].DateModifiedString);
            AddCell(row, 4, ItemsSource[row].Type.ToString());

            TableGrid.Children.Add(rh.Hitbox);
            Rows.Add(rh);
        }

        


        private void AddRowDefinition()
        {
            var rd = new RowDefinition { Height = GridLength.Auto };
            TableGrid.RowDefinitions.Add(rd);
        }

        private void AddCell(int row, int column, string content)
        {
            var tb = new TextBlock();
            tb.Text = content;
            tb.Margin = new Thickness(0, 5, 0, 5);

            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, column);
            TableGrid.Children.Add(tb);
        }
        private void AddSymbolCell(int row, int column, Symbol symbol)
        {
            var content = new SymbolIcon();
            content.Symbol = symbol;
            Grid.SetRow(content, row);
            Grid.SetColumn(content, column);
            TableGrid.Children.Add(content);
        }

        private void Row_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var i = FindSelectedIndex((Rectangle)sender);

            if (i != selectedRow) //Unhighlight old row
            {
                if (selectedRow != -1 && selectedRow < Rows.Count) Rows[selectedRow].Background.Style = null;
                ItemSelected?.Invoke(sender, ItemsSource[i]);
            }

            selectedRow = i;
        }

        private void Row_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ItemDoubleClicked?.Invoke(sender, ItemsSource[selectedRow]);
        }

        private void Row_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var i = FindSelectedIndex((Rectangle)sender);

            if (i != selectedRow) //Unhighlight old row
            {
                if (selectedRow != -1 && selectedRow < Rows.Count) Rows[selectedRow].Background.Style = null;
                ItemSelected?.Invoke(sender, ItemsSource[i]);
                clickCount = 0;
            }

            //Highlight new row
            Rows[i].Background.Style = (Style)Resources["RowSelectedHighlight"];

            if (DateTime.Now > clickTime.AddSeconds(1))
            {
                clickCount = 0;
            }

            selectedRow = i;
            clickCount++;
            clickTime = DateTime.Now;

            if (clickCount == 2)
                ItemDoubleClicked?.Invoke(sender, ItemsSource[i]);
        }

        private void Row_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var tappedItem = (UIElement)e.OriginalSource;

            ItemFlyout.ShowAt(tappedItem, e.GetPosition(tappedItem));
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
