using Path = System.IO.Path;

namespace MediFiler_V2
{
    public class FileTypeHelper
    {
        public enum FileCategory
        {
            IMAGE,
            VIDEO,
            TEXT,
            OTHER
        }

        public static FileCategory GetFileCategory(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".jpg_large" or ".webp" or ".ico" or ".jfif" or ".gif" or ".png" 
                    => FileCategory.IMAGE,
                ".mp4" or ".webm" or ".wmw" or ".flv" or ".avi" or ".mov" or ".mkv" 
                    => FileCategory.VIDEO,
                ".txt"
                    => FileCategory.TEXT,
                _ 
                    => FileCategory.OTHER,
            };
        }
    }
}
