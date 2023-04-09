// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using BitmapImage = Microsoft.UI.Xaml.Media.Imaging.BitmapImage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediFiler_V2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private Dictionary<int, BitmapImage> lowResImages = new();
        private Dictionary<int, BitmapImage> thumbnails = new();

        private List<FileNode> rootNodes = new();
        private List<FileNode> fullFolderList = new();

        private FileNode currentFolder;
        private FileNode currentFile;
        private int currentFolderIndex = 0;
        private int latestLoaded = -1;

        private int preloadDistance = 10;


        // Initialize window
        public MainWindow()
        {
            this.InitializeComponent();

            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;

        }




        // Gets secondary data from the current file
        private void ShowMetadata()
        {
            string metadataText = "";
            metadataText += "(" + (currentFolderIndex + 1) + "/" + (currentFolder.SubFiles.Count) + ") ";
            metadataText += currentFile.Name;

            AppTitleTextBlock.Text = metadataText;
        }

        // Decides how each file type should be shown
        private void DisplayCurrentFile()
        {
            // TODO: This should be done on a separate UI thread

            if (currentFolder == null) return;
            if (currentFolder.SubFiles.Count <= 0) return;

            currentFile = currentFolder.SubFiles[currentFolderIndex];

            ShowMetadata();
            //PreloadImages(currentFolderIndex);

            switch (FileTypeHelper.GetFileCategory(currentFile.Path))
            {
                case FileTypeHelper.FileCategory.IMAGE:
                    DisplayThumbnail();
                    DisplayImage();
                    break;
                case FileTypeHelper.FileCategory.VIDEO:
                    DisplayThumbnail();
                    break;
                case FileTypeHelper.FileCategory.TEXT:
                    DisplayThumbnail();
                    break;
                default:
                    DisplayThumbnail();
                    break;
            }
        }

        // Creates a BitMap from file and sets it as FileViewer source. Works with GIFs
        private async void DisplayImage()
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.DecodePixelHeight = (int)FileHolder.ActualHeight;

            int sentInIndex = currentFolderIndex;
            var sentInFolder = currentFolder.Path;

            var file = await StorageFile.GetFileFromPathAsync(currentFile.Path);
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                await bitmap.SetSourceAsync(stream);
                stream.Dispose();
            }

            // Throw away result if current file changed
            if (sentInIndex != currentFolderIndex) return;
            if (sentInFolder != currentFolder.Path) return;

            latestLoaded = sentInIndex;
            FileViewer.Source = bitmap;
        }

        // Creates BitMap from File Explorer thumbnail and sets it as FileViewer source
        private async void DisplayThumbnail()
        {
            if (thumbnails.ContainsKey(currentFolderIndex))
            {
                FileViewer.Source = thumbnails[currentFolderIndex];
                return;
            }

            int sentInIndex = currentFolderIndex;

            StorageFile file = await StorageFile.GetFileFromPathAsync(currentFile.Path);
            var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
            if (thumbnail == null) return;

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);

            // Throw away result if current file changed
            if (sentInIndex != currentFolderIndex) return;

            // Only prioritize thumbnails for unloaded images
            if (latestLoaded == currentFolderIndex) return;
            FileViewer.Source = bitmap;
        }

        // 
        private async void LoadThumbnail(string path, int index)
        {
            if (thumbnails.ContainsKey(index)) return;
            thumbnails.Add(index, null); // Lock the index

            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            StorageItemThumbnail thumbnail;
            try
            {
                thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                if (thumbnail == null)
                {
                    thumbnails.Remove(index);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed getting thumbnail for " + path + ": " + e);
                thumbnails.Remove(index);
                return;
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);

            thumbnails.Remove(index); // Unlock the index
            thumbnails.Add(index, bitmap);
        }

        //
        private void PreloadThumbnails()
        {
            for (int i = currentFolderIndex - preloadDistance; i < (currentFolderIndex + preloadDistance); i++)
            {
                if (i < currentFolder.SubFiles.Count && i >= 0)
                {
                    var path = currentFolder.SubFiles[i].Path;
                    LoadThumbnail(path, i);
                }
            }
        }


        // Runs when file(s) have been dropped on the main window
        private async void Window_OnDrop(object sender, DragEventArgs e)
        {
            // Only accept files and folders
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            // Should run in the background
            // TODO: Indicate that something is loading while it builds the tree
            await buildTree(items);

            // By default load the first dropped root. Desired behavior?
            currentFolder = fullFolderList.First();

            SwitchFolder(currentFolder);
        }

        private async Task buildTree(IReadOnlyList<IStorageItem> filesAndFolders)
        {
            rootNodes.Clear();
            fullFolderList.Clear();
            FileTreeView.ItemsSource = null;

            // Create the file structure in the background, may be very resource intensive
            // TODO: Show loading indicator
            await Task.Run(() =>
            {
                // Extract all root nodes
                Parallel.ForEach(filesAndFolders, path => rootNodes.Add(new FileNode(path, 0)));

                // Go down each root node and build a tree
                foreach (FileNode node in rootNodes)
                {
                    addFolderToTree(node);
                }
            });

            FileTreeView.ItemsSource = fullFolderList;
        }

        // Recursively extracts folder data from nodes
        private void addFolderToTree(FileNode node)
        {
            fullFolderList.Add(node);
            foreach (FileNode subNode in node.SubFolders)
            {
                addFolderToTree(subNode);
            }
        }


        // Shows what drag operations are allowed
        private void Window_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        // Scroll between files
        private void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(FileViewer).Properties.MouseWheelDelta;
            if (delta == 0) return;
            var increment = delta > 0 ? -1 : 1;
            if (currentFolder == null) return;
            if (currentFolderIndex + increment < 0 || currentFolderIndex + increment >= currentFolder.SubFiles.Count) return;

            currentFolderIndex += increment;
            PreloadThumbnails();
            DisplayCurrentFile();
        }

        // For loading a different folder context
        private void SwitchFolder(FileNode newFolder, int Position = 0)
        {
            currentFolder = newFolder;

            // Reset index unless position requested
            currentFolderIndex = Position < 0 ? 0 : currentFolderIndex;
            currentFolderIndex = Position > newFolder.SubFiles.Count ? Position : 0;

            // Nothing has been loaded yet
            latestLoaded = -1;

            //lowResPreloadedImages.Clear();
            thumbnails.Clear();

            AppTitleTextBlock.Text = "MediFiler";
            FileViewer.Source = null;
            DisplayCurrentFile();
            PreloadThumbnails();
        }



        // // // UI EVENTS



        // Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileNode respectiveNode) return;

            // TODO: Handle left and right click differently
            SwitchFolder(respectiveNode);

            /*
            var items = leftClick ? respectiveNode.SubFiles : respectiveNode.SubFolders;

            foreach (var childNode in items)
            {
                Debug.WriteLine(childNode.Name);
            }*/
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



        // // // WINDOW EVENTS



        // Runs when the window changes focus
        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void ImageViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (FileViewer.Source != null)
                (FileViewer.Source as BitmapImage).DecodePixelHeight = (int)FileHolder.ActualHeight;
        }
    }
}
