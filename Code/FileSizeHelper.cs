using System;

namespace MediFiler_V2.Code;

public class FileSizeHelper
{
    // FileSizeHelper
    public static string GetReadableFileSize(ulong size)
    {
        // Readable file size converter keeping 2 decimals
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = size;
        int order = 0;
        // Would use 1024, but Windows is lying?
        while (len >= 1000 && order < sizes.Length - 1)
        {
            order++;
            len /= 1000;
        }
        
        return $"{len:n1} {sizes[order]}".Replace(",", ".");
    }
}