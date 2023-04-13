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
    public FileSystemNode CurrentFolder;
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
    
    // Load file
    public void Load()
    {
        if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;
        var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
        _metadataHandler.ShowMetadata(currentFile);
        _fileThumbnail.ClearPreviews(_mainWindow.PreviewImageContainer1);
        _fileThumbnail.PreloadThumbnails(CurrentFolderIndex, CurrentFolder, _mainWindow.PreviewImageContainer1);
        DisplayCurrentFile(currentFile);
    }
    
    
    // // // FILE DISPLAY  // // //


    // Decides how each file type should be shown
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
    
    // Displays an image file in FileViewer. Also works with GIFs
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
    
    // Creates BitMap from File Explorer thumbnail and sets it as FileViewer source
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
    



    // For loading a different folder context
    public void SwitchFolder(FileSystemNode newFolder, int position = 0)
    {
        CurrentFolder = newFolder;
        // Set position if within bounds
        CurrentFolderIndex = position < 0 ? 0 : (position > newFolder.SubFiles.Count ? 0 : position);
        Clear();
        Load();
    }

    // Clear all loaded content
    public void Clear()
    {
        _latestLoadedImage = -1;
        _fileThumbnail.ThumbnailCache.Clear();
        _mainWindow.AppTitleTextBlock1.Text = "MediFiler";
        _mainWindow.FileViewer1.Source = null;
        _fileThumbnail.ClearPreviews(_mainWindow.PreviewImageContainer1);
        _metadataHandler.ClearMetadata();
    }
}