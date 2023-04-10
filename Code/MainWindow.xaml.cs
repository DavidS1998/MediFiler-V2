// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using BitmapImage = Microsoft.UI.Xaml.Media.Imaging.BitmapImage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediFiler_V2.Code
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // TODO: Stop using global variables
        private FileNode _currentFolder;
        private int _currentFolderIndex;
        private int _latestLoadedImage = -1;
        
        
        // Initialize window
        public MainWindow()
        {
            InitializeComponent();

            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;
        }

        // Load file
        private void Load()
        {
            // Nothing to load
            if (_currentFolder == null || _currentFolder.SubFiles.Count <= 0) return;
        
            var currentFile = _currentFolder.SubFiles[_currentFolderIndex];

            ShowMetadata(currentFile);
            DisplayCurrentFile(currentFile);
            FileThumbnail.PreloadThumbnails(_currentFolderIndex, _currentFolder);
        }

        // Gets secondary data from the current file
        private void ShowMetadata(FileNode file)
        {
            var metadataText = "";
            metadataText += "(" + (_currentFolderIndex + 1) + "/" + (_currentFolder.SubFiles.Count) + ") ";
            metadataText += file.Name;

            AppTitleTextBlock.Text = metadataText;
        }
        
        
        // // // FILE DISPLAY  // // //
        

        // Decides how each file type should be shown
        private void DisplayCurrentFile(FileNode file)
        {
            DisplayThumbnail(file);
            
            switch (FileTypeHelper.GetFileCategory(file.Path))
            {
                case FileTypeHelper.FileCategory.IMAGE:
                    DisplayImage(file);
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
        public async void DisplayImage(FileNode file)
        {
            var sentInIndex = _currentFolderIndex; // File change check
            var sentInFolder = _currentFolder.Path; // Context change check
            var bitmap = await FileImage.LoadImage(file, (int)FileHolder.ActualHeight);
            // Throw away result if current file changed; Invalid images don't overwrite thumbnails
            if (sentInIndex != _currentFolderIndex || sentInFolder != _currentFolder.Path || bitmap == null) return;
            _latestLoadedImage = sentInIndex; // Stops thumbnail from overwriting image
            FileViewer.Source = bitmap;
        }
        
        // Creates BitMap from File Explorer thumbnail and sets it as FileViewer source
        private async void DisplayThumbnail(FileNode file)
        {
            var sentInIndex = _currentFolderIndex; // File change check
            await FileThumbnail.SaveThumbnailToCache(file.Path, sentInIndex);
            // Don't overwrite full imagwes; Don't show if file changed
            if (_latestLoadedImage == _currentFolderIndex || sentInIndex != _currentFolderIndex) return;
            FileViewer.Source = FileThumbnail.ThumbnailCache[sentInIndex];
        }
        
        
        // // // NAVIGATION // // //
        

        // Scroll between files
        private void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(FileViewer).Properties.MouseWheelDelta;
            if (delta == 0) return;
            var increment = delta > 0 ? -1 : 1;
            if (_currentFolder == null) return;
            if (_currentFolderIndex + increment < 0 || _currentFolderIndex + increment >= _currentFolder.SubFiles.Count) return;

            _currentFolderIndex += increment;
            Load();
        }

        // For loading a different folder context
        private void SwitchFolder(FileNode newFolder, int position = 0)
        {
            _currentFolder = newFolder;
            // Set position if within bounds
            _currentFolderIndex = position < 0 ? 0 : (position > newFolder.SubFiles.Count ? 0 : position);
            Clear();
            Load();
        }

        // Clear all loaded content
        private void Clear()
        {
            _latestLoadedImage = -1;
            FileThumbnail.ThumbnailCache.Clear();
            AppTitleTextBlock.Text = "MediFiler";
            FileViewer.Source = null;
        }

        
        // // // UI EVENTS // // //
        

        // Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileNode respectiveNode) return;
            
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


        private void PreviewEnter(object sender, PointerRoutedEventArgs e)
        {
            ShowPreviews.Begin();
        }
        private void PreviewLeave(object sender, PointerRoutedEventArgs e)
        {
            HidePreviews.Begin();
        }
        

        // // // WINDOW EVENTS // // //
        

        // Runs when the window changes focus
        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
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
            _currentFolder = null;
            Clear();
            FileViewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/Loading.gif"));
            FileViewer.Stretch = Stretch.None;
            await TreeHandler.BuildTree(items, FileTreeView);
            FileViewer.Source = null;
            FileViewer.Stretch = Stretch.Uniform;

            // By default load the first dropped root
            _currentFolder = TreeHandler.LoadRootNode(0);

            SwitchFolder(_currentFolder);
        }

        // Shows what drag operations are allowed
        private void Window_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
