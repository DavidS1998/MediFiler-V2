using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using BitmapImage = Microsoft.UI.Xaml.Media.Imaging.BitmapImage;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

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
        public StackPanel PreviewImageContainer1 { get => PreviewImageContainer; set => PreviewImageContainer = value; }
        public Grid FileHolder1 { get => FileHolder; set => FileHolder = value; }
        public Image FileViewer1 { get => FileViewer; set => FileViewer = value; }
        public TreeView FileTreeView1 { get => FileTreeView; set => FileTreeView = value; }
        
        public AppBarButton RefreshButton1 { get => RefreshButton; set => RefreshButton = value; }
        public AppBarButton RebuildButton1 { get => RebuildButton; set => RebuildButton = value; }
        public AppBarButton RenameButton1 { get => RenameButton; set => RenameButton = value; }
        public AppBarButton UndoButton1 { get => UndoButton; set => UndoButton = value; }
        public AppBarButton DeleteButton1 { get => DeleteButton; set => DeleteButton = value; }

        private readonly MainWindowModel _model;

        // // // INITIALIZATION // // //

        /// Initialize window
        public MainWindow()
        {
            InitializeComponent();
            
            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;
            
            _model = new MainWindowModel(this);
            SetSelectedItem("Home");
            
            ReadJsonFile();
            UpdateHomeFolders();
        }
        
        
        // // // TOP NAVIGATION // // //
        

        public void OpenSortView()
        {
            SortView.Visibility = Visibility.Visible;
            HomeView.Visibility = Visibility.Collapsed;
            SetSelectedItem("Sort");
        }
        
        public void OpenHomeView()
        {
            SortView.Visibility = Visibility.Collapsed;
            HomeView.Visibility = Visibility.Visible;
            SetSelectedItem("Home");
        }

        private void SetSelectedItem(string tag)
        {
            // Find the item with the matching tag and set it as the selected item.
            foreach (var item in SideNav.MenuItems)
            {
                if (item is not NavigationViewItem navItem) continue;
                if (navItem.Tag.ToString() != tag) continue;
                SideNav.SelectedItem = navItem;
                break;
            }
        }
        
        private void SideNav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item) return;
            switch (item.Tag)
            {
                case "Home":
                    OpenHomeView();
                    break;
                case "Sort":
                    OpenSortView();
                    break;
            }
        }


        // // // UI EVENTS // // //
        
        
        /// Scroll between files
        public void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(FileViewer).Properties.MouseWheelDelta;
            if (delta == 0 || _model.CurrentFolder == null) return;
            var increment = delta > 0 ? -1 : 1;
            if (_model.CurrentFolderIndex + increment < 0 || _model.CurrentFolderIndex + increment >= _model.CurrentFolder.SubFiles.Count) return;

            _model.CurrentFolderIndex += increment;
            _model.Load();
        }


        /// Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileSystemNode respectiveNode) return;

            if (leftClick)
            { _model.SwitchFolder(respectiveNode); }
            else
            { _model.MoveFile(respectiveNode); }
        }

        private void FolderListLeftClick(object sender, TappedRoutedEventArgs e)
        { FolderListClick(e, true); }

        private void FolderListRightClick(object sender, RightTappedRoutedEventArgs e)
        { FolderListClick(e, false); }

        /// Hover on preview bar
        private void PreviewEnter(object sender, PointerRoutedEventArgs e)
        { ShowPreviews.Begin(); }
        
        /// Leave preview bar
        private void PreviewLeave(object sender, PointerRoutedEventArgs e)
        { HidePreviews.Begin(); }
        
        /// Click on preview bar
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
            var fileIndex = _model.CurrentFolderIndex + (index - middleIndex);
            if (fileIndex < 0 || fileIndex >= _model.CurrentFolder.SubFiles.Count) return;
            
            // Load the file
            _model.CurrentFolderIndex = fileIndex;
            _model.Load();
        }
        
        private void FileAction_RightClick(object sender, RightTappedRoutedEventArgs e)
        {
            // Only run if the clicked element has name FileHolder or FileViewer
            if (((FrameworkElement)e.OriginalSource).Name != "FileHolder" && ((FrameworkElement)e.OriginalSource).Name != "FileViewer") return;

            var path = _model.CurrentFolder.SubFiles[_model.CurrentFolderIndex].Path;
            
            // TODO: Change action based on file type
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = path;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        

        // // // WINDOW EVENTS // // //
        

        /// Runs when the window changes focus
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
        
        // Loads a folder from an arbitrary context (such as drag and drop or the file picker)
        private async void LoadFolder(IReadOnlyList<IStorageItem> items)
        {
            // Show loading animation while building tree
            _model.CurrentFolder = null;
            _model.Clear(false);
            FileViewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/Loading.gif"));
            FileViewer.Stretch = Stretch.None;
            await TreeHandler.BuildTree(items, FileTreeView);
            FileViewer.Source = null;
            FileViewer.Stretch = Stretch.Uniform;

            // By default load the first dropped root
            _model.CurrentFolder = TreeHandler.LoadRootNode(0);
            _model.SwitchFolder(_model.CurrentFolder);
            
            RebuildButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            
            AddQuickFolder(_model.CurrentFolder);
            OpenSortView();
        }
        
        /// Runs when file(s) have been dropped on the main window
        private async void Window_OnDrop(object sender, DragEventArgs e)
        {
            // Only accept files and folders
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            LoadFolder(items);
        }

        /// Shows what drag operations are allowed
        private void Window_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        /// Folder picker
        private async void AddFolderDialog()
        {
            var openPicker = new FolderPicker()
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.Thumbnail,
                FileTypeFilter = { "*" },
            };

            // System initializer for FolderPicker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);     
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            
            var folder = await openPicker.PickSingleFolderAsync();

            if (folder == null) return;

            // Convert StorageFolder to IStorageItem
            var item = (IStorageItem)folder;

            // Load folder
            LoadFolder(new List<IStorageItem> {item});
        }
        
        /// Quick folder
        private void QuickFolderClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext == null) return;
            var quickFolder = (QuickFolder)((FrameworkElement)sender).DataContext;
            
            // Does not exist
            if (!Directory.Exists(quickFolder.Path) && !File.Exists(quickFolder.Path))
            {
                RemoveQuickFolder(quickFolder.Path);
                return;
            }

            IStorageItem item;
            if (!Directory.Exists(quickFolder.Path))
            {
                var file = StorageFile.GetFileFromPathAsync(quickFolder.Path).GetAwaiter().GetResult();
                item = (IStorageItem)file;
            }
            else
            {
                var folder = StorageFolder.GetFolderFromPathAsync(quickFolder.Path).GetAwaiter().GetResult();
                item = (IStorageItem)folder;
            }

            // Load folder
            LoadFolder(new List<IStorageItem> {item});
        }
        
        
        // TODO: Refactor into own class
        // TODO: Add a favorites list
        // // // JSON // // //
        
        
        private Dictionary<string, QuickFolder> QuickFolders = new();
        
        // Create QuickFolder from appsettings.json
        public void UpdateHomeFolders()
        {
            RecentFoldersView.ItemsSource = null;
            MostOpenedFoldersView.ItemsSource = null;
            
            RecentFoldersView.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.LastOpened);
            MostOpenedFoldersView.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.TimesOpened);
        }
        
        private void ReadJsonFile()
        {
            // Deserialize JSON to QuickFolders list
            var json = File.ReadAllText("appsettings.json");
            QuickFolders = JsonSerializer.Deserialize<Dictionary<string, QuickFolder>>(json);
        }

        private void AddQuickFolder(FileSystemNode node)
        {
            // Opened before
            if (QuickFolders.ContainsKey(node.Path))
            {
                QuickFolders[node.Path].LastOpened = DateTime.Now;
                QuickFolders[node.Path].TimesOpened++;
                UpdateJsonFile();
                UpdateHomeFolders();
                return;
            }
            
            // New QuickFolder
            var quickFolder = new QuickFolder
            {
                Path = node.Path,
                Name = node.Name,
                LastOpened = DateTime.Now,
                TimesOpened = 1
            };
            
            QuickFolders.Add(node.Path, quickFolder);
            UpdateJsonFile();
            UpdateHomeFolders();
        }
        
        private void RemoveQuickFolder(string path)
        {
            QuickFolders.Remove(path);
            UpdateJsonFile();
            UpdateHomeFolders();
        }

        private void UpdateJsonFile()
        {
            // Serialize QuickFolders list to JSON with Path as the key
            var json = JsonSerializer.Serialize(QuickFolders);
            
            // Serialize and write the prettified JSON to appsettings.json
            using (var stream = new FileStream("appsettings.json", FileMode.Create))
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    JsonDocument.Parse(json).RootElement.WriteTo(writer);
                }
            }
        }
        
        
        // // // BUTTONS // // //
        
        
        // Add button
        private void AddButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { AddFolderDialog(); }
        
        // Refresh button
        private void RefreshButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.Refresh(); }        
        
        // Refresh all button
        private void RefreshAllButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.FullRefresh(); }        
        
        // Refresh all button
        private void RenameButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.RenameFile(); }        
        
        // Undo button
        private void UndoButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.Undo(); }  
        
        // Undo button
        private void DeleteButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.DeleteFile(); }
        
        
        // // // KEYBOARD SHORTCUTS // // //
        
        
        // F5 - Refresh
        private void Refresh_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.Refresh(); args.Handled = true; } 
        
        // Shift + F5 - Full refresh
        private void FullRefresh_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.FullRefresh(); args.Handled = true; }
        
        // F2 - Rename
        private void Rename_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.RenameFile(); args.Handled = true; }        
        
        // CTRL + Z - Undo
        private void Undo_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.Undo(); args.Handled = true; }      
        
        // Delete - Delete file
        private void Delete_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.DeleteFile(); args.Handled = true; }
    }
}
