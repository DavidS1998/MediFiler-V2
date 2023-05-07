using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;
using BitmapImage = Microsoft.UI.Xaml.Media.Imaging.BitmapImage;
using Expander = ABI.Microsoft.UI.Xaml.Controls.Expander;
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
        public Grid AppTitleBar1 { get => AppTitleBar; set => AppTitleBar = value; }
        public TextBlock AppTitleTextBlock1 { get => AppTitleTextBlock; set => AppTitleTextBlock = value; }
        public TextBlock InfoTextBlock1 { get => InfoTextBlock; set => InfoTextBlock = value; }
        public StackPanel PreviewImageContainer1 { get => PreviewImageContainer; set => PreviewImageContainer = value; }
        public Grid FileHolder1 { get => FileHolder; set => FileHolder = value; }
        public Image ImageViewer1 { get => ImageViewer; set => ImageViewer = value; }
        public TextBlock TextViewer1 { get => TextViewer; set => TextViewer = value; }
        public ScrollViewer TextHolder1 { get => TextHolder; set => TextHolder = value; }
        public Grid VideoHolder1 { get => VideoHolder; set => VideoHolder = value; }
        public TreeView FileTreeView1 { get => FileTreeView; set => FileTreeView = value; }
        
        public AppBarButton RefreshButton1 { get => RefreshButton; set => RefreshButton = value; }
        public AppBarButton RebuildButton1 { get => RebuildButton; set => RebuildButton = value; }
        public AppBarButton RenameButton1 { get => RenameButton; set => RenameButton = value; }
        public AppBarButton UndoButton1 { get => UndoButton; set => UndoButton = value; }
        public AppBarButton DeleteButton1 { get => DeleteButton; set => DeleteButton = value; }
        public AppBarButton PlusButton1 { get => PlusButton; set => PlusButton = value; }
        public AppBarButton MinusButton1 { get => MinusButton; set => MinusButton = value; }
        public AppBarButton OpenButton1 { get => OpenButton; set => OpenButton = value; }
        public AppBarButton UpscaleButton1 { get => UpscaleButton; set => UpscaleButton = value; }

        private AppWindow _appWindow;
        private readonly MainWindowModel _model;
        private bool _sortPanelPinned = true;
        
        public DispatcherQueue dispatcherQueue;

        // // // INITIALIZATION // // //
        
        /// Initialize window
        public MainWindow()
        {
            Debug.Write("!!!!!!!!!!!! VERSION: " + typeof(string).Assembly.ImageRuntimeVersion);

            
            InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;
            
            // Get window handle, for fullscreen
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(myWndId);
            
            _model = new MainWindowModel(this);
            SetSelectedItem("Home");
            
            ReadJsonFile(); // Loads settings
            UpdateHomeFolders();

            _imageTransformGroup.Children.Add(_translateTransform);
            _imageTransformGroup.Children.Add(_scaleTransform);
            ImageViewer.RenderTransform = _imageTransformGroup;
        }

        public void UpdateFavoriteList()
        {
            FavoriteView.ItemsSource = FavoriteFolders.Values.OrderBy(x => x.Name);
        }

        public void ToggleFullscreen()
        {
            if (_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                // Restore window
                _appWindow.SetPresenter(AppWindowPresenterKind.Default);
                MainContent.RowDefinitions[0].Height = new GridLength(32);
                MainContent.RowDefinitions[1].Height = new GridLength(48);
                ExtendsContentIntoTitleBar = true;
                SideNav.IsPaneVisible = true;
                MoveHelper.Y = 135;
                ((DoubleAnimation)HidePreviews.Children[0]).To = 135;
            }
            else
            {
                // Fullscreen window
                _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                MainContent.RowDefinitions[0].Height = new GridLength(0);
                MainContent.RowDefinitions[1].Height = new GridLength(0);
                ExtendsContentIntoTitleBar = false;
                SideNav.IsPaneVisible = false;
                MoveHelper.Y = 150;
                ((DoubleAnimation)HidePreviews.Children[0]).To = 150;
            }
        }
        
        
        // // // TOP NAVIGATION // // //
        #region TOP NAVIGATION

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

        #endregion
        // // // UI EVENTS // // //
        #region UI Events
        
        
        /// Scroll between files
        public void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(ImageViewer).Properties.MouseWheelDelta;
            if (delta == 0 || _model.CurrentFolder == null) return;
            var increment = delta > 0 ? -1 : 1;

            // Action mode
            if (_model.FileActionInProgress)
            { ScrollAction(increment, e); return; }

            // Scroll through files in folder
            if (_model.CurrentFolderIndex + increment < 0 ||
                _model.CurrentFolderIndex + increment >= _model.CurrentFolder.SubFiles.Count) return;
            _model.CurrentFolderIndex += increment;
            _model.Load();
        }

        /// Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileSystemNode respectiveNode) return;
            var originalName = ((FrameworkElement)e.OriginalSource).Name;
            if (originalName == "ExpandCollapseChevron") return;
            if (originalName == "RootGrid") return;
            //Debug.WriteLine( ((FrameworkElement)e.OriginalSource).Name );

            if (leftClick)
            {
                respectiveNode.IsCurrentFolder = true;
                respectiveNode.IsExpanded = true;
                respectiveNode.UpdateAsLoaded();
                _model.CurrentFolder.IsCurrentFolder = false;
                _model.SwitchFolder(respectiveNode); 
            }
            else
            {
                _model.MoveFile(respectiveNode);
                _model.CurrentFolder.FolderColor = true;
                respectiveNode.FolderColor = true; 
            }
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
            if (((FrameworkElement)e.OriginalSource).Name != "FileHolder" && 
                ((FrameworkElement)e.OriginalSource).Name != "ImageViewer" &&
                ((FrameworkElement)e.OriginalSource).Name != "TextViewer") return;
            _model.FileAction();
        }
        
                
        // Toggle showing folder buttons
        private void FolderItem_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var treeViewItem = sender as TreeViewItem;
            var dropDownButton = FindVisualChild<DropDownButton>(treeViewItem);
            dropDownButton.Visibility = Visibility.Visible;
        }

        private void FolderItem_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            var treeViewItem = sender as TreeViewItem;
            var dropDownButton = FindVisualChild<DropDownButton>(treeViewItem);
            dropDownButton.Visibility = Visibility.Collapsed;
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T) return (T)child;
                else
                {
                    var result = FindVisualChild<T>(child);
                    if (result != null) return result;
                }
            }
            return null;
        }

        // New folder
        private void NewFolder_OnPointerPressed(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            if (((FrameworkElement)tappedRoutedEventArgs.OriginalSource).DataContext is not FileSystemNode node) return;
            _model.CreateFolderDialog(node);
        }
        
        // Rename folder
        private void RenameFolder_OnPointerPressed(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            if (((FrameworkElement)tappedRoutedEventArgs.OriginalSource).DataContext is not FileSystemNode node) return;
            _model.RenameFolderDialog(node);
        }
        
        // Delete folder
        private void DeleteFolder_OnPointerPressed(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            if (((FrameworkElement)tappedRoutedEventArgs.OriginalSource).DataContext is not FileSystemNode node) return;
            _model.DeleteFolderDialog(node);
        }
        
        

        #endregion
        // // // WINDOW EVENTS // // //
        #region WINDOW EVENTS
        

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
            if (ImageViewer.Source != null)
                ((BitmapImage)ImageViewer.Source).DecodePixelHeight = (int)FileHolder.ActualHeight;
        }
        
        private void SortView_OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_sortPanelPinned) return;
            SortPanel.Opacity = 1;
        }

        private void SortView_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_sortPanelPinned) return;
            SortPanel.Opacity = 0;
        }
        
        private void FullscreenChecker_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen) return;
            MainContent.RowDefinitions[0].Height = new GridLength(32);
            MainContent.RowDefinitions[1].Height = new GridLength(48);
            ExtendsContentIntoTitleBar = true;
        }

        private void FullscreenChecker_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen) return;
            MainContent.RowDefinitions[0].Height = new GridLength(0);
            MainContent.RowDefinitions[1].Height = new GridLength(0);
            ExtendsContentIntoTitleBar = false;
        }
        
        // Pin
        private void TogglePin()
        {
            SortPanel.Opacity = _sortPanelPinned ? 1 : 0;
            PinButton.Icon = _sortPanelPinned ? new SymbolIcon(Symbol.UnPin) : new SymbolIcon(Symbol.Pin);
            
            UpdateJsonFile();
            // Save _sortPanelPinned to settings
            //ApplicationDataContainer  localSettings = ApplicationData.Current.LocalSettings;
            //localSettings.Values["SortPanelPinned"] = _sortPanelPinned.ToString();
        }


        #endregion
        // // // LOADING // // //
        
        
        // Loads a folder from an arbitrary context (such as drag and drop or the file picker)
        private async void LoadFolder(IReadOnlyList<IStorageItem> items)
        {
            // Show loading animation while building tree
            _model.CurrentFolder = null;
            _model.Clear(false);
            ImageViewer.Visibility = Visibility.Visible;
            TextHolder.Visibility = Visibility.Collapsed;
            VideoHolder.Visibility = Visibility.Collapsed;
            
            ImageViewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/Loading.gif"));
            ImageViewer.Stretch = Stretch.None;
            await TreeHandler.BuildTree(items, FileTreeView);
            StartLoadFilesInBackground();
            ImageViewer.Source = null;
            ImageViewer.Stretch = Stretch.Uniform;

            // By default load the first dropped root
            _model.CurrentFolder = TreeHandler.LoadRootNode(0);
            _model.CurrentFolder.IsCurrentFolder = true;
            _model.SwitchFolder(_model.CurrentFolder);
            
            RebuildButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            
            AddQuickFolder(_model.CurrentFolder);
        }
        
        // Will update the entire tree at once when this is done which may take a while,
        // but the initial load is much faster in exchange
        public async void StartLoadFilesInBackground()
        {
            await Task.Run(() => LoadFiles());
            foreach (var folder in TreeHandler.FullFolderList)
                { folder.UpdateAsLoaded(); }
        }
        
        // Load files
        public void LoadFiles()
        {
            try
            {
                // Performance measuring
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                
                // Simultaneously load files from all folders
                Parallel.ForEach(TreeHandler.FullFolderList, folder =>
                { folder.GetSubFiles().Wait(); });
                
                // Print performance data
                stopwatch.Stop();
                Debug.WriteLine("Files loaded in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                Debug.WriteLine("Interrupted:" + e);
            }
        } 
        
        /// Runs when file(s) have been dropped on the main window
        private async void Window_OnDrop(object sender, DragEventArgs e)
        {
            // Only accept files and folders
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            OpenSortView();
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
            var hwnd = WindowNative.GetWindowHandle(this);     
            InitializeWithWindow.Initialize(openPicker, hwnd);
            
            var folder = await openPicker.PickSingleFolderAsync();

            if (folder == null) return;

            // Convert StorageFolder to IStorageItem
            var item = (IStorageItem)folder;

            // Load folder
            OpenSortView();
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
                item = file;
            }
            else
            {
                var folder = StorageFolder.GetFolderFromPathAsync(quickFolder.Path).GetAwaiter().GetResult();
                item = folder;
            }

            // Load folder
            OpenSortView();
            LoadFolder(new List<IStorageItem> {item});
        }
        
        // Add/remove favorite
        private void QuickFolder_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not QuickFolder quickFolder) return;

            if (FavoriteFolders.ContainsKey(quickFolder.Path))
            {
                FavoriteFolders.Remove(quickFolder.Path);
            }
            else
            {
                FavoriteFolders.TryAdd(quickFolder.Path, quickFolder);
            }
            UpdateJsonFile();
            UpdateHomeFolders();
        }
        
        
        // TODO: Refactor into own class
        // TODO: Add a favorites list
        // // // JSON // // //
        #region JSON

        public class SettingsRoot
        {
            public Dictionary<string, QuickFolder> QuickFolders { get; set; }
            public Dictionary<string, QuickFolder> FavoriteFolders { get; set; }
            public bool SortPanelPinned { get; set; }
        }
        private Dictionary<string, QuickFolder> QuickFolders = new();
        private Dictionary<string, QuickFolder> FavoriteFolders = new();
        
        // Create QuickFolder from appsettings.json
        public void UpdateHomeFolders()
        {
            RecentFoldersView.ItemsSource = null;
            MostOpenedFoldersView.ItemsSource = null;
            
            RecentFoldersView.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.LastOpened);
            MostOpenedFoldersView.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.TimesOpened);
            FavoriteFoldersView.ItemsSource = FavoriteFolders.Values.OrderBy(x => x.Name);
            
            UpdateFavoriteList();
        }
        
        private void ReadJsonFile()
        {
            // Deserialize JSON to QuickFolders list
            var json = File.ReadAllText("appsettings.json");
            var rootObject = JsonSerializer.Deserialize<SettingsRoot>(json);
            if (rootObject != null) QuickFolders = rootObject.QuickFolders;
            if (rootObject != null) FavoriteFolders = rootObject.FavoriteFolders;
            if (rootObject != null) _sortPanelPinned = rootObject.SortPanelPinned;
            TogglePin();
        }

        private void AddQuickFolder(FileSystemNode node)
        {
            if (node.IsFile) return;
            
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
            var quickFolder = new QuickFolder(node.Name)
            {
                Path = node.Path,
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
            // Create a dictionary with a single key-value pair for QuickFolders
            var quickFolders = new Dictionary<string, QuickFolder>();
            foreach (var folder in QuickFolders)
            {
                quickFolders.Add(folder.Key, folder.Value);
            }

            var dictionary = new Dictionary<string, object>
            {
                { "QuickFolders", quickFolders },
                { "FavoriteFolders", FavoriteFolders },
                { "SortPanelPinned", _sortPanelPinned }
            };

            // Serialize the dictionary to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(dictionary, options);

            // Serialize and write the prettified JSON to appsettings.json
            File.WriteAllText("appsettings.json", json);
        }


        
        #endregion
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
        { _model.RenameDialog(); }        
        
        // Undo button
        private void UndoButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.Undo(); }  
        
        // Undo button
        private void DeleteButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.DeleteFile(); }     
        
        // Pin button
        private void Pin_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _sortPanelPinned = !_sortPanelPinned; TogglePin(); }    
        
        // Fullscreen button
        private void Fullscreen_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { ToggleFullscreen(); }
        
        // Plus button
        private void PlusButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.AddPlus(); }
        
        // Minus button
        private void MinusButton_OnPointerReleased(object sender, TappedRoutedEventArgs e)
        { _model.RemovePlus(); }
        
        // Open in button
        private void OpenButton_OnTapped(object sender, TappedRoutedEventArgs e)
        { _model.OpenAction(); }    
        
        // Upscale button
        private void UpscaleButton_OnTapped(object sender, TappedRoutedEventArgs e)
        { _model.Upscale(2); }


        // // // KEYBOARD SHORTCUTS // // //
        
        
        // F5 - Refresh
        private void Refresh_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.Refresh(); args.Handled = true; } 
        
        // Shift + F5 - Full refresh
        private void FullRefresh_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.FullRefresh(); args.Handled = true; }
        
        // F2 - Rename
        private void Rename_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.RenameDialog(); args.Handled = true; }        
        
        // CTRL + Z - Undo
        private void Undo_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.Undo(); args.Handled = true; }      
        
        // Delete - Delete file
        private void Delete_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.DeleteFile(); args.Handled = true; }
        
        // Tab - Pin
        private void Pin_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _sortPanelPinned = !_sortPanelPinned; TogglePin(); args.Handled = true; }        
        
        // F11 - Fullscreen
        private void Fullscreen_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { ToggleFullscreen(); args.Handled = true; }        
        
        // F6 - Plus
        private void Plus_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.AddPlus(); args.Handled = true; }
        
        // F7 - Minus
        private void Minus_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.RemovePlus(); args.Handled = true; }
        
        // F8 - Upscale
        private void Upscale_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { _model.Upscale(2); args.Handled = true; }

        
        // // // FILE MANIPULATION // // //
        // TODO: Lots of bugs in this section


        private void ScrollAction(int increment, PointerRoutedEventArgs e)
        {
            switch (FileTypeHelper.GetFileCategory(_model.CurrentFolder.SubFiles[_model.CurrentFolderIndex].Path))
            {
                case FileTypeHelper.FileCategory.IMAGE:
                    var mousePosition = e.GetCurrentPoint(ImageViewer).Position;
                    // Check if position is on image
                    if (mousePosition.X < 0 || mousePosition.X > ImageViewer.ActualWidth ||
                        mousePosition.Y < 0 || mousePosition.Y > ImageViewer.ActualHeight) return;
                    ZoomImage(increment, mousePosition);
                    break;
                case FileTypeHelper.FileCategory.VIDEO:
                    break;
                case FileTypeHelper.FileCategory.TEXT:
                    break;
                case FileTypeHelper.FileCategory.OTHER:
                    break;
            }
        }
        
        private readonly TransformGroup _imageTransformGroup = new();
        private readonly TranslateTransform _translateTransform = new();
        private readonly ScaleTransform _scaleTransform = new();
        
        
        // Zoom image on cursor
        private void ZoomImage(int increment, Point mousePosition)
        {
            // Calculate the new scale based on the current scale and the mouse wheel delta
            var scaleDelta = increment > 0 ? -0.1 : 0.1;
            // Get the current scale transform inside the TransformGroup
            var newScale = _scaleTransform.ScaleX + scaleDelta;

            // Limit the zoom scale to a reasonable range
            if (newScale is < 0.5 or > 10) { return; }

            // Get the position of the mouse cursor relative to the image
            var imageWidth = SortView.ActualWidth / 2;
            var imageHeight = SortView.ActualHeight / 2;
            var xRatio = mousePosition.X / imageWidth;
            var yRatio = mousePosition.Y / imageHeight;

            // Adjust the scale transform to zoom in on the mouse cursor
            _scaleTransform.CenterX = imageWidth * xRatio;
            _scaleTransform.CenterY = imageHeight * yRatio;
            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;


            // Readjust the image size
            if (ImageViewer.Source == null) return;
            ((BitmapImage)ImageViewer.Source).DecodePixelHeight = (int)ImageViewer.Height;
        }

        // Reset image
        public void ResetImage()
        {
            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            
            //FileHolder.RenderTransform = _imageTransformGroup;

            if (ImageViewer.Source != null)
            {
                ((BitmapImage)ImageViewer.Source).DecodePixelHeight = (int)ImageViewer.ActualHeight;
            }
        }
        
        
        // Panning
        private bool _isDragging;
        private Point _startPosition;
        private Point _previousPosition;

        private Point _lastTransformPosition;

        //private Double _lastX;
        // PAN
        private void FileHolder_OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ImageViewer.PointerCaptures == null || !_isDragging) return;

            var currentMousePosition = e.GetCurrentPoint(null).Position;

            
            // Offsetting
            var windowSize = new Point(SortPanel.ActualWidth, SortPanel.ActualHeight);
            var windowOffsetY = MainContent.RowDefinitions[0].ActualHeight + MainContent.RowDefinitions[1].ActualHeight;
            var windowOffsetX = 48;
            if (_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            { windowOffsetX = 0; }

            var convertAbsoluteToRelative = new Point(
                    currentMousePosition.X - windowSize.X / 2 - windowOffsetX, 
                    currentMousePosition.Y - windowSize.Y / 2 - windowOffsetY);

            var newImagePositionX = convertAbsoluteToRelative.X;
            var newImagePositionY = convertAbsoluteToRelative.Y;

            // TODO: Investigate a proper way to do this
            
            // Grabs from middle - Wrong, but most intuitive
            _translateTransform.X = newImagePositionX;
            _translateTransform.Y = newImagePositionY;
            
            // Position relative to entire window - Wrong
            //_translateTransform.X = _previousPosition.X + convertAbsoluteToRelative.X;
            //_translateTransform.Y = _previousPosition.Y + convertAbsoluteToRelative.Y;
            
            // Too fast - Wrong
            //_translateTransform.X = _lastTransformPosition.X;
            //_translateTransform.Y = _lastTransformPosition.Y;

            //
            _lastTransformPosition = new Point(_translateTransform.X, _translateTransform.Y);
            _previousPosition = convertAbsoluteToRelative;
        }

        
        
        
        
        
        
        private void FileHolder_OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_model.FileActionInProgress) return;
            if (!e.GetCurrentPoint(ImageViewer).Properties.IsLeftButtonPressed) return;
            
            _isDragging = true;
            _startPosition = e.GetCurrentPoint(null).Position;
            //_startPosition = new Point(FileViewer.ActualWidth / 2, FileViewer.ActualHeight / 2);
            ImageViewer.CapturePointer(e.Pointer);
            
        }

        private void FileHolder_OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_model.FileActionInProgress ||!_isDragging) return;

            _isDragging = false;
            ImageViewer.ReleasePointerCapture(e.Pointer);
        }
        
        // Open folder
        private void Favorite_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            QuickFolderClick(sender, e);
        }

        // 
        private void Favorite_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var quickFolder = (QuickFolder) ((FrameworkElement) sender).DataContext;
            if (quickFolder == null) return;
            
            // Get storage item at path
            IStorageFolder folder = StorageFolder.GetFolderFromPathAsync(quickFolder.Path).AsTask().Result;
            if (folder == null) return;
            
            var node = new FileSystemNode(folder, 0, null, true);
            _model.MoveFile(node);
        }
    }
}
