using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace MediFiler_V2
{
    public class FileNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int Depth { get; set; }
        public FileNode Parent { get; set; }
        public List<FileNode> SubFiles { get; set; } = new();
        public List<FileNode> SubFolders { get; set; } = new();

        public FileNode(IStorageItem node, int depth, FileNode parent)
        {
            Path = node.Path;
            Name = node.Name;
            Depth = depth;

            if (node is not StorageFolder folder) return;

            var folderItems = folder.GetItemsAsync().GetAwaiter().GetResult();

            foreach (var folderItem in folderItems)
            {
                switch (folderItem)
                {
                    case StorageFile:
                    {
                        var newFile = new FileNode(folderItem, depth + 1, this);
                        SubFiles.Add(newFile);
                        break;
                    }
                    case StorageFolder:
                    {
                        var newFolder = new FileNode(folderItem, depth + 1, this);
                        SubFolders.Add(newFolder);
                        break;
                    }
                }
            }
        }
    }
}
