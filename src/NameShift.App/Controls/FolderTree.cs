using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Folders;

namespace NameShift.App.Controls;

/// <summary>Renders the folder tree model: children load lazily, off the UI thread, on first expand.</summary>
internal sealed class FolderTree : UserControl
{
    private readonly FolderTreeModel _model;
    private readonly Action<string> _showStatus;
    private readonly TreeView _tree = new TreeView();
    private int _revealCounter;

    public FolderTree(FolderTreeModel model, Action<string> showStatus)
    {
        _model = model;
        _showStatus = showStatus;
        BuildLayout();
    }

    /// <summary>Raised when the user selects a folder; carries its full path.</summary>
    public event EventHandler<string>? FolderSelected;

    /// <summary>Expands the tree to <paramref name="path"/> and selects it; clears the selection when it is not in the tree.</summary>
    public async Task RevealAsync(string path)
    {
        try
        {
            int reveal = ++_revealCounter;
            FolderNode? target = await _model.RevealAsync(path);
            if (reveal != _revealCounter || IsDisposed)
            {
                return;
            }

            if (target is null)
            {
                _tree.SelectedNode = null;
                return;
            }

            TreeNode? node = FindTreeNode(target);
            if (node is null)
            {
                _tree.SelectedNode = null;
                return;
            }

            _tree.SelectedNode = node;
            node.EnsureVisible();
        }
        catch (Exception e)
        {
            // Reveal runs fire-and-forget: report instead of letting the exception go unobserved.
            _showStatus(e.Message);
        }
    }

    private void BuildLayout()
    {
        _tree.Dock = DockStyle.Fill;
        _tree.HideSelection = false;
        _tree.ShowRootLines = true;
        _tree.ShowLines = false;
        _tree.FullRowSelect = true;
        _tree.AccessibleName = Strings.FolderTreeName;
        _tree.HandleCreated += (_, _) => SetWindowTheme(_tree.Handle, "explorer", null);
        _tree.BeforeExpand += OnBeforeExpand;
        _tree.BeforeSelect += (_, e) => e.Cancel = !(e.Node?.Tag is FolderNode);
        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is FolderNode folder)
            {
                FolderSelected?.Invoke(this, folder.FullPath);
            }
        };

        _tree.BeginUpdate();
        foreach (FolderNode root in _model.Roots)
        {
            _tree.Nodes.Add(CreateNode(root));
        }

        _tree.EndUpdate();
        Controls.Add(_tree);
    }

    private static TreeNode CreateNode(FolderNode folder)
    {
        var node = new TreeNode(folder.Name) { Tag = folder };
        if (folder.MayHaveChildren)
        {
            node.Nodes.Add(PlaceholderNode(Strings.Loading));
        }

        return node;
    }

    private static TreeNode PlaceholderNode(string text) => new TreeNode(text) { ForeColor = SystemColors.GrayText };

    private async void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        try
        {
            TreeNode treeNode = e.Node!;
            if (!(treeNode.Tag is FolderNode folder)
                || (folder.State != FolderNodeState.NotLoaded && folder.State != FolderNodeState.Failed))
            {
                return;
            }

            treeNode.Nodes.Clear();
            treeNode.Nodes.Add(PlaceholderNode(Strings.Loading));
            await _model.ExpandAsync(folder);
            if (IsDisposed || treeNode.TreeView is null)
            {
                return;
            }

            RenderChildren(treeNode, folder);
        }
        catch (Exception ex)
        {
            // An exception escaping an async void handler would end the process.
            _showStatus(ex.Message);
        }
    }

    private void RenderChildren(TreeNode treeNode, FolderNode folder)
    {
        _tree.BeginUpdate();
        try
        {
            treeNode.Nodes.Clear();
            if (folder.Problem is FolderProblem problem)
            {
                treeNode.Nodes.Add(PlaceholderNode(FolderProblemText.For(problem)));
                return;
            }

            foreach (FolderNode child in folder.Children)
            {
                treeNode.Nodes.Add(CreateNode(child));
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    /// <summary>Walks the tree nodes down to a model node, rendering each level from the model.</summary>
    private TreeNode? FindTreeNode(FolderNode target)
    {
        var chain = new System.Collections.Generic.Stack<FolderNode>();
        for (FolderNode? n = target; n != null; n = n.Parent)
        {
            chain.Push(n);
        }

        TreeNodeCollection level = _tree.Nodes;
        TreeNode? current = null;
        foreach (FolderNode folder in chain)
        {
            current = null;
            foreach (TreeNode candidate in level)
            {
                if (ReferenceEquals(candidate.Tag, folder))
                {
                    current = candidate;
                    break;
                }
            }

            if (current is null)
            {
                return null;
            }

            if (folder != target)
            {
                if (folder.State == FolderNodeState.Loaded && !IsRendered(current))
                {
                    RenderChildren(current, folder);
                }

                current.Expand();
            }

            level = current.Nodes;
        }

        return current;
    }

    private static bool IsRendered(TreeNode node) =>
        node.Nodes.Count != 1 || node.Nodes[0].Tag is FolderNode || node.Nodes[0].Text != Strings.Loading;

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);
}
