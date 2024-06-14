using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public class FolderViewList
{
    public ObservableCollection<FolderItem> FolderItems => folderItems;
    ObservableCollection<FolderItem> folderItems = new();
        
    public FolderViewList()
    {
        //Debug.WriteLine("FolderViewList created");
    }
    
    public void ClearFolderItems()
    {
        folderItems.Clear();
    }
    
    public int GetCount()
    {
        return folderItems.Count;
    }
    
    public void ReplaceFolderItems(Dictionary<string, BitmapImage> thumbnailCache)
    {
        folderItems.Clear();
        foreach (var item in thumbnailCache)
        {
            folderItems.Add(new FolderItem(item.Key, item.Value));
        }
        Debug.WriteLine("Updated folder view");
    }
}

public class FolderItem
{
    public string Name { get; set; }
    public BitmapImage Path { get; set; }
        
    public FolderItem(string name, BitmapImage path)
    {
        Name = name;
        Path = path;
    }
}