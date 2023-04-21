using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MediFiler_V2.Code;

public class MainWindowModel
{
    // UI
    private MainWindow _mainWindow;
    
    // Helper classes
    private MetadataHandler _metadataHandler;
    private FileThumbnail _fileThumbnail;
    private FileImage _fileImage;

    // Constants
    public const int PreloadDistance = 21;

    // TODO: Stop using global variables
    private int _latestLoadedImage = -1;
    public FileSystemNode CurrentFolder ;
    public int CurrentFolderIndex;
    public bool FileActionInProgress;
    
    public MainWindowModel(MainWindow window)
    {
        _mainWindow = window;

        _metadataHandler = new MetadataHandler(_mainWindow, this, _mainWindow.AppTitleBar1);
        _fileThumbnail = new FileThumbnail(this, PreloadDistance);
        _fileImage = new FileImage();
        
        // Set thumbnail preview count on startup
        _fileThumbnail.CreatePreviews(PreloadDistance, _mainWindow.PreviewImageContainer1);
    }

    /// Load file
    public void Load()
    {
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0)
        {
            _mainWindow.RenameButton1.IsEnabled = false;
            _mainWindow.DeleteButton1.IsEnabled = false;
            _mainWindow.PlusButton1.IsEnabled = false;
            _mainWindow.MinusButton1.IsEnabled = false;
            _mainWindow.OpenButton1.IsEnabled = false;
            _mainWindow.UpscaleButton1.IsEnabled = false;
            
            var brush = new SolidColorBrush(Colors.Black);
            _mainWindow.AppTitleBar1.Background = brush;
            return;
        };

        // Reset file action when loading new file
        if (FileActionInProgress)
        {
            FileActionInProgress = false;
            _mainWindow.ResetImage();
            return;
        }
        
