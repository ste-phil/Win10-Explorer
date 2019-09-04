using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic.FileSystemService;
using Explorer.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Explorer.Controls
{
    public static class Dialogs
    {
        public static DialogModel ShowEditDialog(string title, string primaryActionName, string secondaryActionName, string editText, Action<string> primaryAction)
            => DialogControl.Instance.ShowEditDialog(title, primaryActionName, secondaryActionName, editText, primaryAction);

        public async static Task<DialogModel> ShowPropertiesDialog(FileSystemElement fse, Action<string> primaryAction)
            => await DialogControl.Instance.ShowPropertiesDialog(fse, primaryAction);
    }

    public class DialogModel : BaseModel
    {
        private string title;
        private string primaryActionName;
        private string secondaryActionName;
        private Action<string> primaryAction;

        private Visibility editVisibility = Visibility.Collapsed;
        private string editText;

        private Visibility propertiesVisibility = Visibility.Collapsed;
        private FileSystemElement propertiesFileSystemElement;
        private BitmapImage propertiesImage;

        public string Title
        {
            get { return title; }
            set { title = value; OnPropertyChanged(); }
        }
        public string PrimaryActionName
        {
            get { return primaryActionName;}
            set { primaryActionName = value; OnPropertyChanged(); }
        }
        public string SecondaryActionName
        {
            get { return secondaryActionName; }
            set { secondaryActionName = value; OnPropertyChanged(); }
        }
        public Action<string> PrimaryAction
        {
            get { return primaryAction; }
            set { primaryAction = value; OnPropertyChanged(); }
        }

        //For edit dialog
        public Visibility EditVisibility
        {
            get { return editVisibility; }
            set { editVisibility = value; OnPropertyChanged(); }
        }

        public string EditText
        {
            get { return editText; }
            set { editText = value; OnPropertyChanged(); }
        }


        //For properties Dialog
        public Visibility PropertiesVisibility
        {
            get { return propertiesVisibility; }
            set { propertiesVisibility = value; OnPropertyChanged(); }
        }


        public FileSystemElement PropertiesFileSystemElement
        {
            get { return propertiesFileSystemElement; }
            set { propertiesFileSystemElement = value; OnPropertyChanged(); }
        }


        public BitmapImage PropertiesImage
        {
            get { return propertiesImage; }
            set { propertiesImage = value; OnPropertyChanged(); }
        }
    }

    public sealed partial class DialogControl : Page
    {
        public static DialogControl Instance;

        #region DependencyProperties

        public static readonly DependencyProperty DialogProperty = DependencyProperty.Register(
         "Dialog", typeof(DialogModel), typeof(DialogControl), new PropertyMetadata(null));
        #endregion

        public DialogControl()
        {
            this.InitializeComponent();

            Instance = this;
            //if (Instance == null) Instance = this;
            //else throw new Exception("Only one DialogControl allowed");

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;

            PrimaryButtonCmd = new Command(() => { Dialog.PrimaryAction(Dialog.EditText); CloseAllDialogs(); }, () => true);
            SecondaryButtonCmd = new Command(() => CloseAllDialogs(), () => true);

            Dialog = CreateClosedDialogModel();
        }

        #region Properties

        public DialogModel Dialog
        {
            get { return (DialogModel) GetValue(DialogProperty); }
            set
            {
                if (value == null) value = CreateClosedDialogModel();

                SetValue(DialogProperty, value);
                Bindings.Update();

                //Set focus to edit field if dialog type is edit
                if (Dialog.EditVisibility == Visibility.Visible) EditTextBox.Focus(FocusState.Programmatic);
            }
        }

        public Command PrimaryButtonCmd { get; set; } 
        public Command SecondaryButtonCmd { get; set; }

        #endregion

        public DialogModel ShowEditDialog(string title, string primaryActionName, string secondaryActionName, string editText, Action<string> primaryAction)
        {
            return new DialogModel
            {
                Title = title,
                PrimaryActionName = primaryActionName,
                SecondaryActionName = secondaryActionName,
                EditText = editText,
                PrimaryAction = primaryAction,
                EditVisibility = Visibility.Visible
            };
        }

        public async Task<DialogModel> ShowPropertiesDialog(FileSystemElement fse, Action<string> primaryAction)
        {
            BitmapImage bitmap = null;
            if (!fse.IsFolder)
            {
                var file = await FileSystem.GetFileAsync(fse);
                var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 50);
                bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
            }

            return new DialogModel
            {
                Title = "Properties",
                PrimaryActionName = "Ok",
                SecondaryActionName = "Cancel",
                PrimaryAction = primaryAction,
                PropertiesVisibility = Visibility.Visible,
                PropertiesFileSystemElement = fse,
                PropertiesImage = bitmap
            };
        }

        private void SaveCloseDialog(VirtualKey key)
        {
            if (key == VirtualKey.Enter) PrimaryButtonCmd.Execute(Dialog.EditText);
        }

        private void EditTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            SaveCloseDialog(e.Key);
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (IsDialogOpen() && args.VirtualKey == VirtualKey.Escape)
            {
                CloseAllDialogs();
                args.Handled = true;
            }
        }

        private bool IsDialogOpen()
        {
            if (Dialog.EditVisibility == Visibility.Visible) return true;
            if (Dialog.PropertiesVisibility == Visibility.Visible) return true;

            return false;
        }

        private void CloseAllDialogs()
        {
            Dialog = CreateClosedDialogModel();

            FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
        }

        private DialogModel CreateClosedDialogModel()
        {
            return new DialogModel {
                EditVisibility = Visibility.Collapsed,
                PropertiesVisibility = Visibility.Collapsed
            };
        }

        
    }
}
