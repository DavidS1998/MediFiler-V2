using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2;

public static class TreeHandler
{
    // TODO: Make helper functions for these lists
    private static List<FileNode> _rootNodes = new(); // Top folders
    private static List<FileNode> _fullFolderList = new(); // Used for the folder list view
    
    // TODO: Show loading indicator
    public static async Task BuildTree(IReadOnlyList<IStorageItem> filesAndFolders, TreeView fileTreeView)
    {
        ClearTree(fileTreeView);

        // Create the file structure in the background, may be very resource intensive
        await Task.Run(() =>
        {
            // Extract all root nodes
            Parallel.ForEach(filesAndFolders, path => _rootNodes.Add(new FileNode(path, 0)));

            // Go down each root node and build a tree
            foreach (var node in _rootNodes)
            {
                AddFolderToTree(node);
            }
        });

        CreateTree(fileTreeView);
    }

    
    // Recursively extracts folder data from nodes
    public static void AddFolderToTree(FileNode node)
    {
        _fullFolderList.Add(node);
        foreach (var subNode in node.SubFolders)
        {
            AddFolderToTree(subNode);
        }
    }


    // // // Helper functions // // //
    
    
    public static void ClearTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = null;
        _rootNodes.Clear();
        _fullFolderList.Clear();
    }

    public static void CreateTree(TreeView fileTreeView)
    {
        fileTreeView.ItemsSource = _fullFolderList;
    }
    
    public static FileNode LoadRootNode(int index)
    {
        return _rootNodes[index];
    }
}