        try
        {
            var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
            
            SetAppBarColor(currentFile);
            _metadataHandler.ShowMetadata(currentFile);
            _fileThumbnail.ClearPreviewCache(_mainWindow.PreviewImageContainer1);
            _fileThumbnail.PreloadThumbnails(CurrentFolderIndex, CurrentFolder, _mainWindow.PreviewImageContainer1);
            DisplayCurrentFile(currentFile);
            
            _mainWindow.RenameButton1.IsEnabled = true;
            _mainWindow.DeleteButton1.IsEnabled = true;
            _mainWindow.PlusButton1.IsEnabled = true;
            _mainWindow.MinusButton1.IsEnabled = true;
            _mainWindow.OpenButton1.IsEnabled = true;
            _mainWindow.UpscaleButton1.IsEnabled = true;
        }
        catch (Exception)
        {
            // If file cannot be found, refresh context and try again
            Debug.WriteLine("File not found when loading");
            Refresh();
        }
    }
    
    // AppBar color
    public void SetAppBarColor(FileSystemNode fileSystem)
    {
        var color = fileSystem.ConditionalColoring();
        // Color to brush
        var brush = new SolidColorBrush(color);
        _mainWindow.AppTitleBar1.Background = brush;
    }

    
    // // // FILE DISPLAY  // // //


    /// Decides how each file type should be shown
    public void DisplayCurrentFile(FileSystemNode fileSystem)
    {
        switch (FileTypeHelper.GetFileCategory(fileSystem.Path))
        {
            case FileTypeHelper.FileCategory.IMAGE:
                _mainWindow.TextHolder1.Visibility = Visibility.Collapsed;
                _mainWindow.ImageViewer1.Visibility = Visibility.Visible;
                _mainWindow.VideoHolder1.Visibility = Visibility.Collapsed;
                DisplayThumbnail(fileSystem);
                DisplayImage(fileSystem);
                break;
            case FileTypeHelper.FileCategory.VIDEO:
                _mainWindow.TextHolder1.Visibility = Visibility.Collapsed;
                _mainWindow.ImageViewer1.Visibility = Visibility.Visible;
                _mainWindow.VideoHolder1.Visibility = Visibility.Visible;
                DisplayThumbnail(fileSystem);
                break;
            case FileTypeHelper.FileCategory.TEXT:
                _mainWindow.TextHolder1.Visibility = Visibility.Visible;
                _mainWindow.ImageViewer1.Visibility = Visibility.Collapsed;
                _mainWindow.VideoHolder1.Visibility = Visibility.Collapsed;
                DisplayText(fileSystem);
                break;
            case FileTypeHelper.FileCategory.OTHER:
                _mainWindow.TextHolder1.Visibility = Visibility.Collapsed;
                _mainWindow.ImageViewer1.Visibility = Visibility.Visible;
                _mainWindow.VideoHolder1.Visibility = Visibility.Collapsed;
                DisplayThumbnail(fileSystem);
                break;
        }
    }
    
    /// Displays an image file in FileViewer. Also works with GIFs
    public async void DisplayImage(FileSystemNode fileSystem)
    {
        var sentInIndex = CurrentFolderIndex; // File change check
        var sentInFolder = CurrentFolder.Path; // Context change check
        var bitmap = await _fileImage.LoadImage(fileSystem, (int)_mainWindow.FileHolder1.ActualHeight);
        // Throw away result if current file changed; Invalid images don't overwrite thumbnails
        if (sentInIndex != CurrentFolderIndex || sentInFolder != CurrentFolder.Path || bitmap == null) return;
        _mainWindow.ImageViewer1.Source = bitmap;
        _latestLoadedImage = sentInIndex; // Stops thumbnail from overwriting image
    }
    
    /// Creates BitMap from File Explorer thumbnail and sets it as FileViewer source
    public async void DisplayThumbnail(FileSystemNode fileSystem)
    {
        var sentInIndex = CurrentFolderIndex; // File change check
        await _fileThumbnail.SaveThumbnailToCache(fileSystem.Path, sentInIndex);
        // Don't overwrite full images; Don't show if file changed
        if (_latestLoadedImage == CurrentFolderIndex || 
            sentInIndex != CurrentFolderIndex ||
            !_fileThumbnail.ThumbnailCache.ContainsKey(sentInIndex)) return;
        _mainWindow.ImageViewer1.Source = _fileThumbnail.ThumbnailCache[sentInIndex];
    }

    public async void DisplayText(FileSystemNode fileSystem)
    {
        var text = await FileIO.ReadTextAsync(fileSystem.File);
        _mainWindow.TextViewer1.Text = text;
    }
    
    
    public void FileAction()
    {
        var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
        
        switch (FileTypeHelper.GetFileCategory(currentFile.Path))
        {
            case FileTypeHelper.FileCategory.IMAGE:
                ImageAction(currentFile);
                break;
            case FileTypeHelper.FileCategory.TEXT:
            case FileTypeHelper.FileCategory.VIDEO:
            case FileTypeHelper.FileCategory.OTHER:
                OpenDefaultAction(currentFile);
                break;
        }
    }

    public void OpenDefaultAction(FileSystemNode currentFile)
    {
        // Open in default app
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = currentFile.Path;
        psi.UseShellExecute = true;
        Process.Start(psi);
    }
    
    public async void OpenAction()
    {
        var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
        // Open in program picker
        var options = new LauncherOptions();
        options.DisplayApplicationPicker = true;
        await Launcher.LaunchFileAsync(currentFile.File, options);
    }

    public async void Upscale()
    {
        try
        {
            var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
        
            if (FileTypeHelper.GetFileCategory(currentFile.Path) != FileTypeHelper.FileCategory.IMAGE) return;

            var baseDirectory = await StorageFolder.GetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory);
            if (baseDirectory == null) return;
            baseDirectory = await baseDirectory.TryGetItemAsync("Upscalers") as StorageFolder;
            if (baseDirectory == null) return;
            baseDirectory = await baseDirectory.TryGetItemAsync("ncnn") as StorageFolder;
            if (baseDirectory == null) return;

            var upscalerExe = await baseDirectory.TryGetItemAsync("ncnn.exe");
            if (upscalerExe == null) return;
        
            var exe = (StorageFile) upscalerExe;

            var pathWithoutFileName = currentFile.Path.Substring(0, currentFile.Path.LastIndexOf('\\') + 1);
            var newName = currentFile.File.Name.Insert(currentFile.File.Name.LastIndexOf('.'), "[U]");
            var argument = " -s 2 -i \"" + currentFile.Path + "\" -o \"" + pathWithoutFileName + newName + "\"";

            Debug.WriteLine(argument);

            var upscaler = new Process();
            upscaler.StartInfo.FileName = exe.Path;
            upscaler.StartInfo.Arguments = argument;
            upscaler.Start();
            upscaler.WaitForExit();

            CurrentFolderIndex++;
            Refresh();
            Debug.WriteLine("Upscale done");
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    public void ImageAction(FileSystemNode currentFile)
    {
        if (FileActionInProgress)
        {
            FileActionInProgress = !FileActionInProgress; 
            _mainWindow.ResetImage();
            return;
        }
        FileActionInProgress = true;
    }
    
    
    // // // NAVIGATION // // //


    /// For loading a different folder context
    public void SwitchFolder(FileSystemNode newFolder, int position = 0, bool reorder = true)
    {
        var sameFolder = CurrentFolder == newFolder && CurrentFolder != null;
        CurrentFolder = newFolder;
        
        try
        {
            // TODO: UNSURE IF THIS SHOULUD BE DEFAULT BEHAVIOR - MAYBE ONLY IF FOLDER IS EMPTY
            // Slows down loading of folders with many files
            
            if (reorder) { CurrentFolder.LocalRefresh(); }
        }
        catch (Exception)
        {
            // Folder does not exist
            FullRefresh();
        }
        
        // Set position if within bounds
        CurrentFolderIndex = position < 0 ? 0 : (position >= newFolder.SubFiles.Count ? (newFolder.FileCount - 1) : position);
        Clear(sameFolder);
        Load();
    }
    
    /// Clear all loaded content
    public void Clear(bool sameFolder)
    {
        _latestLoadedImage = -1;
        _fileThumbnail.ThumbnailCache.Clear();
        _mainWindow.AppTitleTextBlock1.Text = "MediFiler";
        _mainWindow.ImageViewer1.Source = null;
        _fileThumbnail.ClearPreviewCache(_mainWindow.PreviewImageContainer1);
        _metadataHandler.ClearMetadata();

        // Only run on real folder switch
        // Debug.WriteLine(sameFolder ? "Same folder" : "Different folder");
        if (sameFolder) return;
        Debug.WriteLine("Undo queue cleared");
        ClearUndoQueue();
        EmptyTrash();
    }

    /// Refreshes the current folder and reloads all items within it
    public void Refresh(bool reorder = true)
    {
        _mainWindow.UpdateHomeFolders();
        
        if (!CurrentFolder.FolderStillExists())
        {
            // Folder does not exist, load empty context
            return;
        }

        //TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1);
        if (reorder)
        {
            SwitchFolder(CurrentFolder, CurrentFolderIndex);
        }
        else
        {
            SwitchFolder(CurrentFolder, CurrentFolderIndex, false);
        }
    }

    /// Rebuilds the entire context from scratch, may be slow
    public void FullRefresh()
    {
        TreeHandler.RebuildTree(_mainWindow.FileTreeView1);
        Refresh();
    }
    
    
    // // // FILE OPERATIONS // // //

    
    public void MoveFile(FileSystemNode destination)
    {
        // Error check
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0 || destination.Path == CurrentFolder.Path) return;

        try
        {
            // Undo queue
            Push(CurrentFolder.SubFiles[CurrentFolderIndex].CreateMemento(UndoAction.Move));
            CurrentFolder.SubFiles[CurrentFolderIndex].Move(destination);
            Refresh();
        }
        catch (Exception e)
        {
            UndoQueue.Pop();
            Refresh();
            Console.WriteLine(e);
        }
        
    }
    
    /// Deletes by moving to a trash folder, and recycling it only when switching folders
    public async void DeleteFile()
    {
        // Error check
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;

        IStorageFolder baseDirectory = await StorageFolder.GetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory);
        var trashFolder = await baseDirectory.CreateFolderAsync("Trash", CreationCollisionOption.OpenIfExists);
        var trashFolderNode = new FileSystemNode(trashFolder, 0);
        
        // Undo queue
        Push(CurrentFolder.SubFiles[CurrentFolderIndex].CreateMemento(UndoAction.Move));
        
        CurrentFolder.SubFiles[CurrentFolderIndex].Move(trashFolderNode);
        Refresh();
    }

    /// Empty Trash - Runs when switching folders
    public async void EmptyTrash()
    {
        var baseDirectory = await StorageFolder.GetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory);
        var trashFolder = await baseDirectory.CreateFolderAsync("Trash", CreationCollisionOption.OpenIfExists);
        
        // For each file in the trash folder, delete it while awaiting
        foreach (var file in await trashFolder.GetFilesAsync())
        {
            await file.DeleteAsync();
        }
    }

    /// Renames the currently selected file
    public async void RenameDialog()
    {
        // Error check
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;
        
        // Get the file extension as a string
        var fileExtension = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Split('.').Last();
        
        // Create a ContentDialog box with a text input field
        var dialog = new ContentDialog
        {
            Title = "Rename File",
            Content = new TextBox
            {
                // Text is the current file name without the extension
                Text = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Replace("." + fileExtension, ""),
                AcceptsReturn = false
            },
            PrimaryButtonText = "Rename",
            SecondaryButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        var newName = ((TextBox)dialog.Content).Text;
        
        RenameFile(newName);
    }
    
    public async void RenameFile(string newName)
    {
        // Get the file extension as a string
        var fileExtension = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Split('.').Last();
        
        // Only allow valid File names
        var invalidCharsRegex = new Regex("[\\\\/:*?\"<>|]");
        if (string.IsNullOrWhiteSpace(newName) || invalidCharsRegex.IsMatch(newName))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Invalid File Name",
                Content = "File names cannot contain following characters: \\ / : * ? \" < > |",
                PrimaryButtonText = "OK",
                XamlRoot = _mainWindow.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };
            await errorDialog.ShowAsync();
            return;
        }
        
        // Undo queue
        Push(CurrentFolder.SubFiles[CurrentFolderIndex].CreateMemento(UndoAction.Rename));
        
        CurrentFolder.SubFiles[CurrentFolderIndex].Rename(newName + "." + fileExtension);
        Refresh(false);
    }
    
    // TODO: Separate into a plug-in? Setting?
    // Add +
    public void AddPlus()
    {
        // Error check
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;
        
        var fileExtension = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Split('.').Last();
        var currentName = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Replace("." + fileExtension, "");
        // Add a + to the start of the name
        var newName = "+" + currentName;
        RenameFile(newName);
    }
    
    // Remove +
    public void RemovePlus()
    {
        // Error check
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;
        
        var fileExtension = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Split('.').Last();
        var currentName = CurrentFolder.SubFiles[CurrentFolderIndex].Name.Replace("." + fileExtension, "");
        // Check if the name starts with a +
        if (!currentName.StartsWith("+")) return;

        // Remove one + from the start of the name
        var newName = currentName.Remove(0, 1);
        RenameFile(newName);
    }
    
    
    // TODO: Refactor into own class
    // // // UNDO QUEUE // // //
    
    
    // Undo queue - for undoing delete, rename, and move operations
    public Stack<NodeMemento> UndoQueue = new();
    
    public void Push(NodeMemento memento)
    {
        if (UndoQueue.Count <= 0) _mainWindow.UndoButton1.IsEnabled = true;
        UndoQueue.Push(memento);
        Debug.WriteLine("Pushing " + memento.Action + " of " + memento.Name);
    }
    
    public void ClearUndoQueue()
    {
        UndoQueue.Clear();
        _mainWindow.UndoButton1.IsEnabled = false;
    }

    /// Undo the last operation
    public void Undo()
    {
        if (UndoQueue.Count <= 0)
        {
            Debug.WriteLine("UNDO STACK EMPTY");
            return;
        };
        
        var memento = UndoQueue.Pop();
        if (UndoQueue.Count <= 0) _mainWindow.UndoButton1.IsEnabled = false;
        
        Debug.WriteLine("Undoing " + memento.Action + " of " + memento.Node.Name);
        switch (memento.Action)
        {
            case UndoAction.Rename:
                memento.Node.Rename(memento.Name);
                break;
            case UndoAction.Move:
                memento.Node.Move(memento.Parent);
                break;
        }
        
        Refresh();
    }
}