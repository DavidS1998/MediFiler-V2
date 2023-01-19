// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
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
        private Dictionary<int, BitmapImage> lowResImages = new Dictionary<int, BitmapImage>();

        private int preloadCount = 2;
        private Thread preloadThread;
        private bool preloadThreadRunning;

        private List<FileNode> rootNodes = new();
        private List<FileNode> fullFolderList = new();

        private FileNode currentFolder;
        private int currentFolderIndex = 0;

        public MainWindow()
        {
            this.InitializeComponent();

            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;

            //
            preloadThread = new Thread(new ThreadStart(PreloadImages));
        }

        private void DisplayCurrentFile()
        {
            // TODO: This should be done on a separate UI thread

            if (currentFolder == null) return;
            if (currentFolder.SubFiles.Count <= 0) return;

            // TODO: Handle different file types

            FileViewer.Source = new BitmapImage(new Uri(currentFolder.SubFiles[currentFolderIndex].Path));

            /*
            if (lowResImages.ContainsKey(currentFileIndex))
            {
                //BitmapImage highResBitmap = new BitmapImage(new Uri(fileTree.root.Children[currentFileIndex].Name));
                //FileViewer.Source = highResBitmap;
            }
            else
            {
                //FileViewer.Source = new BitmapImage(new Uri(fileTree.root.Children[currentFileIndex].Name));
            }*/
        }

        private void PreloadImages()
        {
            /*
            while (preloadThreadRunning)
            {
                for (int i = currentFileIndex - preloadCount; i <= currentFileIndex + preloadCount; i++)
                {
                    if (i >= 0 && i < fileTree.root.Children.Count)
                    {
                        // Create a new instance of the BitmapImage class with the low resolution settings
                        BitmapImage lowResBitmap = new BitmapImage();
                        lowResBitmap.DecodePixelWidth = 200;
                        lowResBitmap.UriSource = new Uri(fileTree.root.Children[i].Name);
                        // Store the low resolution image in a dictionary with the key being the index of the image
                        lowResImages[i] = lowResBitmap;
                    }
                }
            }
            */
        }


        // Runs when the window changes focus
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
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



        private async void Window_OnDrop(object sender, DragEventArgs e)
        {
            // Only accept files and folders
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            // Should run in the background
            await buildTree(items);

            currentFolder = fullFolderList.First();
            DisplayCurrentFile();
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
                // TODO: Handle files as root nodes
                // Extract all root nodes
                Parallel.ForEach(filesAndFolders, path => rootNodes.Add(new FileNode(path, 0, null)));

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
            if (currentFolderIndex + increment < 0 || currentFolderIndex + increment >= currentFolder.SubFiles.Count) return;

            currentFolderIndex += increment;
            DisplayCurrentFile();
        }

        private void SwitchFolder(FileNode newFolder)
        {
            currentFolder = newFolder;
            currentFolderIndex = 0;
            FileViewer.Source = null;
            DisplayCurrentFile();
        }

        // Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            FileNode respectiveNode = ((FrameworkElement)e.OriginalSource).DataContext as FileNode;
            if (respectiveNode == null) return;

            SwitchFolder(respectiveNode);

            /*
            // TODO: Temp, left click reveals files, right click folders
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
    }
}
