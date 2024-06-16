using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediFiler_V2.Code;

public class FolderViewList
{
    public ObservableCollection<FolderItem> FolderItems => _folderItems;
    readonly ObservableCollection<FolderItem> _folderItems = new();
    
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
        foreach (var item in thumbnailCache)
        {
            var name = item.Key;
            var color = GetColorForName(name);
            _folderItems.Add(new FolderItem(name, item.Value, color));
        }
        //Debug.WriteLine("Updated folder view");
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
        return new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    }
}

public class FolderItem : INotifyPropertyChanged
{
    public string Name { get; set; }
    public BitmapImage Path { get; set; }
    
    private SolidColorBrush backgroundColor = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
    public SolidColorBrush BackgroundColor {
        get => backgroundColor; 
        set { backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }}
    
    private double sizeHeight;
    public double SizeHeight { 
        get => sizeHeight; 
        set { sizeHeight = value; OnPropertyChanged(nameof(SizeHeight)); }}
    
    private double sizeWidth;
    public double SizeWidth {
        get => sizeWidth; 
        set { sizeWidth = value; OnPropertyChanged(nameof(SizeWidth)); } }
        
    public FolderItem(string name, BitmapImage path, SolidColorBrush bgColor = null)
    {
        Name = name;
        Path = path;
        if (bgColor != null) BackgroundColor = bgColor;
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}