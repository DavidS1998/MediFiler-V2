using System.Collections.Generic;
using System.Diagnostics;

namespace MediFiler_V2.Code;

public class UndoHandler
{
    private MainWindowModel _mainWindowModel;
    private MainWindow _mainWindow;
    private Stack<NodeMemento> _undoQueue = new();

    public UndoHandler(MainWindowModel mainWindowModel, MainWindow mainWindow)
    {
        _mainWindowModel = mainWindowModel;
        _mainWindow = mainWindow;
    }

    public void Push(NodeMemento memento)
    {
        if (_undoQueue.Count <= 0) _mainWindow.UndoButton1.IsEnabled = true;
        _undoQueue.Push(memento);
        Debug.WriteLine("Pushing " + memento.Action + " of " + memento.Name);
    }

    public void Pop()
    {
        _undoQueue.Pop();
    }

    public void ClearUndoQueue()
    {
        _undoQueue.Clear();
        _mainWindow.UndoButton1.IsEnabled = false;
    }

    /// Undo the last operation
    public void Undo()
    {
        if (_undoQueue.Count <= 0)
        {
            Debug.WriteLine("UNDO STACK EMPTY");
            return;
        };
        
        var memento = _undoQueue.Pop();
        if (_undoQueue.Count <= 0) _mainWindow.UndoButton1.IsEnabled = false;
        
        Debug.WriteLine("Undoing " + memento.Action + " of " + memento.Node.Name);
        switch (memento.Action)
        {
            case UndoAction.Rename:
                memento.Node.Rename(memento.Name);
                break;
            case UndoAction.Move:
                memento.Node.Move(memento.Parent, _mainWindowModel.CurrentFolderIndex);
                _mainWindowModel.CurrentFolder.FolderColor = true;
                memento.Parent.FolderColor = true; // TODO: Bake into Move method
                break;
        }

        _mainWindowModel.Refresh(moved: true);
    }
}