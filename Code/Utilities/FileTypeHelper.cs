using Path = System.IO.Path;

namespace MediFiler_V2
{
    public class FileTypeHelper
    {
        public enum FileCategory
        {
            IMAGE,
            VIDEO,
            AUDIO,
            TEXT,
            OTHER
        }

        public static FileCategory GetFileCategory(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".jpg_large" or ".webp" or ".ico" or ".jfif" or ".gif" or ".png" or ".bmp" or ".svg"
                    => FileCategory.IMAGE,
                ".mp4" or ".webm" or ".wmw" or ".flv" or ".avi" or ".mov" or ".mkv" or ".m4v" or ".mpg" or ".mpeg" or ".m2v" 
                    or ".3gp" or ".3g2" or ".mxf" or ".roq" or ".nsv" or ".flv" or ".f4v" or ".f4p" or ".f4a" or ".f4b"
                    => FileCategory.VIDEO,
                ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" or ".wma" or ".aac" or ".alac" or ".aiff" or ".dsd" or ".opus"
                    => FileCategory.AUDIO,
                ".txt" or ".md"
                    => FileCategory.TEXT,
                _ 
                    => FileCategory.OTHER,
            };
        }
    }
}
