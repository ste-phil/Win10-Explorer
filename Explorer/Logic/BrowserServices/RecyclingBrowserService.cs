using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Explorer.Entities;
using Explorer.Logic;
using Explorer.Logic.FileSystemService;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using static Explorer.Logic.FileSystemRetrieveService;

namespace Explorer.Models
{
    public class RecyclingBrowserService : FileBrowserService
    {
        public RecyclingBrowserService(ObservableCollection<FileSystemElement> elements) : base(elements) { }
  
        public override async void DeleteFileSystemElement(FileSystemElement fse, bool permanently = false)
        {
            await retrieveService.DeleteFileSystemElement(fse, true);
        }
    }
}
