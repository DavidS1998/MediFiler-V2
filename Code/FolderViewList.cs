using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
    
    public void UpdateSizes(double size)
    {
        foreach (var item in folderItems)
        {
            item.Size = size;
        }
    }
}

public class FolderItem : INotifyPropertyChanged
{
    public string Name { get; set; }
    public BitmapImage Path { get; set; }
    private double size;
    public double Size
    {
        get => size;
        set { size = value; OnPropertyChanged(nameof(Size)); }
    }
        
    public FolderItem(string name, BitmapImage path)
    {
        Name = name;
        Path = path;
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}