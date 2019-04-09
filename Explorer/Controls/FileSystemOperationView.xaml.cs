using Explorer.Entities;
using Explorer.Logic;
using Explorer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Explorer.Controls
{
    public class FileSystemOperationViewModel : BaseModel
    {
        private ObservableCollection<FileSystemOperation> operations;
        private Visibility detailsVisisble;

        public Visibility DetailsVisible
        {
            get { return detailsVisisble; }
            set { detailsVisisble = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FileSystemOperation> Operations
        {
            get { return operations; }
            set { operations = value; OnPropertyChanged(); }
        }
    }

    public sealed partial class FileSystemOperationView : UserControl
    {
        private Compositor compositor;

        public FileSystemOperationViewModel ViewModel { get; set; }

        public FileSystemOperationView()
        {
            this.InitializeComponent();

            compositor = Window.Current.Compositor;

            ViewModel = new FileSystemOperationViewModel();
            ViewModel.Operations = FileSystemOperationService.Instance.Operations;
            ViewModel.DetailsVisible = Visibility.Collapsed;
        }


        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel.DetailsVisible == Visibility.Collapsed)
            {
                ExpandAnimation.Begin();
                ViewModel.DetailsVisible = Visibility.Visible;

                DetailsIcon.Translation = new Vector3(0f, 12f, 0f);
                DetailsIcon.Rotation = 180;
                DetailsIcon.RotationAxis = new Vector3(1f, 0f, 0f);
            }
            else
            {
                ViewModel.DetailsVisible = Visibility.Collapsed;
                CompressAnimation.Begin();

                DetailsIcon.Translation = new Vector3(0f, 0f, 0f);
                DetailsIcon.Rotation = 0;
            }
        }
    }
}
