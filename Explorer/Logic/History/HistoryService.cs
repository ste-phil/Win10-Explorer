using Explorer.Entities;
using Explorer.Logic.History;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorer.Logic
{
    public class HistoryService
    {
        private static HistoryService instance;
        public static HistoryService Instance => instance ?? (instance = new HistoryService());

        private HistoryService() { }

        private int operationCounter;
        public ObservableCollection<IFileSystemElementOperation> Operations { get; set; } = new ObservableCollection<IFileSystemElementOperation>();

        
        public void AddCreateOperation(FileSystemElement fse)
        {
            Operations.Add(new FileSystemElementCreateOperation { ElementId = ++operationCounter, Name = fse.Name, Path = fse.Path }) ;
        }

        public void AddMoveOperation(FileSystemElement fse, string oldPath, string oldName)
        {
            var id = FindOrCreateElementId(oldPath, oldName);

            Operations.Add(new FileSystemElementMoveOperation { Name = fse.Name, Path = fse.Path, ElementId = id, OriginalName = oldName, OriginalPath = oldPath });
        }

        public void AddPasteOperation(FileSystemElement fse, string oldPath, string oldName)
        {
            var id = FindOrCreateElementId(oldPath, oldName);

            Operations.Add(new FileSystemElementPasteOperation { Name = fse.Name, Path = fse.Path, ElementId = id, OriginalName = oldName, OriginalPath = oldPath });
        }

        public void AddRenameOperation(FileSystemElement fse, string oldName)
        {
            var id = FindOrCreateElementId(fse.Path, oldName);

            Operations.Add(new FileSystemElementRenameOperation { Name = fse.Name, Path = fse.Path, ElementId = id, OriginalName = oldName});
        }

        public void AddDeleteOperation(FileSystemElement fse)
        {
            var id = FindOrCreateElementId(fse.Name, fse.Path);

            Operations.Add(new FileSystemElementDeleteOperation { Name = fse.Name, Path = fse.Path, ElementId = id});
        }

        public IEnumerable<IFileSystemElementOperation> GetHistory(FileSystemElement fse)
        {
            var operation = GetLastOperation(fse.Path, fse.Name);
            if (operation == null) return null;

            var id = operation.ElementId;
            return FindHistory(id);
        }

        private IEnumerable<IFileSystemElementOperation> FindHistory(int elementId)
        {
            return Operations.Where(x => x.ElementId == elementId);
        }
        
        private int FindElementId(FileSystemElement fse)
        {
            var operation = GetLastOperation(fse.Path, fse.Name);
            if (operation == null) return -1;

            return operation.ElementId;
        }

        private int FindOrCreateElementId(string path, string name)
        {
            var operation = GetLastOperation(path, name);
            if (operation == null)
            {
                AddCreateOperation(new FileSystemElement { Path = path, Name = name});
                return operationCounter;
            }

            return operation.ElementId;
        }

        private IFileSystemElementOperation GetLastOperation(string path, string name)
        {
            return Operations.LastOrDefault(x => x.Name == name && x.Path == path);
        }

        private void Clear()
        {
            Operations.Clear();
            operationCounter = 0;
        }

    }
}
