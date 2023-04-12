using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2;

public static class TreeHandler
{
    private static readonly List<FileSystemNode> RootNodes = new(); // Top folders
    private static readonly List<FileSystemNode> FullFolderList = new(); // Used for the folder list view

    public static async Task BuildTree(IReadOnlyList<IStorageItem> filesAndFolders, TreeView fileTreeView)
    {
        ClearTree(fileTreeView);
        
        // Performance measuring
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Create the file structure in the background, may be very resource intensive
        await Task.Run(() =>
        {
            // Extract all root nodes
            Parallel.ForEach(filesAndFolders, path => RootNodes.Add(new FileSystemNode(path, 0)));

            // Go down each root node and build a tree
            foreach (var node in RootNodes)
            {
                AddFolderToTree(node);
            }
        });
        CreateTree(fileTreeView);
        
        // Print performance data
        stopwatch.Stop();
        Debug.WriteLine("Tree generated in " + stopwatch.ElapsedMilliseconds + "ms");
    }
    
    // Recursively extracts folder data from nodes
    public static void AddFolderToTree(FileSystemNode systemNode)
    {
        FullFolderList.Add(systemNode);
        foreach (var subNode in systemNode.SubFolders)
        {
            AddFolderToTree(subNode);
        }
    }


    // // // Helper functions // // //
    
    
    public static void ClearTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = null;
        RootNodes.Clear();
        FullFolderList.Clear();
    }

    public static void CreateTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = FullFolderList;
    }
    
    public static FileSystemNode LoadRootNode(int index)
    {
        return RootNodes[index];
    }
}