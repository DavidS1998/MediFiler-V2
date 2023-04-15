namespace MediFiler_V2.Code;

public enum UndoAction
{
    Move,
    Rename,
    Delete
}

public class NodeMemento
{
    public string Name { get; }
    public string Path { get; }
    public FileSystemNode Parent { get; }
    public FileSystemNode Node { get; }
    public UndoAction Action { get; }

    public NodeMemento(UndoAction action, string name, string path, FileSystemNode parent, FileSystemNode node)
    {
        Action = action;
        Name = name;
        Path = path;
        Parent = parent;
        Node = node;
    }
}