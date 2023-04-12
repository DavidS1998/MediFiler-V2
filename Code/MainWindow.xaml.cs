using System;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using BitmapImage = Microsoft.UI.Xaml.Media.Imaging.BitmapImage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediFiler_V2.Code
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow
    {
        // UI access
        public TextBlock AppTitleTextBlock1 { get => AppTitleTextBlock; set => AppTitleTextBlock = value; }
        public TextBlock InfoTextBlock1 { get => InfoTextBlock; set => InfoTextBlock = value; }
        
        // Constants
        public const int PreloadDistance = 21;

        // Helper classes
        private MetadataHandler _metadataHandler;
        private FileThumbnail _fileThumbnail;
        private FileImage _fileImage;
        
        // TODO: Stop using global variables
        private int _latestLoadedImage = -1;
        public FileSystemNode CurrentFolder;
        public int CurrentFolderIndex;
        
        // Initialize window
        public MainWindow()
        {
            InitializeComponent();
            _metadataHandler = new MetadataHandler(this);
            _fileThumbnail = new FileThumbnail(this, PreloadDistance);
            _fileImage = new FileImage(this);
            
            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;
            
            // Set thumbnail preview count on startup
            _fileThumbnail.CreatePreviews(PreloadDistance, PreviewImageContainer);
        }

        // Load file
        public void Load()
        {
            if (CurrentFolder == null || CurrentFolder.SubFiles.Count <= 0) return;
            var currentFile = CurrentFolder.SubFiles[CurrentFolderIndex];
            _metadataHandler.ShowMetadata(currentFile);
            _fileThumbnail.ClearPreviews(PreviewImageContainer);
            _fileThumbnail.PreloadThumbnails(CurrentFolderIndex, CurrentFolder, PreviewImageContainer);
            DisplayCurrentFile(currentFile);
        }
        

        // // // FILE DISPLAY  // // //


        // Decides how each file type should be shown
        private void DisplayCurrentFile(FileSystemNode fileSystem)
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
        
        // Displays an image file in FileViewer, also works with GIFs
        private async void DisplayImage(FileSystemNode fileSystem)
        {
            var sentInIndex = CurrentFolderIndex; // File change check
            var sentInFolder = CurrentFolder.Path; // Context change check
            var bitmap = await _fileImage.LoadImage(fileSystem, (int)FileHolder.ActualHeight);
            // Throw away result if current file changed; Invalid images don't overwrite thumbnails
            if (sentInIndex != CurrentFolderIndex || sentInFolder != CurrentFolder.Path || bitmap == null) return;
            FileViewer.Source = bitmap;
            _latestLoadedImage = sentInIndex; // Stops thumbnail from overwriting image
        }
        
        // Creates BitMap from File Explorer thumbnail and sets it as FileViewer source
        private async void DisplayThumbnail(FileSystemNode fileSystem)
        {
            var sentInIndex = CurrentFolderIndex; // File change check
            await _fileThumbnail.SaveThumbnailToCache(fileSystem.Path, sentInIndex);
            // Don't overwrite full images; Don't show if file changed
            if (_latestLoadedImage == CurrentFolderIndex || 
                sentInIndex != CurrentFolderIndex ||
                !_fileThumbnail.ThumbnailCache.ContainsKey(sentInIndex)) return;
            FileViewer.Source = _fileThumbnail.ThumbnailCache[sentInIndex];
        }
        
        
        // // // NAVIGATION // // //
        

        // Scroll between files
        private void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(FileViewer).Properties.MouseWheelDelta;
            if (delta == 0 || CurrentFolder == null) return;
            var increment = delta > 0 ? -1 : 1;
            if (CurrentFolderIndex + increment < 0 || CurrentFolderIndex + increment >= CurrentFolder.SubFiles.Count) return;

            CurrentFolderIndex += increment;
            Load();
        }

        // For loading a different folder context
        private void SwitchFolder(FileSystemNode newFolder, int position = 0)
        {
            CurrentFolder = newFolder;
            // Set position if within bounds
            CurrentFolderIndex = position < 0 ? 0 : (position > newFolder.SubFiles.Count ? 0 : position);
            Clear();
            Load();
        }

        // Clear all loaded content
        private void Clear()
        {
            _latestLoadedImage = -1;
            _fileThumbnail.ThumbnailCache.Clear();
            AppTitleTextBlock.Text = "MediFiler";
            FileViewer.Source = null;
            _fileThumbnail.ClearPreviews(PreviewImageContainer);
            _metadataHandler.ClearMetadata();
        }

        
        // // // UI EVENTS // // //
        

        // Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileSystemNode respectiveNode) return;
            
            if (leftClick)
            {
                SwitchFolder(respectiveNode);
            }
            // TODO: Handle left and right click differently
        }

        private void FolderListLeftClick(object sender, TappedRoutedEventArgs e)
        {
            FolderListClick(e, true);
        }

        private void FolderListRightClick(object sender, RightTappedRoutedEventArgs e)
        {
            FolderListClick(e, false);
        }

        // Hover on preview bar
        private void PreviewEnter(object sender, PointerRoutedEventArgs e)
        {
            ShowPreviews.Begin();
        }
        
        // Leave preview bar
        private void PreviewLeave(object sender, PointerRoutedEventArgs e)
        {
            HidePreviews.Begin();
        }
        
        // Click on preview bar
        private void PreviewBar_OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var parent = ((FrameworkElement)e.OriginalSource).Parent;
            var index = PreviewImageContainer.Children.IndexOf((FrameworkElement)parent);
            // Print out class of clicked child
            Debug.WriteLine(index);

            // Invalid element clicked
            if (index == -1 || e.OriginalSource.GetType() != typeof(Image)) return;
            
            // Figure out the requested index
            var middleIndex = (int)Math.Floor(PreviewImageContainer.Children.Count / 2.0);
            var fileIndex = CurrentFolderIndex + (index - middleIndex);
            if (fileIndex < 0 || fileIndex >= CurrentFolder.SubFiles.Count) return;
            
            // Load the file
            CurrentFolderIndex = fileIndex;
            Load();
        }
        

        // // // WINDOW EVENTS // // //
        

        // Runs when the window changes focus
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)Application.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)Application.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void ImageViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (FileViewer.Source != null)
                ((BitmapImage)FileViewer.Source).DecodePixelHeight = (int)FileHolder.ActualHeight;
        }
        
        // Runs when file(s) have been dropped on the main window
        private async void Window_OnDrop(object sender, DragEventArgs e)
        {
            // Only accept files and folders
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            // Show loading animation while building tree
            CurrentFolder = null;
            Clear();
            FileViewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/Loading.gif"));
            FileViewer.Stretch = Stretch.None;
            await TreeHandler.BuildTree(items, FileTreeView);
            FileViewer.Source = null;
            FileViewer.Stretch = Stretch.Uniform;

            // By default load the first dropped root
            CurrentFolder = TreeHandler.LoadRootNode(0);

            SwitchFolder(CurrentFolder);
        }

        // Shows what drag operations are allowed
        private void Window_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
