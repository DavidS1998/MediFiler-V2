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
        public StackPanel PreviewImageContainer1 { get => PreviewImageContainer; set => PreviewImageContainer = value; }
        public Grid FileHolder1 { get => FileHolder; set => FileHolder = value; }
        public Image FileViewer1 { get => FileViewer; set => FileViewer = value; }

        public MainWindowModel _model;
        
        // TODO: Move most methods to a Model class

        // Initialize window
        public MainWindow()
        {
            InitializeComponent();
            
            // Hide default title bar.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Activated += MainWindow_Activated;
            
            _model = new MainWindowModel(this);
        }


        // // // UI EVENTS // // //
        
        
        // Scroll between files
        public void MouseWheelScrollHandler(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(FileViewer).Properties.MouseWheelDelta;
            if (delta == 0 || _model.CurrentFolder == null) return;
            var increment = delta > 0 ? -1 : 1;
            if (_model.CurrentFolderIndex + increment < 0 || _model.CurrentFolderIndex + increment >= _model.CurrentFolder.SubFiles.Count) return;

            _model.CurrentFolderIndex += increment;
            _model.Load();
        }
        

        // Handler for the folder list
        private void FolderListClick(RoutedEventArgs e, bool leftClick)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is not FileSystemNode respectiveNode) return;
            
            if (leftClick)
            {
                _model.SwitchFolder(respectiveNode);
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
            var fileIndex = _model.CurrentFolderIndex + (index - middleIndex);
            if (fileIndex < 0 || fileIndex >= _model.CurrentFolder.SubFiles.Count) return;
            
            // Load the file
            _model.CurrentFolderIndex = fileIndex;
            _model.Load();
        }
        
        private void FileAction_RightClick(object sender, RightTappedRoutedEventArgs e)
        {
            var path = _model.CurrentFolder.SubFiles[_model.CurrentFolderIndex].Path;
            
            // TODO: Change action based on file type
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = path;
            psi.UseShellExecute = true;
            Process.Start(psi);
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
            _model.CurrentFolder = null;
            _model.Clear();
            FileViewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/Loading.gif"));
            FileViewer.Stretch = Stretch.None;
            await TreeHandler.BuildTree(items, FileTreeView);
            FileViewer.Source = null;
            FileViewer.Stretch = Stretch.Uniform;

            // By default load the first dropped root
            _model.CurrentFolder = TreeHandler.LoadRootNode(0);

            _model.SwitchFolder(_model.CurrentFolder);
        }

        // Shows what drag operations are allowed
        private void Window_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
