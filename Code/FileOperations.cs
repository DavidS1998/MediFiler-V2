using System;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MediFiler_V2.Code;

public class FileOperations
{
    private MainWindowModel _mainWindowModel;
    //private XamlRoot _xamlRoot;
    private MainWindow _mainWindow;
    

    public FileOperations(MainWindowModel mainWindowModel, MainWindow mainWindow)
    {
        _mainWindowModel = mainWindowModel;
        _mainWindow = mainWindow;
    }

    public void MoveFile(FileSystemNode destination)
    {
        // Error check
        if (_mainWindowModel.CurrentFolder == null || _mainWindowModel.CurrentFolder.SubFiles.Count <= 0 || destination.Path == _mainWindowModel.CurrentFolder.Path) return;

        try
        {
            // Undo queue
            _mainWindowModel.UndoHandler.Push(_mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].CreateMemento(UndoAction.Move));
            _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Move(destination, 0);
            _mainWindowModel.Refresh(moved: true);
        }
        catch (Exception e)
        {
            _mainWindowModel.UndoHandler.Pop();
            _mainWindowModel.Refresh();
            Console.WriteLine(e);
        }
        
    }

    /// Deletes by moving to a trash folder, and recycling it only when switching folders
    public async void DeleteFile()
    {
        // Error check
        if (_mainWindowModel.CurrentFolder == null || _mainWindowModel.CurrentFolder.SubFiles.Count <= 0) return;

        IStorageFolder baseDirectory = await StorageFolder.GetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory);
        var trashFolder = await baseDirectory.CreateFolderAsync("Trash", CreationCollisionOption.OpenIfExists);
        var trashFolderNode = new FileSystemNode(trashFolder, 0, null);
        
        // Undo queue
        _mainWindowModel.UndoHandler.Push(_mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].CreateMemento(UndoAction.Move));

        _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Move(trashFolderNode, 0);
        _mainWindowModel.Refresh();
    }

    /// Renames the currently selected file
    public async void RenameDialog()
    {
        // Error check
        if (_mainWindowModel.CurrentFolder == null || _mainWindowModel.CurrentFolder.SubFiles.Count <= 0) return;
        
        // Get the file extension as a string
        var fileExtension = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Split('.').Last();
        
        // Create a ContentDialog box with a text input field
        var dialog = new ContentDialog
        {
            Title = "Rename File",
            Content = new TextBox
            {
                // Text is the current file name without the extension
                SelectedText = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Replace("." + fileExtension, ""),
                AcceptsReturn = false,
            },
            PrimaryButtonText = "Rename",
            SecondaryButtonText = "Cancel",
            XamlRoot = _mainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        var newName = ((TextBox)dialog.Content).Text;
        
        RenameFile(newName);
    }

    public async void RenameFile(string newName)
    {
        // Get the file extension as a string
        var fileExtension = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Split('.').Last();
        
        // Only allow valid File names
        var invalidCharsRegex = new Regex("[\\\\/:*?\"<>|]");
        if (string.IsNullOrWhiteSpace(newName) || invalidCharsRegex.IsMatch(newName))
        {
            var errorDialog = new ContentDialog
            {
                Title = "Invalid File Name",
                Content = "File names cannot contain following characters: \\ / : * ? \" < > |",
                PrimaryButtonText = "OK",
                XamlRoot = _mainWindow.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };
            await errorDialog.ShowAsync();
            return;
        }
        
        // Undo queue
        _mainWindowModel.UndoHandler.Push(_mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].CreateMemento(UndoAction.Rename));

        _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Rename(newName + "." + fileExtension);
        _mainWindowModel.Refresh(true, moved: true);
    }

    
    
    public void AddPlus()
    {
        // Error check
        if (_mainWindowModel.CurrentFolder == null || _mainWindowModel.CurrentFolder.SubFiles.Count <= 0) return;
        
        var fileExtension = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Split('.').Last();
        var currentName = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Replace("." + fileExtension, "");
        // Add a + to the start of the name
        var newName = "+" + currentName;
        RenameFile(newName);
        
        // Update folder color if criteria is met
        if (!PlusExists()) _mainWindowModel.CurrentFolder.FolderColor = true;
    }

    public void RemovePlus()
    {
        // Error check
        if (_mainWindowModel.CurrentFolder == null || _mainWindowModel.CurrentFolder.SubFiles.Count <= 0) return;

        var fileExtension = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Split('.').Last();
        var currentName = _mainWindowModel.CurrentFolder.SubFiles[_mainWindowModel.CurrentFolderIndex].Name.Replace("." + fileExtension, "");
        // Check if the name starts with a +
        if (!currentName.StartsWith("+")) return;

        // Remove one + from the start of the name
        var newName = currentName.Remove(0, 1);
        RenameFile(newName);
        
        // Update folder color if criteria is no longer met
        if (PlusExists()) _mainWindowModel.CurrentFolder.FolderColor = true;
    }

    private bool PlusExists()
    {
        // If _mainWindowModel.CurrentFolder.SubFiles contains even one file not starting with a +, return false
        return _mainWindowModel.CurrentFolder.SubFiles.Any(file => !file.Name.StartsWith("+"));
    }
}