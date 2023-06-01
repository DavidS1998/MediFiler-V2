using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2.Code;

public class FolderOperations
{
    private MainWindowModel _mainWindowModel;
    private MainWindow _mainWindow;

    public FolderOperations(MainWindowModel mainWindowModel, MainWindow mainWindow)
    {
        _mainWindowModel = mainWindowModel;
        _mainWindow = mainWindow;
    }

    public async void CreateFolderDialog(FileSystemNode node)
    {
        // Create a ContentDialog box with a text input field
        var dialog = new ContentDialog
        {
            Title = "Create new folder",
            Content = new TextBox
            {
                AcceptsReturn = false
            },
            PrimaryButtonText = "Create",
            SecondaryButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        var folderName = ((TextBox)dialog.Content).Text;
        
        await Task.Run((Action)(() => CreateFolder(folderName, node)));
    }

    private async void CreateFolder(string folderName, FileSystemNode node)
    {
        // Only allow valid File names
        var invalidCharsRegex = new Regex("[\\\\/:*?\"<>|]");
        if (string.IsNullOrWhiteSpace(folderName) || invalidCharsRegex.IsMatch(folderName))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Invalid Folder Name",
                Content = "Folder names cannot contain following characters: \\ / : * ? \" < > |",
                PrimaryButtonText = "OK",
                XamlRoot = _mainWindow.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };
            await errorDialog.ShowAsync();
            return;
        }

        try
        {
            var newFolder = await node.Folder.CreateFolderAsync(folderName);
            node.SubFolders.Insert(0, new FileSystemNode(newFolder, _mainWindowModel.CurrentFolder.Depth + 1, node));
            TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1, _mainWindow.dispatcherQueue);
            await Task.Delay(1);
            Debug.WriteLine("Finished");
        }
        catch (Exception e)
        {
            Debug.WriteLine("Error creating folder: " + e);
        }
    }

    public async void DeleteFolderDialog(FileSystemNode node)
    {
        // Create a ContentDialog box with a text input field
        var dialog = new ContentDialog
        {
            Title = "Delete Folder",
            Content = "Are you sure you want to delete this folder to the recycling bin?",
            PrimaryButtonText = "Delete",
            SecondaryButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;
        
        // Run DeleteFolder() on a separate thread using Task
        await Task.Run((Action)(() => DeleteFolder(node)));
    }

    private async void DeleteFolder(FileSystemNode node)
    {
        try
        {
            node.Parent.SubFolders.Remove(node);
            TreeHandler.FullFolderList.Remove(node);
            TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1, _mainWindow.dispatcherQueue);
            
            #pragma warning disable 4014
            node.Folder.DeleteAsync(StorageDeleteOption.Default);
            await Task.Delay(1);
            Debug.WriteLine("Finished");
        }
        catch (Exception e)
        {
            Debug.WriteLine("Error deleting folder: " + e.Message);
            _mainWindowModel.SwitchFolder(TreeHandler.RootNodes[0]);
        }
    }

    public async void RenameFolderDialog(FileSystemNode node)
    {
        // Create a ContentDialog box with a text input field
        var dialog = new ContentDialog
        {
            Title = "Rename Folder",
            Content = new TextBox
            {
                SelectedText = node.Folder.Name,
                AcceptsReturn = false
            },
            PrimaryButtonText = "Rename",
            SecondaryButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        var newName = ((TextBox)dialog.Content).Text;
        if (newName == node.Folder.Name) return;

        await Task.Run((Action)(() => RenameFolder(newName, node)));
    }

    private async void RenameFolder(string newName, FileSystemNode node)
    {
        // Only allow valid File names
        var invalidCharsRegex = new Regex("[\\\\/:*?\"<>|]");
        if (string.IsNullOrWhiteSpace(newName) || invalidCharsRegex.IsMatch(newName))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Invalid Folder Name",
                Content = "Folder names cannot contain following characters: \\ / : * ? \" < > |",
                PrimaryButtonText = "OK",
                XamlRoot = _mainWindow.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };
            await errorDialog.ShowAsync();
            return;
        }
        
        // Rename the folder
        try
        {
            node.Name = newName;
            node.Path = node.Parent.Path + "\\" + newName;
            TreeHandler.AssignTreeToUserInterface(_mainWindow.FileTreeView1, _mainWindow.dispatcherQueue);
            node.Folder.RenameAsync(newName);
            await Task.Delay(1);
            
            Debug.WriteLine("Finished");
            //FullRefresh();
        }
        catch (Exception e)
        {
            Debug.WriteLine("Folder already exists: " + e);
        }
    }
}