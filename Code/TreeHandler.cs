using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2;

public static class TreeHandler
{
    // TODO: Use helper functions instead of global variables
    public static readonly List<FileSystemNode> RootNodes = new(); // Top folders
    public static readonly List<FileSystemNode> FullFolderList = new(); // Used for the folder list view
    private static IReadOnlyList<IStorageItem> rootFiles;

    public static async Task BuildTree(IReadOnlyList<IStorageItem> filesAndFolders, TreeView fileTreeView)
    {
        rootFiles = filesAndFolders;
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
        AssignTreeToUserInterface(fileTreeView);
        
        // Print performance data
        stopwatch.Stop();
        Debug.WriteLine("Tree generated in " + stopwatch.ElapsedMilliseconds + "ms");
    }
    
    /// Recursively extracts folder data from nodes
    public static void AddFolderToTree(FileSystemNode systemNode)
    {
        FullFolderList.Add(systemNode);
        foreach (var subNode in systemNode.SubFolders)
        {
            AddFolderToTree(subNode);
        }
    }

    /// Rebuilds the tree from the root files
    public static async void RebuildTree(TreeView fileTreeView)
    {
        FullFolderList.Clear();
        await BuildTree(rootFiles, fileTreeView);
    }


    // // // Helper functions // // //
    
    
    private static void ClearTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = null;
        RootNodes.Clear();
        FullFolderList.Clear();
    }

    public static void AssignTreeToUserInterface(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = null;
        fileTreeView.ItemsSource = RootNodes;
        //fileTreeView.ItemsSource = FullFolderList;
    }
    
    public static FileSystemNode LoadRootNode(int index)
    {
        return RootNodes[index];
    }

    public static FileSystemNode FindNode(string Path)
    {
        foreach (var node in FullFolderList)
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