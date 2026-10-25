using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The pure rule that leaves out the items inside hidden or system folders. A tilde in a path stands for a
/// backslash, so that no escape sequence is written in this file.</summary>
public sealed class HiddenFoldersTests
{
    private static readonly string Root = Real("C:~R");

    private static string Real(string text) => text.Replace('~', (char)92);

    private static RenameItem File(string folder, string name) =>
        new RenameItem(name, Real(folder), ItemKind.File);

    private static RenameItem Folder(string folder, string name, ItemAttributes attributes = ItemAttributes.None) =>
        new RenameItem(name, Real(folder), ItemKind.Folder, attributes);

    /// <summary>Each item as its path under the root, with a tilde for the separator.</summary>
    private static string[] Names(IEnumerable<RenameItem> items) =>
        items.Select(i => (i.FolderPath.Substring(Root.Length) + (char)92 + i.Name).TrimStart((char)92).Replace((char)92, '~')).ToArray();

    [Fact]
    public void LeaveOut_Table()
    {
        var listed = new List<RenameItem>
        {
            Folder("C:~R", "h", ItemAttributes.Hidden),
            Folder("C:~R", "s", ItemAttributes.System),
            Folder("C:~R", "v"),
            Folder("C:~R", "p"),
            Folder("C:~R", "hx"),
            File("C:~R", "root.txt"),
            File("C:~R~h", "a.txt"),
            Folder("C:~R~h", "p"),
            File("C:~R~h~p", "b.txt"),
            File("C:~R~s", "c.txt"),
            Folder("C:~R~v", "w"),
            File("C:~R~v", "e.txt"),
            Folder("C:~R~v~w", "hh", ItemAttributes.Hidden | ItemAttributes.ReadOnly),
            File("C:~R~v~w", "f.txt"),
            File("C:~R~v~w~hh", "d.txt"),
            File("C:~R~p", "g.txt"),
            File("C:~R~hx", "i.txt"),
        };

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, listed, listed);

        Assert.Equal(
            new[] { "h", "s", "v", "p", "hx", "root.txt", "v~e.txt", "v~w", "v~w~hh", "v~w~f.txt", "p~g.txt", "hx~i.txt" }.OrderBy(x => x).ToArray(),
            Names(kept).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void LeaveOut_KeepsTheOrderOfTheKeptItems()
    {
        RenameItem hidden = Folder("C:~R", "h", ItemAttributes.Hidden);
        RenameItem a = File("C:~R", "a.txt");
        RenameItem inside = File("C:~R~h", "x.txt");
        RenameItem b = File("C:~R~p", "b.txt");
        RenameItem c = File("C:~R", "c.txt");
        var listed = new[] { hidden, a, inside, b, c };

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, listed, new[] { c, b, inside, a });

        Assert.Equal(new[] { c, b, a }, kept);
    }

    [Fact]
    public void LeaveOut_HiddenFolderItself_StaysWhenItIsInTheRoot()
    {
        RenameItem hidden = Folder("C:~R", "h", ItemAttributes.Hidden);
        RenameItem inside = File("C:~R~h", "x.txt");

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, new[] { hidden, inside }, new[] { hidden, inside });

        Assert.Equal(new[] { hidden }, kept);
    }

    [Fact]
    public void LeaveOut_HiddenFolderBelowTheRoot_LeavesItsFolderItemIn_ButNotItsContent()
    {
        RenameItem plain = Folder("C:~R", "p");
        RenameItem hidden = Folder("C:~R~p", "h", ItemAttributes.System);
        RenameItem inside = File("C:~R~p~h", "x.txt");

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, new[] { plain, hidden, inside }, new[] { plain, hidden, inside });

        Assert.Equal(new[] { plain, hidden }, kept);
    }

    [Fact]
    public void LeaveOut_IgnoresCase()
    {
        RenameItem hidden = Folder("C:~R", "Git", ItemAttributes.Hidden);
        RenameItem inside = File("C:~R~GIT~sub", "x.txt");

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, new[] { hidden, inside }, new[] { inside });

        Assert.Empty(kept);
    }

    [Fact]
    public void LeaveOut_NoHiddenFolder_ReturnsTheSameList()
    {
        RenameItem plain = Folder("C:~R", "p");
        RenameItem file = File("C:~R~p", "x.txt");
        var kept = new[] { file };

        Assert.Same(kept, HiddenFolders.LeaveOut(Root, new[] { plain, file }, kept));
    }

    [Fact]
    public void LeaveOut_TheRootItselfIsNeverTested()
    {
        // The root is hidden on its own account: it is not an item of the listing, so its content is kept.
        RenameItem file = File("C:~R", "a.txt");
        RenameItem deeper = File("C:~R~sub", "b.txt");

        IReadOnlyList<RenameItem> kept = HiddenFolders.LeaveOut(Root, new[] { file, deeper }, new[] { file, deeper });

        Assert.Equal(new[] { file, deeper }, kept);
    }
}
