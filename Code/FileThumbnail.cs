using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public static class FileThumbnail
{
    // TODO: Use helper functions for the dictionary
    public static Dictionary<int, BitmapImage> ThumbnailCache = new();
    private const int PreloadDistance = 10;
    
    // Saves thumbnail to the dictionary with the index as key
    public static async Task SaveThumbnailToCache(string path, int index)
    {
        if (ThumbnailCache.ContainsKey(index)) return;
        ThumbnailCache.Add(index, null); // Lock the index

        var file = await StorageFile.GetFileFromPathAsync(path);
        StorageItemThumbnail thumbnail;
        
        try
        {
            thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);
            if (thumbnail == null)
            {
                ThumbnailCache.Remove(index);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed getting thumbnail for " + path + ": " + e);
            ThumbnailCache.Remove(index);
            return;
        }

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(thumbnail);
        ThumbnailCache[index] = bitmap;
    }
    
    // Caches several adjacent file thumbnails
    public static async void PreloadThumbnails(int currentPositionInFolder, FileSystemNode currentFolder)
    {
        var tasks = new List<Task>();
        for (var fileIndex = currentPositionInFolder - PreloadDistance; 
             fileIndex < (currentPositionInFolder + PreloadDistance); 
             fileIndex++)
        {
            if (fileIndex >= currentFolder.SubFiles.Count || fileIndex < 0) continue;
                
            var path = currentFolder.SubFiles[fileIndex].Path;
            tasks.Add(SaveThumbnailToCache(path, fileIndex));
        }
        await Task.WhenAll(tasks);
    }
}