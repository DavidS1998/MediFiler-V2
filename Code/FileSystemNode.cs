using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using MediFiler_V2.Code;

namespace MediFiler_V2
{
    public class FileSystemNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsFile { get; set; } = false;
        public int Depth { get; set; }
        public FileSystemNode Parent { get; set; }
        public List<FileSystemNode> SubFiles { get; set; } = new();
        public List<FileSystemNode> SubFolders { get; set; } = new();
        public int FileCount { get; set; }
        public int ChildFileCount { get { return FileCount + SubFolders.Sum(f => f.ChildFileCount); } }
        
        public IStorageFile File { get; set; }
        public IStorageFolder Folder { get; set; }

        public FileSystemNode(IStorageItem storageItem, int depth, FileSystemNode parent = null)
        {
            Name = storageItem.Name;
            Path = storageItem.Path;
            Depth = depth;
            Parent = parent;

            // If root node is a file, pretend it's a folder with only this as a file
            if (depth == 0 && storageItem is StorageFile)
            {
                // TODO: Figure this out - For combining loose files into one pretend folder
                SubFiles.Add(new FileSystemNode(storageItem, Depth + 1, this));
            }
            
            // Return if this is a file (leaf)
            if (!storageItem.IsOfType(StorageItemTypes.Folder))
            {
                File = storageItem as IStorageFile;
                IsFile = true;
                return;
            }
            Folder = storageItem as IStorageFolder;
            
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
            
            SubFiles = SubFiles.OrderBy(x => x.Name, new SortLiterally()).ToList();
            FileCount = SubFiles.Count;
        }

        // Reloads all files within this folder node and all subfolders
        public void CascadingRefresh()
        {
            // TODO: Implement - For use with SubFolder viewing
        }

        // Move this node to another node
        public void Move(FileSystemNode destination)
        {
            if (!IsFile) return;
            
            // Check if file already exists in destination
            var newPath = destination.Path + "\\" + Name;
            var extension = File.FileType;
            var nameWithoutExtension = Name.Substring(0, Name.Length - extension.Length);
            for (int i = 1; System.IO.File.Exists(newPath); i++)
            {
                newPath = destination.Path + "\\" + nameWithoutExtension + " (" + i + ")" + extension;
            }
            
            Debug.WriteLine("Moving " + Path + " to " + newPath + "");
            
            // Can't use File.MoveAsync because IStorageFiles created from Drag and Drop are set to ReadOnly (blame microsoft)
            System.IO.File.Move(Path, newPath, false);

            // Remove from old parent
            Parent.SubFiles.Remove(this);
            Parent.FileCount--;
            
            // Update properties
            this.Parent = destination;
            this.Path = destination.Path + "\\" + Name;
            this.Depth = destination.Depth + 1;

            // Add to new parent
            destination.SubFiles.Add(this);
            destination.FileCount++;
            destination.SubFiles = destination.SubFiles = SubFiles.OrderBy(x => x.Name, new SortLiterally()).ToList();
        }
        
        public void Rename(string newName)
        {
            if (!IsFile) return;
            File.RenameAsync(newName, NameCollisionOption.GenerateUniqueName).AsTask().Wait();
            Name = newName;
            Path = File.Path;

            // TODO: Folder renaming
        }
        
        // Checks if this folder node has been deleted
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
        
        // Create mmento
        public NodeMemento CreateMemento(UndoAction action)
        {
            return new NodeMemento(action, Name, Path, Parent, this);
        }
        
        // Literal sort (1, 2, 10)
        public class SortLiterally : IComparer<string> {

            [DllImport("shlwapi.dll", CharSet=CharSet.Unicode, ExactSpelling=true)]
            static extern int StrCmpLogicalW(String x, String y);

            public int Compare(string x, string y) {
                return StrCmpLogicalW(x, y);
            }
        }
    }
}
