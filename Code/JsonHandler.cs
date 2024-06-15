using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MediFiler_V2.Code;

public sealed class JsonHandler
{
    private MainWindow _mainWindow;
    
    public JsonHandler(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }
    
    public class SettingsRoot
    {
        public Dictionary<string, QuickAccessFolder> QuickFolders { get; set; }
        public Dictionary<string, QuickAccessFolder> FavoriteFolders { get; set; }
        public int FolderSize { get; set; }
        public bool SortPanelPinned { get; set; }
    }
    public Dictionary<string, QuickAccessFolder> QuickFolders = new();
    public Dictionary<string, QuickAccessFolder> FavoriteFolders = new();

    public void UpdateHomeFolders()
    {
        _mainWindow.RecentFoldersView1.ItemsSource = null;
        _mainWindow.MostOpenedFoldersView1.ItemsSource = null;

        _mainWindow.RecentFoldersView1.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.LastOpened);
        _mainWindow.MostOpenedFoldersView1.ItemsSource = QuickFolders.Values.OrderByDescending(x => x.TimesOpened);
        _mainWindow.FavoriteFoldersView1.ItemsSource = FavoriteFolders.Values.OrderBy(x => x.Name);

        _mainWindow.FavoriteView1.ItemsSource = FavoriteFolders.Values.OrderBy(x => x.Name);
    }

    public void ReadJsonFile()
    {
        // Deserialize JSON to QuickFolders list
        var json = File.ReadAllText("appsettings.json");
        var rootObject = JsonSerializer.Deserialize<SettingsRoot>(json);
        if (rootObject != null) QuickFolders = rootObject.QuickFolders;
        if (rootObject != null) FavoriteFolders = rootObject.FavoriteFolders;
        if (rootObject != null) _mainWindow._folderViewSize = rootObject.FolderSize;
        if (rootObject != null) _mainWindow.SortPanelPinned = rootObject.SortPanelPinned;
        _mainWindow.TogglePin();
    }
    
    public void AddQuickFolder(FileSystemNode node)
    {
        if (node.IsFile) return;
            
        // Opened before
        if (QuickFolders.ContainsKey(node.Path))
        {
            QuickFolders[node.Path].LastOpened = DateTime.Now;
            QuickFolders[node.Path].TimesOpened++;
            UpdateJsonFile();
            UpdateHomeFolders();
            return;
        }
            
        // New QuickFolder
        var quickFolder = new QuickAccessFolder(node.Name)
        {
            Path = node.Path,
            LastOpened = DateTime.Now,
            TimesOpened = 1
        };

        QuickFolders.Add(node.Path, quickFolder);
        UpdateJsonFile();
        UpdateHomeFolders();
    }

    public void RemoveQuickFolder(string path)
    {
        QuickFolders.Remove(path);
        UpdateJsonFile();
        UpdateHomeFolders();
    }

    public void UpdateJsonFile()
    {
        // Create a dictionary with a single key-value pair for QuickFolders
        var quickFolders = new Dictionary<string, QuickAccessFolder>();
        foreach (var folder in QuickFolders)
        {
            quickFolders.Add(folder.Key, folder.Value);
        }

        var dictionary = new Dictionary<string, object>
        {
            { "QuickFolders", quickFolders },
            { "FavoriteFolders", FavoriteFolders },
            { "FolderSize", _mainWindow._folderViewSize },
            { "SortPanelPinned", _mainWindow.SortPanelPinned }
        };

        // Serialize the dictionary to JSON
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(dictionary, options);

        // Serialize and write the prettified JSON to appsettings.json
        File.WriteAllText("appsettings.json", json);
    }
}