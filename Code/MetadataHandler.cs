﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace MediFiler_V2.Code;

public class MetadataHandler
{
    MainWindow mainWindow;
    MainWindowModel mainWindowModel;
    private Grid appbar;
    public int vertical;
    public int horizontal;

    public MetadataHandler(MainWindow mainWindow, MainWindowModel mainWindowModel, Grid appbar)
    {
        this.mainWindow = mainWindow;
        this.mainWindowModel = mainWindowModel;
        this.appbar = appbar;
    }
    // TODO: Give a reference to this class from FileSystemNode, and do file system operations there
    
    /// Gets secondary data from the current file
    public async void ShowMetadata(FileSystemNode fileSystem)
    {
        vertical = 0;
        SetTitleText(fileSystem);
        await SetInfoText(fileSystem);
        
        ResolutionWarning(fileSystem);
    }
    
    //
    public void ResolutionWarning(FileSystemNode fileSystem)
    {
        // TODO: Find a better place to do this
        if (FileTypeHelper.GetFileCategory(fileSystem.Path) != FileTypeHelper.FileCategory.IMAGE
            || vertical is not ((< 1000 or > 5000) and not 0) 
            || fileSystem.Name.EndsWith(".gif")
            || fileSystem.Name.EndsWith(".ico"))
            return;
        
        appbar.Background = new SolidColorBrush(Color.FromArgb(255, 50, 50, 50));
    }

    private void SetTitleText(FileSystemNode fileSystem)
    {
        // Current position and name
        var titleText = "";
        titleText += "(" + (mainWindowModel.CurrentFolderIndex + 1) + "/" + (mainWindowModel.CurrentFolder.SubFiles.Count) + ") ";
        titleText += fileSystem.Name;
        
        mainWindow.AppTitleTextBlock1.Text = titleText;
    }
    
    // TODO: Refactor, make coloring specific parts of info text easier
    // TODO: 0s in time metadata should be greyed out
    
    private async Task SetInfoText(FileSystemNode fileSystem)
    {
        var metadataText = "";
        BasicProperties file;

        // File size
        try
        {
            file = await fileSystem.File.GetBasicPropertiesAsync();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return;
        }
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
            case FileTypeHelper.FileCategory.VIDEO:
                var duration = await GetVideoMetadata(fileSystem.Path);
                metadataText += " - " + duration + "";
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
        try
        {
            // Get resolution from stream loaded from filePath
            var loadedFile = await StorageFile.GetFileFromPathAsync(filePath);
            using var stream = await loadedFile.OpenAsync(FileAccessMode.Read);
        
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var resolution = decoder.PixelWidth + "×" + decoder.PixelHeight;
            horizontal = (int) decoder.PixelWidth;
            vertical = (int) decoder.PixelHeight;
            stream.Dispose();
            return resolution;
        }
        catch (Exception e)
        {
            Console.WriteLine("Metadata error: " + e);
            return "INVALID";
        }
    }

    private async Task<string> GetVideoMetadata(string filePath)
    {
        try
        {
            // Get video length
            var loadedFile = await StorageFile.GetFileFromPathAsync(filePath);
            var decoder = await loadedFile.Properties.GetVideoPropertiesAsync();
            var duration = decoder.Duration;
            var resolution = decoder.Height;
            var resolutionString = resolution + "p";
            
            if (resolution == 0)
            {
                resolutionString = "UNKNOWN";
            }
            
            var durationString = duration.ToString(@"hh\:mm\:ss");
            // Remove zeros from durationString
            durationString = durationString.TrimStart('0');
            durationString = durationString.TrimStart(':');
            return "[" + resolutionString + "] - [" + durationString + "]";
        }
        catch (Exception e)
        {
            Console.WriteLine("Metadata error: " + e);
            return "INVALID";
        }
    }
    
    /// Colors the path before the root folder in the metadata text block
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