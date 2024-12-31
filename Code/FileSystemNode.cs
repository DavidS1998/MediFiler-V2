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
using MediFiler_V2.Code.Utilities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

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
        public BitmapImage FolderIcon = null;
        public bool IsLoaded = false;

        private bool _hideSpecialSubFolders = false;
        public bool HideSpecialSubFolders {
            get { return _hideSpecialSubFolders; } 
            set { _hideSpecialSubFolders = value; OnPropertyChanged(nameof(HideSpecialSubFolders)); } }
        
        private int _fileCount;
        public int FileCount { 
            get { return _fileCount; } 
            set { _fileCount = value; OnPropertyChanged(nameof(FileCount)); } }
        public int ChildFileCount { get { return FileCount + SubFolders.Sum(f => f.ChildFileCount); } set {  } }
        
        private bool _folderColor;
        public bool FolderColor { 
            get { return _folderColor; } 
            set { _folderColor = value; OnPropertyChanged(nameof(FolderColor)); } }
        
        private bool _isCurrentFolder;
        public bool IsCurrentFolder { 
            get { return _isCurrentFolder; }
            set { _isCurrentFolder = value; OnPropertyChanged(nameof(IsCurrentFolder)); } }

        public bool AllExpanded = true;
        private static bool _isExpanded;
        public bool IsExpanded { 
            get { return _isExpanded; } 
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }
        
        public IStorageFile File { get; set; }
        public IStorageFolder Folder { get; set; }

        public FileSystemNode(IStorageItem storageItem, int depth, FileSystemNode parent, bool temp = false)
        {
            Name = storageItem.Name;
            Path = storageItem.Path;
            Depth = depth;
            Parent = parent;

            if (temp) return;

            // If root node is a file, pretend it's a folder with only this as a file
            if (depth == 0 && storageItem is StorageFile)
            {
                // TODO: Figure this out - For combining loose files into one pretend folder
                SubFiles.Add(new FileSystemNode(storageItem, Depth + 1, this));
            }
            
            // Return if this is a file (leaf)
            if (storageItem.IsOfType(StorageItemTypes.File))
            {
                File = storageItem as IStorageFile;
                IsFile = true;
                return;
            }
            // FOLDER CONFIRMED
            Folder = storageItem as IStorageFolder;
            
            var folder = (StorageFolder) storageItem;
            //var filesAndFolders = folder.GetItemsAsync().AsTask().Result;
            var folders = folder.GetFoldersAsync().AsTask().Result;

            // Make a node for each file and folder
            // Parallel causes race conditions, performance improvement was negligible
            //Parallel.ForEach(folders, item =>
            foreach (var item in folders)
            {
                // if (item.IsOfType(StorageItemTypes.File))
                // {
                //     SubFiles.Add(new FileSystemNode(item, depth + 1, this));
                // }
                if (item.IsOfType(StorageItemTypes.Folder))
                {
                    SubFolders.Add(new FileSystemNode(item, depth + 1, this));
                }
            }
            //});
            
            // Sort result
            //SubFiles = SubFiles.OrderBy(x => x.Name).ToList();
            SubFolders = SubFolders.OrderBy(x => x.Name).ToList();

            // Supplementary information
            //FileCount = SubFiles.Count;
        }

        public async Task GetSubFiles()
        {
            var files = await Folder.GetFilesAsync();
            
            if (!IsLoaded)
            {
                try
                {
                    Parallel.ForEach(files, file => { SubFiles.Add(new FileSystemNode(file, Depth + 1, this)); });
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error: " + e);
                }

            }
            else
            {
                //Debug.WriteLine("ISLOADED");
            }

            SubFiles = SubFiles.OrderBy(x => x.Name).ToList();
            IsLoaded = true;
            _fileCount = SubFiles.Count;
        }
        
        public void UpdateAsLoaded()
        {
            FileCount = SubFiles.Count;
            ChildFileCount = FileCount + SubFolders.Sum(f => f.ChildFileCount);
            FolderColor = true;
            //SetFolderIcon();
            OnPropertyChanged(nameof(FileCount));
        }

        public void UpdateColor()
        {
            OnPropertyChanged(nameof(FolderColor));
        }
        
        public void SetFolderIcon()
        {
            if (IsFile) return;
            if (FolderIcon != null) return;
            if (!FolderIconGetter.IconCache.ContainsKey(Path)) return;
            
            var thumbnail = FolderIconGetter.IconCache[Path];
            FolderIcon = new BitmapImage();
            FolderIcon.SetSource(thumbnail);
            OnPropertyChanged(nameof(FolderIcon));
        }

        public string GetFormattedText(int fileCount, string name, int childFileCount)
        {
            return string.Format("({0}/{2}) - {1}", fileCount, name, childFileCount);
        }
        
        // Color of the folder in the tree view
        public Brush GetColor(bool folderColor)
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
                _ when Name.StartsWith("[THEME]") => Color.FromArgb(255, 120, 155, 120),
                // Star filtering
                _ when Name.StartsWith("++++++") => Color.FromArgb(255, 255, 69, 0),
                _ when Name.StartsWith("+++++") => Color.FromArgb(255, 159, 49, 222),
                _ when Name.StartsWith("++++") => Color.FromArgb(255, 230, 30, 88),
                _ when Name.StartsWith("+++") => Color.FromArgb(255, 19, 150, 226),
                _ when Name.StartsWith("++") => Color.FromArgb(255, 150, 150, 1),
                _ when Name.StartsWith("+") => Color.FromArgb(255, 50, 150, 50),
                // Standard folder coloring
                _ when IsFile => Color.FromArgb(0, 150, 150, 150),
                _ when FileCount == 0 => Color.FromArgb(64, 255, 255, 255),
                _ when FileCount >= 100 => Color.FromArgb(255, 255, 0, 0),
                // When file list contains any file not starting with +
                _ when SubFiles.Any(f => !f.Name.StartsWith("+")) && Depth != 0 => Color.FromArgb(255, 255, 128, 0),
                
                _ when Depth == 0 => Color.FromArgb(255, 255, 255, 255),
                _ when Depth == 1 => Color.FromArgb(255, 255, 255, 255),
                _ when Depth == 2 => Color.FromArgb(255, 200, 200, 200),
                _ when Depth >= 3 => Color.FromArgb(255, 150, 150, 150),
                _ => Color.FromArgb(255, 255, 255, 255)
            };
        }
        
        
        public void UpdateColorStatus()
        {
            // TODO: Simplify condition checks into one function; currently has bugs regarding moving and undoing
        }

        public Brush ActiveFolderBackgroundColor(bool isCurrentFolder)
        {
            return isCurrentFolder ? 
                new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)) : 
                new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        public void ExpandWhenClicked(bool isExpanded)
        {
            if (!IsExpanded)
                IsExpanded = !IsExpanded;
        }
        
        public Visibility HasSubFolders()
        {
            return SubFolders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public bool ConditionalExpand(bool isExpanded)
        {
            return Name switch
            {
                _ when _isCurrentFolder => true,
                _ when Name.Contains("[CREATOR]") => false,
                _ when Name.Contains("[SORT]") => false,
                _ when Name.Contains("[META]") => false,
                _ when Name.Contains("[SET]") => false,
                _ when Name.Contains("[Theme]") => false,
                _ when Parent != null && Parent.Name.Contains("[CREATOR]") => false,
                _ when SubFolders.All(subFolder => subFolder.Name.Contains('[') && subFolder.Name.Contains(']')) && _hideSpecialSubFolders => false,
                _ when AllExpanded => true,
                _ when !AllExpanded => false,
                _ => true
            };
        }



        // Reloads all files within this folder node
        public void LocalRefresh()
        {
            if (IsFile) return;
            //Debug.WriteLine("Refreshed!");
            IsLoaded = true;
            
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

        public void FileRemoved(int index)
        {
            // Do nothing??
            //if (IsFile) return;
            //Debug.WriteLine(index);
            

            //if (index < 0 || index >= SubFiles.Count) return;
            //SubFiles.RemoveAt(index);
            //FileCount = SubFiles.Count;
        }

        // Reloads all files within this folder node and all subfolders
        public void CascadingRefresh()
        {
            // TODO: Implement - For use with SubFolder viewing
        }

        // Move this node to another node
        public void Move(FileSystemNode destination, int index)
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
            if (index < 0 || index >= destination.SubFiles.Count)
                { destination.SubFiles.Add(this); }
            else
                { destination.SubFiles.Insert(index, this); }
            destination.FileCount++;
            //destination.SubFiles = destination.SubFiles = SubFiles.OrderBy(x => x.Name, new SortLiterally()).ToList();
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
            //Debug.WriteLine("Property changed: " + propertyName + "");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
