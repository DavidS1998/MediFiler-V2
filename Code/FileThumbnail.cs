﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public class FileThumbnail
{
    // TODO: Use helper functions for the dictionary
    public Dictionary<int, BitmapImage> ThumbnailCache = new();

    MainWindowModel _model;
    private DispatcherQueue _dispatcherQueue;
    int PreloadDistance;
    
    public FileThumbnail(MainWindowModel mainWindow, int preloadDistance, DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _model = mainWindow;
        PreloadDistance = preloadDistance;
    }
    
    /// Saves thumbnail to the dictionary with the index as key
    public async Task SaveThumbnailToCache(string path, int index)
    {
        if (ThumbnailCache.ContainsKey(index)) return;
        StorageItemThumbnail thumbnail;
        
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);
            if (thumbnail == null)
            {
                if (ThumbnailCache.ContainsKey(index))
                    ThumbnailCache.Remove(index);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed getting thumbnail for " + path + ": " + e);
            if (ThumbnailCache.ContainsKey(index))
                ThumbnailCache.Remove(index);
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.SetSourceAsync(thumbnail);
            ThumbnailCache.TryAdd(index, bitmap);
        });
    }
    
    /// Caches several adjacent file thumbnails into the dictionary
    public async void PreloadThumbnails(int currentPositionInFolder, FileSystemNode currentFolder, StackPanel previewImageContainer)
    {
        // Immediate secondary caching; starts at user position, less waiting
        var tasks = new List<Task>();
        // For loop using PreloadDistance as length in both directions
        for (var i = -PreloadDistance; i < PreloadDistance; i++)
        {
            var fileIndex = currentPositionInFolder + i;
            if (fileIndex >= currentFolder.SubFiles.Count || fileIndex < 0) continue;
                
            var path = currentFolder.SubFiles[fileIndex].Path;
            tasks.Add(SaveThumbnailToCache(path, fileIndex));
        }
        await Task.WhenAll(tasks);
        
        
        // Ensure the following code runs on the UI thread
        _dispatcherQueue.TryEnqueue(() =>
        {
            FillPreviews(previewImageContainer);
            // Don't run if nothing new was cached
            if (ThumbnailCache.Count != currentFolder.SubFiles.Count)
            {
                _model.UpdateFolderView();
            }
        });
        
        // Rapid folder switching may cause thumbnails from the wrong folder to be loaded
        if (currentFolder == _model.CurrentFolder) return;
        Debug.WriteLine("LOADED WRONG FOLDER, RETRYING");
        ThumbnailCache.Clear();
        PreloadThumbnails(currentPositionInFolder, _model.CurrentFolder, previewImageContainer);
    }
    
    /// Cache all thumbnails in the current folder
    public async void CacheAllThumbnails()
    {
        // Check if all have been cached
        if (ThumbnailCache.Count == _model.CurrentFolder.SubFiles.Count) return;
        if (_isCaching) return;
        _isCaching = true;
        
        var tasks = new List<Task>();
        try
        {
            for (var i = 0; i < _model.CurrentFolder.SubFiles.Count; i++)
            {
                var path = _model.CurrentFolder.SubFiles[i].Path;
                tasks.Add(SaveThumbnailToCache(path, i));
                await Task.Delay(1);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e + " - Context likely changed");
            _isCaching = false;
            return;
        }
        await Task.WhenAll(tasks);
        Debug.WriteLine("Thumbnail caching complete");
        _isCaching = false;
        
        _dispatcherQueue.TryEnqueue(() =>
        {
            _model.UpdateFolderView();
        });
    }
    private bool _isCaching;
    
    
    // // // FOLDER VIEW // // //

    
    public Dictionary<int, BitmapImage> GetThumbnails()
    {
        return ThumbnailCache;
    }
    
    
    // // // PREVIEW BAR // // //
    
    
    /// Initialize image preview container
    public void CreatePreviews(int previewCount, StackPanel previewImageContainer)
    {
        // Use PreloadDistance to determine how many Borders to create
        for (var i = 0; i < previewCount; i++)
        {
            var container = new Border { Width = 150, IsTapEnabled = true, IsHitTestVisible = true };
            // Make the container clickable
            var image = new Image
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Stretch = Stretch.UniformToFill
            };
            container.Child = image;
            previewImageContainer.Children.Add(container);
        }
        // Give the middle image a colored border
        var middleBorder = (Border) previewImageContainer.Children[previewCount / 2];
        middleBorder.BorderBrush = new SolidColorBrush(Colors.Fuchsia);
        middleBorder.BorderThickness = new Thickness(3);
    }
        
    /// Fill preview images with thumbnails, putting the current image in the middle
    private void FillPreviews(StackPanel previewImageContainer)
    {
        // Check if ThumbnailCache at index exists
        if (!ThumbnailCache.ContainsKey(_model.CurrentFolderIndex)) return;
            
        var previewCount = previewImageContainer.Children.Count;
        var middleIndex = previewCount / 2;
        var middleBorder = (Border) previewImageContainer.Children[middleIndex];
        var middleImage = (Image) middleBorder.Child;
        middleImage.Source = ThumbnailCache[_model.CurrentFolderIndex];
            
        // Fill previews to the left
        for (var i = middleIndex - 1; i >= 0; i--)
        {
            var border = (Border) previewImageContainer.Children[i];
            var image = (Image) border.Child;
            var index = _model.CurrentFolderIndex - (middleIndex - i);
            if (index < 0) continue;

            if (!ThumbnailCache.ContainsKey(index)) continue;
            image.Source = ThumbnailCache[index];
        }
            
        // Fill previews to the right
        for (var i = middleIndex + 1; i < previewCount; i++)
        {
            var border = (Border) previewImageContainer.Children[i];
            var image = (Image) border.Child;
            var index = _model.CurrentFolderIndex + (i - middleIndex);
            if (index >= _model.CurrentFolder.SubFiles.Count) continue;
            
            if (!ThumbnailCache.ContainsKey(index)) continue;
            image.Source = ThumbnailCache[index];
        }
    }

    /// Clear all cached thumbnails
    public void ClearPreviewCache(StackPanel previewImageContainer)
    {
        var previewCount = previewImageContainer.Children.Count;
        for (var i = 0; i < previewCount; i++)
        {
            var image = (Image) ((Border) previewImageContainer.Children[i]).Child;
            image.Source = null;
        }
    }
}