using System;
using System.Collections.Generic;

namespace NameShift.Ui.Actions;

/// <summary>The folders whose entries a Rename or Revert may have changed: the tree nodes to refresh.</summary>
public sealed class FolderContentsChangedEventArgs : EventArgs
{
    public FolderContentsChangedEventArgs(IReadOnlyList<string> folders)
    {
        Folders = folders ?? throw new ArgumentNullException(nameof(folders));
    }

    public IReadOnlyList<string> Folders { get; }
}