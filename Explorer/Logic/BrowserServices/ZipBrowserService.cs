using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Explorer.Entities;
using Explorer.Helper;
using Explorer.Logic;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Explorer.Models
{
    public class ZipBrowserService : IBrowserService
    {
        private StorageFile currentZIPFile;
        private FileSystemElement currentFSE;
        private int currentDepth;

        private MultiDictionary<int, ZipFileElement> elements;

        public ObservableCollection<FileSystemElement> FileSystemElements { get; set; }

        public ZipBrowserService(ObservableCollection<FileSystemElement> elements)
        {
            FileSystemElements = elements;

            this.elements = new MultiDictionary<int, ZipFileElement>();
        }

        public async void OpenFileSystemElement(FileSystemElement fse)
        {
            var zfe = (ZipFileElement) fse;
            var file = await FileSystem.CreateStorageFile(ApplicationData.Current.TemporaryFolder, zfe.Name, zfe.ElementStream);

            if (fse.DisplayType == "Application") FileSystem.LaunchExeAsync(file.Path);
            else FileSystem.OpenFileWithDefaultApp(file.Path);
        }

        public void LoadFolder(FileSystemElement fse, FileSystemRetrieveService.ThumbnailFetchOptions thumbnailOptions)
        {
            FileSystemElements.Clear();

            if (fse is ZipFileElement) LoadZipItem((ZipFileElement) fse, thumbnailOptions);
            else LoadZip(fse, thumbnailOptions);
        }

        /// <summary>
        /// Loads a zip file
        /// </summary>
        /// <param name="fse"></param>
        /// <param name="thumbnailOptions"></param>
        private async void LoadZip(FileSystemElement fse, FileSystemRetrieveService.ThumbnailFetchOptions thumbnailOptions)
        {
            elements.Clear();
            currentZIPFile = await FileSystem.GetFileAsync(fse);
            currentFSE = fse;

            using (Stream stream = await currentZIPFile.OpenStreamForReadAsync())
            {
                var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    var entry = reader.Entry;

                    var keySplit = entry.Key.Split("/");
                    var subPath = string.Join(@"\", keySplit, 0, keySplit.Length - 1);

                    int depth;
                    ZipFileElement element;
                    if (entry.IsDirectory)
                    {
                        var name = keySplit[keySplit.Length - 2];
                        depth = keySplit.Length - 2;

                        element = new ZipFileElement(
                            name,
                            fse.Path + @"\" + subPath,
                            entry.LastModifiedTime.Value,
                            (ulong)entry.Size,
                            entry.Key,
                            depth
                        );

                        elements.AddFirst(depth, element);
                    }
                    else
                    {
                        var name = keySplit[keySplit.Length - 1];
                        depth = keySplit.Length - 1;

                        string fileExtension = "";
                        var fileName = entry.Key.Split(".");
                        if (fileName.Length > 1) fileExtension = fileName[fileName.Length - 1];

                        //Store fileStream to access it later
                        var elementStream = new MemoryStream();
                        reader.WriteEntryTo(elementStream);
                        await elementStream.FlushAsync();

                        var thumbnail = await FileSystem.GetFileExtensionThumbnail(fileExtension, thumbnailOptions.Mode, thumbnailOptions.Size, thumbnailOptions.Scale);
                        element = new ZipFileElement(
                            name,
                            fse.Path + @"\" + subPath,
                            entry.LastModifiedTime.Value,
                            (ulong)entry.Size,
                            thumbnail,
                            "." + fileExtension,
                            fileExtension,
                            entry.Key,
                            depth,
                            elementStream
                        );

                        elements.Add(depth, element);
                    }

                    if (depth == currentDepth) FileSystemElements.Add(element);
                }
            }
        }

        /// <summary> 
        /// Loads further folders inside zip files
        /// </summary>
        /// <param name="fse"></param>
        /// <param name="thumbnailOptions"></param>
        private void LoadZipItem(ZipFileElement fse, FileSystemRetrieveService.ThumbnailFetchOptions thumbnailOptions)
        {
            currentDepth = fse.ElementDepth + 1;

            //Load next depth
            var depthElements = elements[currentDepth];
            for (int i = 0; i < depthElements.Count; i++)
            {
                if (depthElements[i].ElementKey.Contains(fse.ElementKey)) FileSystemElements.Add(depthElements[i]);
            }
        }

        public void SearchAsync(string search)
        {
            
        }

        public void RenameFileSystemElement(FileSystemElement fse, string newName)
        {
            
        }

        public void DeleteFileSystemElement(FileSystemElement fse)
        {
            
        }

        public void RefetchThumbnails(FileSystemRetrieveService.ThumbnailFetchOptions thumbnailOptions)
        {
            
        }

        public async void CreateFolder(string folderName)
        {
            //using (var stream = await currentZIPFile.OpenStreamForWriteAsync())
            //using (var writer = WriterFactory.Open(stream,
            //    ArchiveType.Zip,
            //    new WriterOptions(CompressionType.Deflate)))
            //{
            //    writer.Write(folderName + "\\", new MemoryStream());
            //}

            //using (var stream = await currentZIPFile.OpenStreamForWriteAsync())
            //{
            //    var archive = ZipArchive.Open(stream);
            //    archive.AddEntry(folderName + "\\", new MemoryStream());
            //    archive.SaveTo(stream);
            //    stream.Flush();
            //}
        }

        public async void CreateFile(string fileName)
        {
            using (var stream = await currentZIPFile.OpenStreamForWriteAsync())
            using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate)))
            {
                writer.Write(fileName, new MemoryStream());
            }
        }

        public void CopyFileSystemElement(Collection<FileSystemElement> files)
        {
        }

        public void CutFileSystemElement(Collection<FileSystemElement> files)
        {
        }

        public void PasteFileSystemElement(FileSystemElement currentFolder)
        {
        }

        public void DragStorageItems(DataPackage dataPackage, DragUI dragUI, Collection<FileSystemElement> draggedItems)
        {
        }

        public void DropStorageItems(FileSystemElement droppedTo, IEnumerable<IStorageItem> droppeditems)
        {
        }
    }
}
