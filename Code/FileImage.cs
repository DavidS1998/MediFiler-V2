using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public static class FileImage
{
    
    // Load image from file
    public static async Task<BitmapImage> LoadImage(FileSystemNode fileSystem, int size)
    {
        var bitmap = new BitmapImage();
        bitmap.DecodePixelHeight = size;

        try
        {
            var loadedFile = await StorageFile.GetFileFromPathAsync(fileSystem.Path);
            using var stream = await loadedFile.OpenAsync(FileAccessMode.Read);
            await bitmap.SetSourceAsync(stream);
            stream.Dispose();
        }
        catch (Exception)
        {
            Debug.WriteLine("Read permission denied");
            return null;
        }
        
        return bitmap;
    }
}