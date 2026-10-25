using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Core.Tests;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

public sealed class PhotoDatesTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "x");
    private static readonly DateTime Modified = new DateTime(2024, 3, 4, 5, 6, 7);
    private static readonly DateTime Date1 = new DateTime(2023, 7, 1, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Date2 = new DateTime(2021, 12, 31, 23, 59, 58, DateTimeKind.Unspecified);
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static RenameItem Photo(string name, long size = 100, DateTime? modified = null, string? folder = null) =>
        new RenameItem(name, folder ?? Folder, ItemKind.File, ItemAttributes.None, size, Modified, modified ?? Modified);

    private static string PathOf(string name) => Path.Combine(Folder, name);

    private static PhotoDates Over(FakePhotoFile fake, int capacity = PhotoDates.DefaultCapacity) => new PhotoDates(fake, capacity);

    // AC 1

    [Fact]
    public void Fill_SameOrder_NewList_InputUnchanged()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        fake.Answer(PathOf("c.JPEG"), PhotoRead.Found(Date2));
        RenameItem[] input =
        [
            Photo("a.jpg"),
            Photo("b.txt"),
            Photo("c.JPEG"),
            new RenameItem("d.jpg", Folder, ItemKind.Folder),
            Photo("e.jpe"),
        ];
        RenameItem[] copy = (RenameItem[])input.Clone();

        IReadOnlyList<RenameItem> result = Over(fake).Fill(input, CancellationToken.None);

        Assert.Equal(5, result.Count);
        Assert.Equal(input.Select(i => i.Name), result.Select(i => i.Name));
        Assert.Same(input[1], result[1]);
        Assert.Same(input[3], result[3]);
        Assert.Same(input[4], result[4]);
        Assert.Equal(0, fake.CallsFor(PathOf("b.txt")));
        Assert.Equal(0, fake.CallsFor(PathOf("d.jpg")));
        Assert.Equal(0, fake.CallsFor(PathOf("e.jpe")));
        Assert.Equal(2, fake.Calls);
        Assert.NotSame(input[0], result[0]);
        Assert.NotSame(input[2], result[2]);
        Assert.Equal(Date1, result[0].Taken);
        Assert.Equal(Date2, result[2].Taken);
        Assert.Equal(copy, input);
        Assert.All(input, i => Assert.Null(i.Taken));
    }

    [Fact]
    public void Fill_DateStoredAsReturned()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));

        RenameItem item = Over(fake).Fill([Photo("a.jpg")], CancellationToken.None)[0];

        Assert.Equal(Date1, item.Taken);
        Assert.Equal(DateTimeKind.Unspecified, item.Taken!.Value.Kind);
        Assert.Equal(Date1.Ticks, item.Taken.Value.Ticks);
    }

    [Fact]
    public void Fill_KeepsTheOtherFacts()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        var input = new RenameItem("a.jpg", Folder, ItemKind.File, ItemAttributes.Hidden | ItemAttributes.ReadOnly, 1234, Date2, Modified);

        RenameItem item = Over(fake).Fill([input], CancellationToken.None)[0];

        Assert.Equal(input.Name, item.Name);
        Assert.Equal(input.FolderPath, item.FolderPath);
        Assert.Equal(input.Kind, item.Kind);
        Assert.Equal(input.Attributes, item.Attributes);
        Assert.Equal(input.Size, item.Size);
        Assert.Equal(input.Created, item.Created);
        Assert.Equal(input.Modified, item.Modified);
    }

    [Fact]
    public void Fill_NoDateAndFailed_GiveNull()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("n.jpg"), PhotoRead.NoDate);
        fake.Answer(PathOf("f.jpg"), PhotoRead.Failed);
        RenameItem[] input = [Photo("n.jpg"), Photo("f.jpg")];

        IReadOnlyList<RenameItem> result = Over(fake).Fill(input, CancellationToken.None);

        Assert.NotSame(input[0], result[0]);
        Assert.NotSame(input[1], result[1]);
        Assert.Null(result[0].Taken);
        Assert.Null(result[1].Taken);
    }

    [Fact]
    public void Fill_NullArguments_Throw()
    {
        var dates = Over(new FakePhotoFile());

        Assert.Throws<ArgumentNullException>(() => dates.Fill(null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => dates.Fill([Photo("a.jpg"), null!], CancellationToken.None));
    }

    [Fact]
    public void Fill_Empty_GivesEmpty()
    {
        Assert.Empty(Over(new FakePhotoFile()).Fill([], CancellationToken.None));
    }

    // AC 2

    [Fact]
    public void Placeholder_NotCached_TriedAgain()
    {
        int attributeCalls = 0;
        int opens = 0;
        var file = new DiskPhotoFile(
            _ =>
            {
                attributeCalls++;
                return (FileAttributes)DiskPhotoFile.RecallOnDataAccessFlag | FileAttributes.ReparsePoint;
            },
            _ =>
            {
                opens++;
                return new MemoryStream(JpegBuilder.ValidFile(false));
            });
        var dates = new PhotoDates(file, PhotoDates.DefaultCapacity);
        RenameItem[] input = [Photo("cloud.jpg")];

        Assert.Null(dates.Fill(input, CancellationToken.None)[0].Taken);
        Assert.Null(dates.Fill(input, CancellationToken.None)[0].Taken);

        Assert.Equal(2, attributeCalls);
        Assert.Equal(0, opens);
        Assert.Equal(0, dates.CachedCount);
    }

    [Fact]
    public void OneFailure_NextFilesStillRead()
    {
        var file = new DiskPhotoFile(
            _ => FileAttributes.Normal,
            path => Path.GetFileName(path) == "2.jpg"
                ? throw new IOException("locked")
                : new MemoryStream(JpegBuilder.ValidFile(false)));
        var dates = new PhotoDates(file, PhotoDates.DefaultCapacity);

        IReadOnlyList<RenameItem> result = dates.Fill([Photo("1.jpg"), Photo("2.jpg"), Photo("3.jpg")], CancellationToken.None);

        Assert.Equal(Date1, result[0].Taken);
        Assert.Null(result[1].Taken);
        Assert.Equal(Date1, result[2].Taken);
    }

    // AC 3

    [Fact]
    public void SecondFill_NoSeamCall()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        fake.Answer(PathOf("b.jpg"), PhotoRead.Found(Date2));
        RenameItem[] input = [Photo("a.jpg"), Photo("b.jpg"), Photo("c.jpg")];
        var dates = Over(fake);

        IReadOnlyList<RenameItem> first = dates.Fill(input, CancellationToken.None);
        int calls = fake.Calls;
        IReadOnlyList<RenameItem> second = dates.Fill(input, CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal(calls, fake.Calls);
        Assert.Equal(first.Select(i => i.Taken), second.Select(i => i.Taken));
        Assert.Equal(new DateTime?[] { Date1, Date2, null }, second.Select(i => i.Taken));
    }

    [Fact]
    public void Cache_PathIgnoresCase()
    {
        var fake = new FakePhotoFile();
        fake.Answer(Path.Combine(Path.GetTempPath(), "X", "A.JPG"), PhotoRead.Found(Date1));
        var dates = Over(fake);

        dates.Fill([Photo("A.JPG", folder: Path.Combine(Path.GetTempPath(), "X"))], CancellationToken.None);
        RenameItem item = dates.Fill([Photo("a.jpg", folder: Path.Combine(Path.GetTempPath(), "x"))], CancellationToken.None)[0];

        Assert.Equal(1, fake.Calls);
        Assert.Equal(Date1, item.Taken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cache_SizeOrModifiedChanged_ReadAgain(bool changeSize)
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        var dates = Over(fake);
        RenameItem changed = changeSize ? Photo("a.jpg", size: 101) : Photo("a.jpg", modified: Modified.AddTicks(1));

        dates.Fill([Photo("a.jpg")], CancellationToken.None);
        dates.Fill([changed], CancellationToken.None);

        Assert.Equal(2, fake.CallsFor(PathOf("a.jpg")));
        Assert.Equal(1, dates.CachedCount);

        // The new stamp replaced the old entry: the changed item is now a hit.
        dates.Fill([changed], CancellationToken.None);
        Assert.Equal(2, fake.Calls);
    }

    [Fact]
    public void Cache_NoDateCached_FailedNotCached()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("n.jpg"), PhotoRead.NoDate);
        fake.Answer(PathOf("f.jpg"), PhotoRead.Failed);
        var dates = Over(fake);
        RenameItem[] input = [Photo("n.jpg"), Photo("f.jpg")];

        dates.Fill(input, CancellationToken.None);
        dates.Fill(input, CancellationToken.None);

        Assert.Equal(1, fake.CallsFor(PathOf("n.jpg")));
        Assert.Equal(2, fake.CallsFor(PathOf("f.jpg")));
        Assert.Equal(1, dates.CachedCount);
    }

    // AC 4

    [Fact]
    public void Capacity_ClearsWhenFull()
    {
        var fake = new FakePhotoFile();
        RenameItem[] input = Enumerable.Range(0, 4).Select(i => Photo(i + ".jpg")).ToArray();
        for (int i = 0; i < 4; i++)
        {
            fake.Answer(PathOf(i + ".jpg"), PhotoRead.Found(Date1.AddDays(i)));
        }

        var dates = Over(fake, 3);

        IReadOnlyList<RenameItem> result = dates.Fill(input, CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, 4).Select(i => (DateTime?)Date1.AddDays(i)), result.Select(r => r.Taken));
        Assert.Equal(1, dates.CachedCount);
        Assert.Equal(4, fake.Calls);

        RenameItem again = dates.Fill([input[0]], CancellationToken.None)[0];

        Assert.Equal(5, fake.Calls);
        Assert.Equal(Date1, again.Taken);
    }

    [Fact]
    public void Capacity_ReplacingAnEntry_DoesNotClear()
    {
        var fake = new FakePhotoFile();
        var dates = Over(fake, 2);

        dates.Fill([Photo("a.jpg"), Photo("b.jpg")], CancellationToken.None);
        dates.Fill([Photo("a.jpg", size: 5)], CancellationToken.None);

        Assert.Equal(3, fake.Calls);
        Assert.Equal(2, dates.CachedCount);

        dates.Fill([Photo("b.jpg")], CancellationToken.None);
        Assert.Equal(3, fake.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Capacity_BelowOne_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoDates(new FakePhotoFile(), capacity));
    }

    [Fact]
    public void Capacity_One_Works()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        var dates = Over(fake, 1);

        IReadOnlyList<RenameItem> result = dates.Fill([Photo("a.jpg"), Photo("b.jpg"), Photo("a.jpg")], CancellationToken.None);

        Assert.Equal(new DateTime?[] { Date1, null, Date1 }, result.Select(r => r.Taken));
        Assert.Equal(1, dates.CachedCount);
    }

    // AC 5

    [Fact]
    public void Cancel_BeforeNextFile()
    {
        var fake = new FakePhotoFile();
        using var cts = new CancellationTokenSource();
        fake.OnRead = _ =>
        {
            if (fake.Calls == 3)
            {
                cts.Cancel();
            }

            return null;
        };
        RenameItem[] input = Enumerable.Range(0, 10).Select(i => Photo(i + ".jpg")).ToArray();
        var dates = Over(fake);

        Assert.Throws<OperationCanceledException>(() => dates.Fill(input, cts.Token));
        Assert.Equal(3, fake.Calls);
        Assert.Equal(3, dates.CachedCount);

        fake.OnRead = null;
        dates.Fill(input, CancellationToken.None);

        Assert.Equal(10, fake.Calls);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_NoSeamCall()
    {
        var fake = new FakePhotoFile();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => Over(fake).Fill([Photo("b.txt"), Photo("a.jpg")], cts.Token));
        Assert.Equal(0, fake.Calls);
    }

    // AC 6

    [Fact]
    public async Task TwoCalls_Concurrent_Correct()
    {
        const int Count = 2000;
        var fake = new FakePhotoFile();
        RenameItem[] input = new RenameItem[Count];
        for (int i = 0; i < Count; i++)
        {
            input[i] = Photo(i + ".jpg");
            fake.Answer(PathOf(i + ".jpg"), PhotoRead.Found(Date1.AddMinutes(i)));
        }

        RenameItem[] reversed = input.Reverse().ToArray();
        var dates = Over(fake);

        Task<IReadOnlyList<RenameItem>>[] tasks =
        [
            Task.Run(() => dates.Fill(input, CancellationToken.None)),
            Task.Run(() => dates.Fill(input, CancellationToken.None)),
            Task.Run(() => dates.Fill(reversed, CancellationToken.None)),
        ];

        Assert.True(await Within(Task.WhenAll(tasks), TimeSpan.FromSeconds(30)));
        IReadOnlyList<RenameItem>[] results = await Task.WhenAll(tasks);
        foreach (IReadOnlyList<RenameItem> result in results)
        {
            Assert.Equal(Count, result.Count);
            foreach (RenameItem item in result)
            {
                int i = int.Parse(Path.GetFileNameWithoutExtension(item.Name), CultureInfo.InvariantCulture);
                Assert.Equal(Date1.AddMinutes(i), item.Taken);
            }
        }

        Assert.Equal(input.Select(i => i.Name), results[0].Select(i => i.Name));
        Assert.Equal(input.Select(i => i.Name), results[1].Select(i => i.Name));
        Assert.Equal(reversed.Select(i => i.Name), results[2].Select(i => i.Name));
        Assert.Equal(Count, dates.CachedCount);
    }

    [Fact]
    public async Task LockNotHeldDuringRead()
    {
        var fake = new FakePhotoFile();
        fake.Answer(PathOf("slow.jpg"), PhotoRead.Found(Date2));
        fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Date1));
        fake.Answer(PathOf("other.jpg"), PhotoRead.Found(Date1));
        var dates = Over(fake);
        dates.Fill([Photo("a.jpg"), Photo("b.jpg")], CancellationToken.None);

        var entered = new ManualResetEventSlim();
        var gate = new ManualResetEventSlim();
        fake.OnRead = path =>
        {
            if (string.Equals(path, PathOf("slow.jpg"), StringComparison.OrdinalIgnoreCase))
            {
                entered.Set();
                gate.Wait(TimeSpan.FromSeconds(30));
            }

            return null;
        };

        Task<IReadOnlyList<RenameItem>>? a = null;
        try
        {
            a = Task.Run(() => dates.Fill([Photo("slow.jpg")], CancellationToken.None));
            Assert.True(entered.Wait(Bound));

            Task<IReadOnlyList<RenameItem>> b = Task.Run(() =>
                dates.Fill([Photo("a.jpg"), Photo("b.jpg"), Photo("other.jpg")], CancellationToken.None));

            Assert.True(await Within(b, Bound));
            Assert.False(a.IsCompleted);
            Assert.Equal(new DateTime?[] { Date1, null, Date1 }, (await b).Select(r => r.Taken));

            gate.Set();
            Assert.True(await Within(a, Bound));
            Assert.Equal(Date2, (await a)[0].Taken);
        }
        finally
        {
            gate.Set();
            if (a == null || await Within(a, Bound))
            {
                entered.Dispose();
                gate.Dispose();
            }
        }
    }

    /// <summary>True when <paramref name="task"/> ends within <paramref name="bound"/> (a test never hangs).</summary>
    private static async Task<bool> Within(Task task, TimeSpan bound) =>
        await Task.WhenAny(task, Task.Delay(bound)) == task;

    // AC 8

    [Fact]
    public void TenThousand_NoFiles_NoThrow()
    {
        string missing = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
        RenameItem[] input = Enumerable.Range(0, 10000)
            .Select(i => new RenameItem(i + ".jpg", missing, ItemKind.File, ItemAttributes.None, 100, Modified, Modified))
            .ToArray();

        IReadOnlyList<RenameItem> result = new PhotoDates().Fill(input, CancellationToken.None);

        Assert.False(Directory.Exists(missing));
        Assert.Equal(10000, result.Count);
        Assert.All(result, r => Assert.Null(r.Taken));
    }
}
