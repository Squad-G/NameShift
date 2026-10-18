using System;

namespace NameShift.Ui;

/// <summary>The folder the window now shows.</summary>
public sealed class FolderChangedEventArgs : EventArgs
{
    public FolderChangedEventArgs(string path)
    {
        Path = path;
    }

    public string Path { get; }
}
