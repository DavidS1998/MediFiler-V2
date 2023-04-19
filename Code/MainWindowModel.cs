using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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
    
    public MainWindowModel(MainWindow window)
    {
        _mainWindow = window;

        _metadataHandler = new MetadataHandler(_mainWindow, this);
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
            return;
        };
        try
        {
            var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
            _metadataHandler.ShowMetadata(currentFile);
            _fileThumbnail.ClearPreviewCache(_mainWindow.PreviewImageContainer1);
            _fileThumbnail.PreloadThumbnails(CurrentFolderIndex, CurrentFolder, _mainWindow.PreviewImageContainer1);
            DisplayCurrentFile(currentFile);
            
            _mainWindow.RenameButton1.IsEnabled = true;
            _mainWindow.DeleteButton1.IsEnabled = true;
        }
        catch (Exception)
        {
            // If file cannot be found, refresh context and try again
            Debug.WriteLine("File not found when loading");
            Refresh();
        }
    }
    
    
    // // // FILE DISPLAY  // // //


    /// Decides how each file type should be shown
    public void DisplayCurrentFile(FileSystemNode fileSystem)
    {
        DisplayThumbnail(fileSystem);
        
        switch (FileTypeHelper.GetFileCategory(fileSystem.Path))
        {
            case FileTypeHelper.FileCategory.IMAGE:
                DisplayImage(fileSystem);
                break;
            case FileTypeHelper.FileCategory.VIDEO:
                break;
            case FileTypeHelper.FileCategory.TEXT:
                break;
            case FileTypeHelper.FileCategory.OTHER:
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
        _mainWindow.FileViewer1.Source = bitmap;
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
        _mainWindow.FileViewer1.Source = _fileThumbnail.ThumbnailCache[sentInIndex];
    }
    
    
    // // // NAVIGATION // // //


    /// For loading a different folder context
    public void SwitchFolder(FileSystemNode newFolder, int position = 0)
    {
        var sameFolder = CurrentFolder == newFolder && CurrentFolder != null;
        CurrentFolder = newFolder;
        
        try
        {
            // TODO: UNSURE IF THIS SHOULUD BE DEFAULT BEHAVIOR - MAYBE ONLY IF FOLDER IS EMPTY
            // Slows down loading of folders with many files
            CurrentFolder.LocalRefresh();
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
        Debug.WriteLine(sameFolder ? "Same folder" : "Different folder");
        
        _latestLoadedImage = -1;
        _fileThumbnail.ThumbnailCache.Clear();
        _mainWindow.AppTitleTextBlock1.Text = "MediFiler";
        _mainWindow.FileViewer1.Source = null;
        _fileThumbnail.ClearPreviewCache(_mainWindow.PreviewImageContainer1);
        _metadataHandler.ClearMetadata();

        // Only run on real folder switch
        if (sameFolder) return;
        Debug.WriteLine("Undo queue cleared");
        ClearUndoQueue();
        EmptyTrash();
    }

    /// Refreshes the current folder and reloads all items within it
    public void Refresh()
    {
        _mainWindow.UpdateHomeFolders();
        
        if (!CurrentFolder.FolderStillExists())
        {
            // Folder does not exist, load empty context
            return;
        }
        CurrentFolder.LocalRefresh();
        TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1);
        SwitchFolder(CurrentFolder, CurrentFolderIndex);
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
    public async void RenameFile()
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
        Refresh();
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
                TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1);
                memento.Node.Move(memento.Parent);
                break;
        }
        
        Refresh();
    }
}