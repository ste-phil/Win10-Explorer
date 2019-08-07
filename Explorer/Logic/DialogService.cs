using Explorer.Entities;
using Explorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Explorer.Logic
{
    public class DialogService : ObservableEntity
    {
        private string dialogName;
        private string primaryButtonText;
        private string secondaryButtonText;
        private string inputText;

        public ContentDialog TextDialog { get; set; }

        public string DialogName
        {
            get { return dialogName; }
            set { dialogName = value; OnPropertyChanged(); }
        }
        
        public string PrimaryButtonText
        {
            get { return primaryButtonText; }
            set { primaryButtonText = value; OnPropertyChanged(); }
        }

        public string SecondaryButtonText
        {
            get { return secondaryButtonText; }
            set { secondaryButtonText = value; OnPropertyChanged(); }
        }

        public string InputText
        {
            get { return inputText; }
            set { inputText = value; OnPropertyChanged(); }
        }

        public async Task<string> ShowTextDialog(string dialogName, string primaryAction, string secondaryAction, string inputText = "")
        {
            DialogName = dialogName;
            PrimaryButtonText = primaryAction;
            SecondaryButtonText = secondaryAction;
            InputText = inputText;

            var result = await TextDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                return InputText;
            }

            return null;
        }
    }
}
