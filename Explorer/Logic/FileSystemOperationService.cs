using Explorer.Entities;
using Explorer.Logic.FileSystemService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Explorer.Logic
{
    public class FileSystemOperationService
    {
        private static FileSystemOperationService instance;
        public static FileSystemOperationService Instance => instance ?? (instance = new FileSystemOperationService());

        public ObservableCollection<FileSystemOperation> Operations { get; set; } = new ObservableCollection<FileSystemOperation>();

        private FileSystemOperationService() {
            //Operations.Add(new FileSystemOperation(FileSystemOperations.Move, sourceItem: null, new FileSystemElement { Name = "1."}));
            //Operations.Add(new FileSystemOperation(FileSystemOperations.Copy, sourceItem: null, new FileSystemElement { Name = "2." }));
        }

        public async Task BeginMoveOperation(FileSystemElement targetFolder, IStorageItem sourceItem)
        {
            await BeginMoveOperation(targetFolder, new List<IStorageItem> { sourceItem });
        }

        public async Task BeginMoveOperation(FileSystemElement targetFolder, List<IStorageItem> sourceItems)
        {
            var itemsString = sourceItems.Count > 1 ? sourceItems.Count.ToString() : sourceItems[0].Name;
            var operation = new FileSystemOperation(FileSystemOperations.Move, itemsString, targetFolder);

            Operations.Add(operation);
            await FileSystem.MoveStorageItemsAsync(targetFolder, sourceItems);
            Operations.Remove(operation);
        }

        public async Task BeginCopyOperation(FileSystemElement targetFolder, IStorageItem sourceItem)
        {
            await BeginMoveOperation(targetFolder, new List<IStorageItem> { sourceItem });
        }

        public async Task BeginCopyOperation(FileSystemElement targetFolder, List<IStorageItem> sourceItems)
        {
            var itemsString = sourceItems.Count > 1 ? sourceItems.Count.ToString() : sourceItems[0].Name;
            var operation = new FileSystemOperation(FileSystemOperations.Copy, itemsString, targetFolder);

            Operations.Add(operation);
            await FileSystem.CopyStorageItemsAsync(targetFolder, sourceItems);
            Operations.Remove(operation);
        }
    }
}
