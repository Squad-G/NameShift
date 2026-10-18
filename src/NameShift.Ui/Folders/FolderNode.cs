using System;
using System.Collections.Generic;

namespace NameShift.Ui.Folders;

/// <summary>A folder in the tree model. Children are listed the first time the node is expanded.</summary>
public sealed class FolderNode
{
    internal FolderNode(string name, string fullPath, FolderNode? parent, FolderRootKind? rootKind)
    {
        Name = name;
        FullPath = fullPath;
        Parent = parent;
        RootKind = rootKind;
    }

    public string Name { get; }

    public string FullPath { get; }

    /// <summary>Null for roots.</summary>
    public FolderNode? Parent { get; }

    /// <summary>Set on roots only.</summary>
    public FolderRootKind? RootKind { get; }

    public FolderNodeState State { get; internal set; } = FolderNodeState.NotLoaded;

    public FolderProblem? Problem { get; internal set; }

    public IReadOnlyList<FolderNode> Children { get; internal set; } = Array.Empty<FolderNode>();

    /// <summary>True until the node is loaded and found to have no sub-folder.</summary>
    public bool MayHaveChildren { get; internal set; } = true;
}
