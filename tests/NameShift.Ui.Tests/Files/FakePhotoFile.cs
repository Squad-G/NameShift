using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NameShift.Ui.Files;

namespace NameShift.Ui.Tests.Files;

/// <summary>A thread-safe in-memory <see cref="IPhotoFile"/>: answers by full path (ignoring case), an unknown path
/// gives <see cref="PhotoRead.NoDate"/>. Counts every call, per path and in all. Set the answers before the
/// calls.</summary>
internal sealed class FakePhotoFile : IPhotoFile
{
    private readonly Dictionary<string, PhotoRead> _answers = new Dictionary<string, PhotoRead>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _calls = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int _total;

    /// <summary>Called on each read before the answer is looked up; its answer is used when it returns one.</summary>
    public Func<string, PhotoRead?>? OnRead { get; set; }

    /// <summary>All the calls so far.</summary>
    public int Calls => Volatile.Read(ref _total);

    /// <summary>Sets the answer for <paramref name="path"/> (not thread-safe: before the calls).</summary>
    public void Answer(string path, PhotoRead read) => _answers[path] = read;

    /// <summary>The calls for <paramref name="path"/> so far.</summary>
    public int CallsFor(string path) => _calls.TryGetValue(path, out int n) ? n : 0;

    public PhotoRead Read(string path)
    {
        Interlocked.Increment(ref _total);
        _calls.AddOrUpdate(path, 1, (_, n) => n + 1);
        PhotoRead? hooked = OnRead?.Invoke(path);
        if (hooked.HasValue)
        {
            return hooked.Value;
        }

        return _answers.TryGetValue(path, out PhotoRead read) ? read : PhotoRead.NoDate;
    }
}
