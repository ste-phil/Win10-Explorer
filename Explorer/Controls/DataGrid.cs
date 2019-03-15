using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorer.Controls
{
    public class DataGrid : Microsoft.Toolkit.Uwp.UI.Controls.DataGrid
    {
        private uint clickCount;
        private DateTime clickTime = DateTime.Now;

        public event EventHandler<int> ItemClicked;
        public event EventHandler<int> ItemDoubleClicked;

        public DataGrid()
        {
            SelectionChanged += DataGrid_SelectionChanged;
            PointerPressed += DataGrid_PointerPressed;
        }

        private void DataGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (DateTime.Now > clickTime.AddSeconds(1))   //Reset click count if more than x time has elapsed
            {
                clickCount = 0;
                clickTime = DateTime.Now;
            }

            ItemClicked?.Invoke(sender, 3);
            clickCount++;

            Debug.WriteLine("Click");

            if (clickCount == 2)
            {
                ItemDoubleClicked?.Invoke(sender, 3);
                Debug.WriteLine("DClick");
            }
        }

        private void DataGrid_SelectionChanged(object sender, Windows.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Change");

            if (clickCount > 1)
                clickCount = 0;

            clickTime = DateTime.Now;
        }
    }
}
