using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Microsoft.UI;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace MediFiler_V2.Code;

public class MetadataHandler
{
    MainWindow mainWindow;
    
    public MetadataHandler(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
    }
    
    // Gets secondary data from the current file
    public void ShowMetadata(FileSystemNode fileSystem)
    {
        SetTitleText(fileSystem);
        SetInfoText(fileSystem);
    }

    private void SetTitleText(FileSystemNode fileSystem)
    {
        // Current position and name
        var titleText = "";
        titleText += "(" + (mainWindow.CurrentFolderIndex + 1) + "/" + (mainWindow.CurrentFolder.SubFiles.Count) + ") ";
        titleText += fileSystem.Name;
        
        mainWindow.AppTitleTextBlock1.Text = titleText;
    }
    
    private async void SetInfoText(FileSystemNode fileSystem)
    {
        var metadataText = "";

        // File size
        var file = await fileSystem.File.GetBasicPropertiesAsync();
        var size = file.Size;
        var sizeString = FileSizeHelper.GetReadableFileSize(size);
        metadataText += sizeString;
        
        // Type specific metadata
        switch (FileTypeHelper.GetFileCategory(fileSystem.Name))
        {
            case FileTypeHelper.FileCategory.IMAGE:
                var resolution = await GetImageMetadata(fileSystem.Path);
                metadataText += " - [" + resolution + "]";
                break;
        }

        // File location
        var path = fileSystem.Path;
        var name = fileSystem.Name;
        var pathWithoutName = path.Substring(0, path.Length - name.Length);
        metadataText += " - " + pathWithoutName;
        
        mainWindow.InfoTextBlock1.Text = metadataText;
        ColorMetadataText(fileSystem);
    }

    private async Task<string> GetImageMetadata(string filePath)
    {
        // Get resolution from stream loaded from filePath
        var loadedFile = await StorageFile.GetFileFromPathAsync(filePath);
        using var stream = await loadedFile.OpenAsync(FileAccessMode.Read);
        
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var resolution = decoder.PixelWidth + "×" + decoder.PixelHeight;
        stream.Dispose();
        return resolution;
    }
    
    // Colors the path before the root folder in the metadata text block
    private void ColorMetadataText(FileSystemNode node)
    {
        var topNode = node;
        while (topNode.Parent != null)
        {
            topNode = topNode.Parent;
        }

        var path = topNode.Path;
        var name = topNode.Name;
        var pathWithoutName = path.Substring(0, path.Length - name.Length);
        var pathWithoutLastBackslash = pathWithoutName.TrimEnd('\\');

        // Do not color parent folder name
        if (topNode.IsFile)
        {
            var lastBackslashIndex = pathWithoutLastBackslash.LastIndexOf('\\');
            pathWithoutLastBackslash = pathWithoutLastBackslash.Substring(0, lastBackslashIndex + 1);
        }
        var text = mainWindow.InfoTextBlock1.Text;
        var index = text.IndexOf(pathWithoutLastBackslash, StringComparison.Ordinal);
        if (index == -1) return;
        var textBeforeRun = new Run {Text = text.Substring(0, index)};
        var textMiddleRun = new Run {Text = pathWithoutLastBackslash, Foreground = new SolidColorBrush(Colors.Gray)};
        var textAfterRun = new Run {Text = text.Substring(index + pathWithoutLastBackslash.Length)};
        mainWindow.InfoTextBlock1.Inlines.Clear();
        mainWindow.InfoTextBlock1.Inlines.Add(textBeforeRun);
        mainWindow.InfoTextBlock1.Inlines.Add(textMiddleRun);
        mainWindow.InfoTextBlock1.Inlines.Add(textAfterRun);
    }

    public void ClearMetadata()
    {
        mainWindow.AppTitleTextBlock1.Text = "MediFiler";
        mainWindow.InfoTextBlock1.Text = "Nothing loaded!";
    }
}