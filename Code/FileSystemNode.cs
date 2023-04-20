using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using MediFiler_V2.Code;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MediFiler_V2
{
    public class FileSystemNode : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsFile { get; set; } = false;
        public int Depth { get; set; }
        public FileSystemNode Parent { get; set; }
        public List<FileSystemNode> SubFiles { get; set; } = new();
        public List<FileSystemNode> SubFolders { get; set; } = new();
        private int _fileCount;
        public int FileCount { 
            get { return _fileCount; } 
            set { _fileCount = value; OnPropertyChanged(nameof(FileCount)); } }
        public int ChildFileCount { get { return FileCount + SubFolders.Sum(f => f.ChildFileCount); } }
        public Brush FolderColor { get; set; }
        
        private static bool _isExpanded = true;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
        
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
            // FOLDER CONFIRMED
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
        
        // Color of the folder in the tree view
        public Brush GetColor()
        {
            var color = ConditionalColoring();
            var brush = new SolidColorBrush(color);

            return brush;
        }

        public Color ConditionalColoring()
        {
            // TODO: Move to a settings file (JSON?)
            return Name switch
            {
                // Meta filtering
                _ when Name.StartsWith("[CREATOR]") => Color.FromArgb(255, 255, 128, 128),
                _ when Name.StartsWith("[SORT]") => Color.FromArgb(255, 128, 128, 255),
                _ when Name.StartsWith("[META]") => Color.FromArgb(255, 255, 255, 128),
                _ when Name.StartsWith("[SET]") => Color.FromArgb(255, 255,113,206),
                // Star filtering
                _ when Name.StartsWith("++++++") => Color.FromArgb(255, 255, 69, 0),
                _ when Name.StartsWith("+++++") => Color.FromArgb(255, 159, 49, 222),
                _ when Name.StartsWith("++++") => Color.FromArgb(255, 230, 30, 88),
                _ when Name.StartsWith("+++") => Color.FromArgb(255, 19, 200, 226),
                _ when Name.StartsWith("++") => Color.FromArgb(255, 243, 243, 1),
                _ when Name.StartsWith("+") => Color.FromArgb(255, 50, 255, 50),
                _ when IsFile => Color.FromArgb(255, 0, 0, 0),
                _ when FileCount == 0 => Color.FromArgb(128, 255, 255, 255),
                _ when FileCount >= 100 => Color.FromArgb(255, 255, 0, 0),
                _ => Color.FromArgb(255, 255, 255, 255)
            };
        }

        public Color ActiveFolderBackgroundColor()
        {
            return Color.FromArgb(100, 255, 255, 255);
        }
        
        public Visibility HasSubFolders()
        {
            return SubFolders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public bool ConditionalExpand()
        {
            return Name switch
            {
                _ when Name.Contains("[CREATOR]") => false,
                _ when Name.Contains("[SORT]") => false,
                _ when Name.Contains("[META]") => false,
                _ when Name.Contains("[SET]") => false,
                _ when Name.Contains("[Theme]") => false,
                _ => true
            };
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
        
        // Create memento
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            Debug.WriteLine("Property changed: " + propertyName + "");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
