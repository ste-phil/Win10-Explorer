using Explorer.Entities;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Explorer.Controls
{
    public class StorageAdaptiveGridView : AdaptiveGridView
    {
        private bool sourceChanging;

        public new ObservableCollection<FileSystemElement> SelectedItems
        {
            get { return (ObservableCollection<FileSystemElement>)GetValue(SelectedItemsProperty); }
            set
            {
                if (SelectedItems != null) SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;

                SetValue(SelectedItemsProperty, value);

                if (SelectedItems != null) SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
            }
        }

        

        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
          "SelectedItems", typeof(ObservableCollection<FileSystemElement>), typeof(StorageAdaptiveGridView), new PropertyMetadata(null));
        


        public StorageAdaptiveGridView()
        {
            base.SelectionChanged += MultiAdaptiveGridView_SelectionChanged;
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            sourceChanging = true;

            var iss = (ObservableCollection<FileSystemElement>)ItemsSource;

            base.SelectedItems.Clear();
            for (int i = 0; i < SelectedItems.Count; i++)
            {
                SelectRange(new ItemIndexRange(iss.IndexOf(SelectedItems[i]), 1));
            }

            sourceChanging = false;
        }

        private void MultiAdaptiveGridView_SelectionChanged(object sender, Windows.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            if (sourceChanging) return;

            var added = e.AddedItems.ToList();
            var removed = e.RemovedItems.ToList();
            for (int i = 0; i < added.Count; i++)
            {
                SelectedItems.Add((FileSystemElement)added[i]);
            }

            for (int i = 0; i < removed.Count; i++)
            {
                SelectedItems.Remove((FileSystemElement)removed[i]);
            }
        }
    }
}
