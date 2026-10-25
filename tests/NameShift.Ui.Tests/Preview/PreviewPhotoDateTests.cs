using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Files;
using Xunit;
using static NameShift.Ui.Tests.Files.TestWait;

namespace NameShift.Ui.Tests.Preview;

/// <summary>The Photo date source in the preview (NS-86): the fill before planning, the snapshot flag and the Status
/// note. Photo reads go through a fake source.</summary>
public sealed class PreviewPhotoDateTests
{
    private static readonly string Dir = "C:" + (char)92 + "A";
    private static readonly DateTime Modified = new DateTime(2024, 3, 4, 5, 6, 7);
    private static readonly DateTime Taken = new DateTime(2023, 7, 1, 10, 0, 0);

    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FakeDelay _delay = new FakeDelay();
    private readonly FakePhotoFile _fake = new FakePhotoFile();
    private readonly FileListModel _files;
    private readonly AutoDatePanelModel _panel = new AutoDatePanelModel();
    private int _changes;

    public PreviewPhotoDateTests()
    {
        _files = new FileListModel(_source);
        _panel.Source = DateSource.Taken;
        _panel.Enabled = true;
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Taken));
        _fake.Answer(PathOf("b.jpg"), PhotoRead.NoDate);
    }

    private static string PathOf(string name) => System.IO.Path.Combine(Dir, name);

    private static RenameItem Photo(string name) =>
        new RenameItem(name, Dir, ItemKind.File, ItemAttributes.None, 100, Modified, Modified);

    private static RenameItem SubFolder(string name) => new RenameItem(name, Dir, ItemKind.Folder);

    private PreviewModel Build()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { _panel });
        var preview = new PreviewModel(host, _files, new PhotoDates(_fake, PhotoDates.DefaultCapacity), _delay.Delay);
        preview.Changed += (_, _) => Interlocked.Increment(ref _changes);
        return preview;
    }

    private async Task<PreviewModel> BuildLoaded(params RenameItem[] items)
    {
        _source.SetEntries(Dir, items);
        PreviewModel preview = Build();
        await _files.LoadAsync(Dir);
        await Within(preview.Pending);
        return preview;
    }

    private int Row(string name) => Array.FindIndex(_files.Items.ToArray(), i => i.Name == name);

    private static async Task Settle(PreviewModel preview, FakeDelay delay)
    {
        delay.ReleaseAll();
        await Within(preview.Pending);
    }

    [Fact]
    public async Task Taken_FillsBeforePlanning()
    {
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Equal("2023-07-01_a.jpg", preview.CellForRow(Row("a.jpg")).Text);
        Assert.Equal("2024-03-04_b.jpg", preview.CellForRow(Row("b.jpg")).Text);
        Assert.True(preview.Snapshot.PhotoDatesRead);
    }

    [Fact]
    public async Task OtherSource_NoRead()
    {
        _panel.Source = DateSource.Modified;

        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Equal(0, _fake.Calls);
        Assert.False(preview.Snapshot.PhotoDatesRead);
        Assert.Equal("2024-03-04_a.jpg", preview.CellForRow(Row("a.jpg")).Text);
    }

    [Fact]
    public async Task PanelOnButNoRule_NoRead()
    {
        _panel.Format = "";

        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Equal(0, _fake.Calls);
        Assert.False(preview.Snapshot.PhotoDatesRead);
        Assert.Equal("", preview.StatusForRow(Row("b.jpg")).Text);
    }

    [Fact]
    public async Task PanelOff_NoRead()
    {
        _panel.Enabled = false;

        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Equal(0, _fake.Calls);
        Assert.False(preview.Snapshot.PhotoDatesRead);
    }

    [Fact]
    public async Task OnlyTheBatch()
    {
        _source.SetEntries(Dir, Photo("a.jpg"), Photo("b.jpg"), Photo("c.jpg"), Photo("d.jpg"));
        PreviewModel preview = Build();
        _panel.Enabled = false;
        await _files.LoadAsync(Dir);
        await Within(preview.Pending);
        Assert.Equal(0, _fake.Calls);

        _files.SetSelection(new[] { Row("a.jpg"), Row("c.jpg") });
        _panel.Enabled = true;
        await Settle(preview, _delay);

        Assert.Equal(1, _fake.CallsFor(PathOf("a.jpg")));
        Assert.Equal(1, _fake.CallsFor(PathOf("c.jpg")));
        Assert.Equal(0, _fake.CallsFor(PathOf("b.jpg")));
        Assert.Equal(0, _fake.CallsFor(PathOf("d.jpg")));
        Assert.Equal(2, _fake.Calls);
    }

    [Fact]
    public async Task FormatEdit_NoReread_FailedReadTriedAgain()
    {
        _fake.Answer(PathOf("c.jpg"), PhotoRead.Failed);
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"), Photo("c.jpg"));
        Assert.Equal(3, _fake.Calls);

        _panel.Format = "yyyyMMdd";
        await Settle(preview, _delay);

        Assert.Equal("20230701_a.jpg", preview.CellForRow(Row("a.jpg")).Text);
        Assert.Equal(1, _fake.CallsFor(PathOf("a.jpg")));
        Assert.Equal(1, _fake.CallsFor(PathOf("b.jpg")));
        Assert.Equal(2, _fake.CallsFor(PathOf("c.jpg")));
    }

    [Fact]
    public async Task FillRunsOnPool()
    {
        // Failed reads are not cached, so the second run reads both files again.
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Failed);
        _fake.Answer(PathOf("b.jpg"), PhotoRead.Failed);
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));
        var threads = new List<(int Id, bool Pool)>();
        _fake.OnRead = _ =>
        {
            lock (threads)
            {
                threads.Add((Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread));
            }

            return null;
        };

        // The change comes from a thread of its own, which is busy or gone while the run works: the fill cannot be there.
        int caller = 0;
        var changer = new Thread(() =>
        {
            caller = Environment.CurrentManagedThreadId;
            _panel.Format = "yyyyMMdd";
        });
        changer.Start();
        changer.Join();
        await Settle(preview, _delay);

        Assert.Equal(2, threads.Count);
        Assert.All(threads, t => Assert.True(t.Pool));
        Assert.All(threads, t => Assert.NotEqual(caller, t.Id));
    }

    [Fact]
    public async Task SnapshotKeepsOriginalBatch()
    {
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Same(_files.Items, preview.Snapshot.Batch.ListItems);
        int a = Row("a.jpg");
        Assert.Null(preview.Snapshot.Items[a].Taken);
        Assert.Equal(Taken, preview.Snapshot.Results[a].Item.Taken);
        Assert.Equal("2023-07-01_a.jpg", preview.CellForRow(a).Text);
        Assert.Equal("", preview.StatusForRow(a).Text);
        Assert.Equal(Strings.PhotoDateMissing, preview.StatusForRow(Row("b.jpg")).Text);
    }

    [Fact]
    public async Task Cancelled_Superseded()
    {
        using var gate = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        int reads = 0;
        try
        {
            _fake.OnRead = _ =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                {
                    entered.Set();
                    Block(gate);
                }

                return null;
            };
            _source.SetEntries(Dir, Photo("a.jpg"), Photo("b.jpg"));
            PreviewModel preview = Build();
            await _files.LoadAsync(Dir);
            Task<bool> first = Assert.IsAssignableFrom<Task<bool>>(preview.Pending);
            Assert.True(entered.Wait(TestWait.Timeout));
            int before = _changes;

            _panel.Format = "yyyyMMdd";
            Task<bool> second = Assert.IsAssignableFrom<Task<bool>>(preview.Pending);
            gate.Set();
            _delay.ReleaseAll();

            Assert.False(await Within(first));
            Assert.True(await Within(second));
            Assert.Equal(before + 1, _changes);
            Assert.Equal("20230701_a.jpg", preview.CellForRow(Row("a.jpg")).Text);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task Status_NoPhotoDate_Note()
    {
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"), Photo("c.txt"), SubFolder("sub"));

        Assert.Equal(StatusCell.None.Text, preview.StatusForRow(Row("a.jpg")).Text);
        foreach (string name in new[] { "b.jpg", "c.txt", "sub" })
        {
            StatusCell cell = preview.StatusForRow(Row(name));
            Assert.Equal("No photo date: modified date used", cell.Text);
            Assert.Equal(Strings.PhotoDateMissing, cell.Text);
            Assert.False(cell.IsConflict);
        }
    }

    [Fact]
    public async Task Status_ConflictWins()
    {
        _panel.Separator = ":";

        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        StatusCell cell = preview.StatusForRow(Row("b.jpg"));
        Assert.True(cell.IsConflict);
        Assert.NotEqual(Strings.PhotoDateMissing, cell.Text);
        Assert.True(cell.Text.Length > 0);
    }

    [Fact]
    public async Task Status_FollowsSnapshotNotPanel()
    {
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));
        int b = Row("b.jpg");
        Assert.Equal(Strings.PhotoDateMissing, preview.StatusForRow(b).Text);

        _panel.Source = DateSource.Modified; // the run waits for its delay: the old snapshot is still the one shown

        Assert.Equal(Strings.PhotoDateMissing, preview.StatusForRow(b).Text);
        await Settle(preview, _delay);
        Assert.False(preview.Snapshot.PhotoDatesRead);
        Assert.Equal("", preview.StatusForRow(b).Text);
    }

    [Fact]
    public async Task NotBlocked()
    {
        PreviewModel preview = await BuildLoaded(Photo("a.jpg"), Photo("b.jpg"));

        Assert.Equal(RenameBlocker.None, preview.Snapshot.Blocker);
        Assert.Equal(2, preview.Snapshot.ChangedCount);
        Assert.Equal(Strings.PhotoDateMissing, preview.StatusForRow(Row("b.jpg")).Text);
    }

    [Fact]
    public void PublicSnapshots_ReadNoDates()
    {
        Assert.False(PreviewSnapshot.Empty.PhotoDatesRead);
        Assert.False(new PreviewSnapshot(
            Array.Empty<RenameItem>(), Array.Empty<RenameResult>(),
            new ConflictReport(Array.Empty<IReadOnlyList<Conflict>>())).PhotoDatesRead);
    }

    [Fact]
    public void PhotoDatesNull_Throws()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { _panel });

        Assert.Throws<ArgumentNullException>(() => new PreviewModel(host, _files, (PhotoDates)null!));
    }
}
