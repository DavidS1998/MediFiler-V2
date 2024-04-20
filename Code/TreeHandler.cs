using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using MediFiler_V2.Code;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2;

public static class TreeHandler
{
    // TODO: Use helper functions instead of global variables
    public static List<FileSystemNode> RootNodes = new(); // Top folders
    private static List<FileSystemNode> _fullFolderList = new(); // Used for the folder list view
    private static IReadOnlyList<IStorageItem> _rootFiles;
    private static object fullFolderListLock = new object(); // Lock object for synchronization


    public static async Task BuildTree(IReadOnlyList<IStorageItem> filesAndFolders, TreeView fileTreeView)
    {
        _rootFiles = filesAndFolders;
        ClearTree(fileTreeView);
        fileTreeView.ItemsSource = null;
        
        // Performance measuring
        //var stopwatch = new Stopwatch();
        //stopwatch.Start();

        // Create the file structure in the background, may be very resource intensive
        // Extract all root nodes
        // Create the file structure in the background, may be very resource intensive
        await Task.Run(() =>
        {
            // Extract all root nodes
            foreach (var path in filesAndFolders)
            {
                var node = new FileSystemNode(path, 0, null);
                RootNodes.Add(node);
                AddFolderToTree(node);
            }
        });

        fileTreeView.ItemsSource = RootNodes;
        
        // Print performance data
        //stopwatch.Stop();
        //Debug.WriteLine("Tree generated in " + stopwatch.ElapsedMilliseconds + "ms");
    }
    
    /// Recursively extracts folder data from nodes
    public static void AddFolderToTree(FileSystemNode systemNode)
    {
        lock (fullFolderListLock)
        {
            _fullFolderList.Add(systemNode);
        }
    
        foreach (var subNode in systemNode.SubFolders)
        {
            AddFolderToTree(subNode);
        }
        //Debug.WriteLine("Added folder: " + systemNode.Name);
    }

    /// Rebuilds the tree from the root files
    public static async void RebuildTree(TreeView fileTreeView, MainWindow mainWindow = null)
    {
        //FullFolderList.Clear();
        await BuildTree(_rootFiles, fileTreeView);
        
        if (mainWindow != null) mainWindow.StartLoadFilesInBackground();
    }
    

    
    // // // Helper functions // // //
    
    
    
    // Get copy of the full folder list
    public static List<FileSystemNode> GetFullFolderList()
    {
        lock (fullFolderListLock)
        {
            return _fullFolderList;
        }
    }
    
    public static void RemoveNode(FileSystemNode node)
    {
        lock (fullFolderListLock)
        {
            _fullFolderList.Remove(node);
        }
    }
    
    private static void ClearTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = null;
        RootNodes.Clear();
        lock (fullFolderListLock)
        {
            _fullFolderList.Clear();
        }
    }

    public static void AssignTreeToUserInterface(TreeView fileTreeView, DispatcherQueue queue)
    {
        // Invoke fileTreeView update on the UI thread
        queue.TryEnqueue(() =>
        {
            fileTreeView.ItemsSource = null;
            fileTreeView.ItemsSource = RootNodes;
        });
    }
    
    public static FileSystemNode LoadRootNode(int index)
    {
        return RootNodes[index];
    }

    public static FileSystemNode FindNode(string Path)
    {
        foreach (var node in _fullFolderList)
        {
            if (node.Path == Path) return node;
        }
        return RootNodes[0];
    }

    public static FileSystemNode GetCurrentFolder()
    {
        // TODO: Implement - To be used instead of the global variable
        return null;
    }
}