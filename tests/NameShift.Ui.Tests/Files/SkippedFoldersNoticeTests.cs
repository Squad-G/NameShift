using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-78 AC 4: one status line per landed load that skipped folders, with a fake source (no disk).</summary>
public sealed class SkippedFoldersNoticeTests : IDisposable
{
    private const string FolderA = @"C:\A";
    private const string FolderB = @"C:\B";
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FileListModel _files;
    private readonly SkippedFoldersNotice _notice;
    private readonly List<string> _notices = new List<string>();

    public SkippedFoldersNoticeTests()
    {
        _files = new FileListModel(_source);
        _notice = new SkippedFoldersNotice(_files, EnUs);
        _notice.Notice += (_, text) =>
        {
            lock (_notices)
            {
                _notices.Add(text);
            }
        };
    }

    public void Dispose()
    {
        _notice.Dispose();
        _files.Dispose();
    }

    private string[] Notices()
    {
        lock (_notices)
        {
            return _notices.ToArray();
        }
    }

    private void SetSkipped(string path, int skipped) =>
        _source.SetListing(path, FileListing.Ok(new[] { File("a.txt", folder: path) }, skipped));

    [Fact]
    public void Text()
    {
        Assert.Null(SkippedFoldersNotice.Text(0, EnUs));
        Assert.Equal("1 folder could not be read", SkippedFoldersNotice.Text(1, EnUs));
        Assert.Equal("3 folders could not be read", SkippedFoldersNotice.Text(3, EnUs));
        Assert.Equal("1,234 folders could not be read", SkippedFoldersNotice.Text(1234, EnUs));
        Assert.Equal("1.234 folders could not be read", SkippedFoldersNotice.Text(1234, CultureInfo.GetCultureInfo("de-DE")));
        Assert.Throws<ArgumentNullException>(() => SkippedFoldersNotice.Text(1, null!));
    }

    [Fact]
    public async Task Load_Skipping0_1_3()
    {
        SetSkipped(FolderA, 0);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));
        Assert.Empty(Notices());

        SetSkipped(FolderA, 1);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));
        Assert.Equal(new[] { "1 folder could not be read" }, Notices());

        SetSkipped(FolderA, 3);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));
        Assert.Equal(new[] { "1 folder could not be read", "3 folders could not be read" }, Notices());
    }

    [Fact]
    public async Task SecondLoadWithoutSkips_NoRepeat()
    {
        SetSkipped(FolderA, 3);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));
        SetSkipped(FolderA, 0);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));

        Assert.Equal(new[] { "3 folders could not be read" }, Notices());
    }

    [Fact]
    public async Task SupersededLoad_NoText()
    {
        SetSkipped(FolderA, 2);
        SetSkipped(FolderB, 0);
        using var gate = _source.Gate(FolderA);
        Task<bool> first = _files.LoadAsync(FolderA);

        Assert.True(await TestWait.Within(_files.LoadAsync(FolderB)));
        gate.Set();
        Assert.False(await TestWait.Within(first));

        Assert.Empty(Notices());
        Assert.Equal(0, _files.SkippedFolders);
    }

    [Fact]
    public async Task FailedLoad_NoText()
    {
        _source.SetListing(FolderA, FileListing.Failed(FolderProblem.Unavailable));
        await TestWait.Within(_files.LoadAsync(FolderA));

        Assert.Equal(FileListState.Failed, _files.State);
        Assert.Empty(Notices());
    }

    [Fact]
    public async Task Dispose_StopsNotices()
    {
        _notice.Dispose();
        SetSkipped(FolderA, 3);
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));

        Assert.Empty(Notices());
        _notice.Dispose();
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new SkippedFoldersNotice(null!, EnUs));
        Assert.Throws<ArgumentNullException>(() => new SkippedFoldersNotice(_files, null!));
    }
}
