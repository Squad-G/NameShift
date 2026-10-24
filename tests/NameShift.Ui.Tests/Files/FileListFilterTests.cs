using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

public sealed class FileListFilterTests
{
    private static RenameItem[] Many(int count) =>
        Enumerable.Range(0, count).Select(i => File("file" + i + ".txt")).ToArray();

    [Fact]
    public void Apply_KeepsIncluded_InOrder_SameInstances()
    {
        RenameItem[] items = { File("b.txt"), File("a.jpg"), File("c.txt"), Folder("d.txt") };

        IReadOnlyList<RenameItem> kept = FileListFilter.Apply(items, item => item.Name.EndsWith(".txt", StringComparison.Ordinal), CancellationToken.None);

        Assert.NotSame(items, kept);
        Assert.Equal(3, kept.Count);
        Assert.Same(items[0], kept[0]);
        Assert.Same(items[2], kept[1]);
        Assert.Same(items[3], kept[2]);
    }

    [Fact]
    public void Apply_CancelledToken_ThrowsBeforeFirstItem()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        int calls = 0;

        Assert.ThrowsAny<OperationCanceledException>(() => FileListFilter.Apply(Many(3), _ => ++calls > 0, cts.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Apply_CancelledMidway_StopsAtOnce()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;

        Assert.ThrowsAny<OperationCanceledException>(() => FileListFilter.Apply(
            Many(10000),
            _ =>
            {
                if (++calls == 5)
                {
                    cts.Cancel();
                }

                return true;
            },
            cts.Token));
        Assert.Equal(5, calls);
    }

    [Fact]
    public void Apply_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FileListFilter.Apply(null!, _ => true, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => FileListFilter.Apply(Many(1), null!, CancellationToken.None));
    }
}
