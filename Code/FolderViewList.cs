using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public class FolderViewList
{
    public ObservableCollection<FolderItem> FolderItems => _folderItems;
    readonly ObservableCollection<FolderItem> _folderItems = new();
    private SolidColorBrush _defaultColor = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    
    private readonly Dictionary<string, SolidColorBrush> _prefixColorMapping = new()
    {
        { "++++++", new SolidColorBrush(Color.FromArgb(200, 255, 69, 0)) },
        { "+++++", new SolidColorBrush(Color.FromArgb(200, 159, 49, 222)) },
        { "++++", new SolidColorBrush(Color.FromArgb(200, 230, 30, 88)) },
        { "+++", new SolidColorBrush(Color.FromArgb(200, 19, 150, 226)) },
        { "++", new SolidColorBrush(Color.FromArgb(200, 150, 150, 1)) },
        { "+", new SolidColorBrush(Color.FromArgb(200, 50, 150, 50)) }
    };
    
    public void ClearFolderItems()
    {
        _folderItems.Clear();
    }
    
    public void ReplaceFolderItems(Dictionary<string, BitmapImage> thumbnailCache)
    {
        _folderItems.Clear();
        //SolidColorBrush lastColor = GetColorForName(thumbnailCache.Keys.First());
        // ^ buggy
        
        foreach (var item in thumbnailCache)
        {
            var name = item.Key;
            var color = GetColorForName(name);
            
            /*
            // Add breaks in between each color change
            if (color != lastColor)
            {
                _folderItems.Add(new FolderItem("Break", null, null, true));
            }
            lastColor = color;
            */
            
            _folderItems.Add(new FolderItem(name, item.Value, color));
        }
    }
    
    public void UpdateSizes(double sizeHeight, double sizeWidth)
    {
        foreach (var item in _folderItems)
        {
            item.SizeHeight = sizeHeight;
            item.SizeWidth = sizeWidth;
        }
    }
    
    private SolidColorBrush GetColorForName(string name)
    {
        foreach (var prefix in _prefixColorMapping.Keys.OrderByDescending(k => k.Length))
        {
            if (name.StartsWith(prefix))
            {
                return _prefixColorMapping[prefix];
            }
        }
        // Default color if no prefix matches
        return _defaultColor;
    }
}

public class FolderItem : INotifyPropertyChanged
{
    public string Name { get; set; }
    public BitmapImage Path { get; set; }
    
    private SolidColorBrush _backgroundColor = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
    public SolidColorBrush BackgroundColor {
        get => _backgroundColor; 
        set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }}
    
    private double _sizeHeight;
    public double SizeHeight { 
        get => _sizeHeight;
        set { _sizeHeight = value; OnPropertyChanged(nameof(SizeHeight)); }}
    
    private double _sizeWidth;
    public double SizeWidth
    {
        get => _sizeWidth;
        set { _sizeWidth = value; OnPropertyChanged(nameof(SizeWidth)); }}
    
    public bool IsBreak { get; set; }
        
    public FolderItem(string name, BitmapImage path, SolidColorBrush bgColor = null, bool isBreak = false)
    {
        // Random value between 100 and 200
        Name = name;
        Path = path;
        IsBreak = isBreak;
        if (bgColor != null) BackgroundColor = bgColor;
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class FolderItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate NormalTemplate { get; set; }
    public DataTemplate BreakTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var folderItem = item as FolderItem;
        return folderItem != null && folderItem.IsBreak ? BreakTemplate : NormalTemplate;
    }
}
