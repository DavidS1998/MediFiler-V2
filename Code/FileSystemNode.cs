using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace MediFiler_V2
{
    public class FileSystemNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public IStorageFile File { get; set; }
        public bool IsFile { get; set; } = false;
        public int Depth { get; set; }
        public FileSystemNode Parent { get; set; }
        public List<FileSystemNode> SubFiles { get; set; } = new();
        public List<FileSystemNode> SubFolders { get; set; } = new();
        public int FileCount { get; set; }
        public int ChildFileCount { get { return FileCount + SubFolders.Sum(f => f.ChildFileCount); } }

        public FileSystemNode(IStorageItem storageItem, int depth, FileSystemNode parent = null)
        {
            Name = storageItem.Name;
            Path = storageItem.Path;
            Depth = depth;
            Parent = parent;

            // If root node is a file, pretend it's a folder with only this as a file
            if (depth == 0 && storageItem is StorageFile)
            {
                IsFile = true;
                SubFiles.Add(new FileSystemNode(storageItem, Depth + 1, this));
            }
            
            // Return if this is a file (leaf)
            if (!storageItem.IsOfType(StorageItemTypes.Folder)) {File = storageItem as IStorageFile; return;}
            
            var folder = (StorageFolder) storageItem;
            var filesAndFolders = folder.GetItemsAsync().AsTask().Result;

            // Make a node for each file and folder
            Parallel.ForEach(filesAndFolders, item =>
            {
                if (item.IsOfType(StorageItemTypes.File))
                {
                    SubFiles.Add(new FileSystemNode(item, depth + 1, this));
                }
                if (item.IsOfType(StorageItemTypes.Folder))
                {
                    SubFolders.Add(new FileSystemNode(item, depth + 1, this));
                }
            });
            
            // Sort result
            SubFiles = SubFiles.OrderBy(x => x.Name).ToList();
            SubFolders = SubFolders.OrderBy(x => x.Name).ToList();
            
            // Supplementary information
            FileCount = SubFiles.Count;
        }
        
        public string GetFormattedText(int fileCount, string name, int childFileCount)
        {
            return string.Format("({0}/{2}) - {1}", fileCount, name, childFileCount);
        }

        // Reloads all files within this folder node
        public void LocalRefresh()
        {
            if (IsFile) return;
            
            // Get rid of SubFiles and load them anew
            SubFiles.Clear();
            var folder = StorageFolder.GetFolderFromPathAsync(Path).AsTask().Result;
            var files = folder.GetFilesAsync().AsTask().Result;
            
            // Make a node for each file
            Parallel.ForEach(files, file =>
            {
                SubFiles.Add(new FileSystemNode(file, Depth + 1, this));
            });
            
            // Sort result
            SubFiles = SubFiles.OrderBy(x => x.Name).ToList();
            FileCount = SubFiles.Count;
        }

        // Reloads all files within this folder node and all subfolders
        public void CascadingRefresh()
        {
            // TODO: Implement
        }
        
        public bool FolderStillExists()
        {
            if (IsFile) return true;
            try
            {
                _ = StorageFolder.GetFolderFromPathAsync(Path).AsTask().Result;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
