﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code.Utilities;

public static class FolderIconGetter
{
    public static Dictionary<string, StorageItemThumbnail> IconCache = new();

    // Saves thumbnail to the dictionary with the path as key
    public static async void GetFolderIcon(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        IconCache.Clear();
        var nodes = TreeHandler.GetFullFolderList();
        if (nodes.Count <= 0) return;

        try
        {
            foreach (var node in nodes)
            {
                StorageItemThumbnail thumbnail;
                var file = await StorageFolder.GetFolderFromPathAsync(node.Path);
                thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);
                IconCache.TryAdd(node.Path, thumbnail);
                // Call UI thread to update the icon
                queue.TryEnqueue(() => { node.SetFolderIcon(); });
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed getting folder icon: " + e);
        }
    }
}

