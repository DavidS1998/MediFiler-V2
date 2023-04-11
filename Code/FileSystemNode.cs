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
        public int Depth { get; set; }
        public List<FileSystemNode> SubFiles { get; set; } = new();
        public List<FileSystemNode> SubFolders { get; set; } = new();
        
        public FileSystemNode(IStorageItem storageItem, int depth)
        {
            Name = storageItem.Name;
            Path = storageItem.Path;
            Depth = depth;

            // If root node is a file, pretend it's a folder with only this as a file
            if (depth == 0 && storageItem is StorageFile)
            {
                SubFiles.Add(new FileSystemNode(storageItem, Depth + 1));
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
                    SubFiles.Add(new FileSystemNode(item, depth + 1));
                }
                if (item.IsOfType(StorageItemTypes.Folder))
                {
                    SubFolders.Add(new FileSystemNode(item, depth + 1));
                }
            });
            
            // Sort result
            SubFiles = SubFiles.OrderBy(x => x.Name).ToList();
            SubFolders = SubFolders.OrderBy(x => x.Name).ToList();
        }
    }
}
