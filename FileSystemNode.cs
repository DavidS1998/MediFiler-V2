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
        public List<FileNode> SubFiles { get; set; } = new();
        public List<FileNode> SubFolders { get; set; } = new();

        public FileNode(IStorageItem node, int depth)
        {
            Path = node.Path;
            Name = node.Name;
            Depth = depth;

            // If root node is a file, pretend this is a folder with only this as a file
            if (depth == 0 && node is StorageFile)
            {
                SubFiles.Add(new FileNode(node, Depth + 1));
            }

            // If this item is a folder, continue on
            if (node is not StorageFolder folder) return;

            var folderItems = folder.GetItemsAsync().GetAwaiter().GetResult();

            foreach (var folderItem in folderItems)
            {
                // Add child nodes to respective list
                switch (folderItem)
                {
                    case StorageFile file:
                        SubFiles.Add(new FileNode(file, depth + 1));
                        break;
                    case StorageFolder subFolder:
                        SubFolders.Add(new FileNode(subFolder, depth + 1));
                        break;
                }
            }
        }
    }
}
