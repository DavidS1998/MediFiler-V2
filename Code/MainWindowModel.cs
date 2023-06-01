﻿using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.System;
using MediFiler_V2.Code.Utilities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MediFiler_V2.Code;

public class MainWindowModel
{
    // UI
    private readonly MainWindow _mainWindow;
    
    // Helper classes
    private readonly MetadataHandler _metadataHandler;
    private readonly FileThumbnail _fileThumbnail;
    private readonly ImageLoader _imageLoader;
    
    private readonly FolderOperations _folderOperations;
    public FolderOperations FolderOperations => _folderOperations;
    private readonly FileOperations _fileOperations;
    public FileOperations FileOperations => _fileOperations;
    private readonly UndoHandler _undoHandler;
    public UndoHandler UndoHandler => _undoHandler;

    // Constants
    public const int PreloadDistance = 13; // 21 fills ultrawide

    // TODO: Stop using global variables
    private int _latestLoadedImage = -1;
    public FileSystemNode CurrentFolder;
    public int CurrentFolderIndex;
    public bool FileActionInProgress;


    public MainWindowModel(MainWindow window)
    {
        _mainWindow = window;

        _metadataHandler = new MetadataHandler(_mainWindow, this, _mainWindow.AppTitleBar1);
        _fileThumbnail = new FileThumbnail(this, PreloadDistance);
        _imageLoader = new ImageLoader();
        
        // Set thumbnail preview count on startup
        _fileThumbnail.CreatePreviews(PreloadDistance, _mainWindow.PreviewImageContainer1);
        _undoHandler = new UndoHandler(this, _mainWindow);
        _folderOperations = new FolderOperations(this, _mainWindow);
        _fileOperations = new FileOperations(this, _mainWindow);
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
            //return;
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
        var bitmap = await _imageLoader.LoadImage(fileSystem, (int)_mainWindow.FileHolder1.ActualHeight);
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

    public async void Upscale(double factor)
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
            var argument = " -s " + factor + " -i \"" + currentFile.Path + "\" -o \"" + pathWithoutFileName + newName + "\"";

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
        _mainWindow.Expanded = true;

        // Only run on real folder switch
        // Debug.WriteLine(sameFolder ? "Same folder" : "Different folder");
        if (sameFolder) return;
        Debug.WriteLine("Undo queue cleared");
        UndoHandler.ClearUndoQueue();
        EmptyTrash();
    }

    /// Refreshes the current folder and reloads all items within it
    public void Refresh(bool reorder = true)
    {
        _mainWindow.JsonHandler.UpdateHomeFolders();
        
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
        TreeHandler.RebuildTree(_mainWindow.FileTreeView1, _mainWindow);
        Refresh();
    }
    
    /// Empty Trash - Runs when switching folders
    private static async void EmptyTrash()
    {
        try
        {
            var baseDirectory = await StorageFolder.GetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory);
            var trashFolder = await baseDirectory.CreateFolderAsync("Trash", CreationCollisionOption.OpenIfExists);
            
            // For each file in the trash folder, delete it while awaiting
            foreach (var file in await trashFolder.GetFilesAsync())
            {
                await file.DeleteAsync();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}