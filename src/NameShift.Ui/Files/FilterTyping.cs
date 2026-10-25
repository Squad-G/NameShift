using System;
using System.Threading;
using System.Threading.Tasks;

namespace NameShift.Ui.Files;

/// <summary>
/// The filter row's typing delay (NS-70): text typed in a box waits <see cref="TypingDelay"/> after the last key, then
/// goes to the <see cref="FilterPanelModel"/> in one set per field. <see cref="Flush"/> sets the pending texts at once
/// (before a check box change, on Enter, when a box loses focus), so the order the user made is kept. A change of the
/// model made by anything else (Reset, a check box set without a flush) drops the pending texts, so it is never undone
/// when the wait ends. <see cref="Dispose"/> drops them too and sets nothing. Every member is called on the UI thread;
/// the wait's continuation runs on the caller's synchronization context (no <c>ConfigureAwait(false)</c>), so it sets
/// the model on the UI thread too.
/// </summary>
public sealed class FilterTyping : IDisposable
{
    /// <summary>How long after the last key the typed text applies.</summary>
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

    private static readonly FilterField[] ApplyOrder = { FilterField.Mask, FilterField.Pattern, FilterField.MinLength, FilterField.MaxLength };

    private readonly FilterPanelModel _filters;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    // The text waiting per field (indexed by FilterField), or null when nothing waits.
    private readonly string?[] _pending = new string?[ApplyOrder.Length];

    // The current wait. Old sources are cancelled, never disposed: a delay may still hold the token.
    private CancellationTokenSource? _waitCts;
    private bool _applying;
    private bool _disposed;

    /// <summary>A typing delay over the model, waiting with <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    public FilterTyping(FilterPanelModel filters)
        : this(filters, (d, t) => Task.Delay(d, t))
    {
    }

    /// <summary>For tests: <paramref name="delay"/> replaces <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    internal FilterTyping(FilterPanelModel filters, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _filters.Changed += OnFiltersChanged;
    }

    /// <summary>The latest wait-then-apply task, for tests (never faults).</summary>
    internal Task PendingWait { get; private set; } = Task.CompletedTask;

    /// <summary>Keeps the text for the field (null is stored as "") and restarts the wait. Does nothing once
    /// disposed.</summary>
    public void Type(FilterField field, string text)
    {
        if (_disposed)
        {
            return;
        }

        _pending[Index(field)] = text ?? "";
        _waitCts?.Cancel();
        var cts = new CancellationTokenSource();
        _waitCts = cts;
        PendingWait = WaitThenApplyAsync(cts);
    }

    /// <summary>Ends the wait and sets every pending text now, in the order Mask, Pattern, MinLength, MaxLength. Each
    /// set may raise <see cref="FilterPanelModel.Changed"/>: the last filter wins. Does nothing once disposed.</summary>
    public void Flush()
    {
        if (_disposed)
        {
            return;
        }

        CancelWait();
        _applying = true;
        try
        {
            foreach (FilterField field in ApplyOrder)
            {
                int index = (int)field;
                string? text = _pending[index];
                if (text is null)
                {
                    continue;
                }

                // Cleared before the set, so a Changed handler that reads IsPending sees the truth.
                _pending[index] = null;
                Set(field, text);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>True when text typed in the field waits to be set.</summary>
    public bool IsPending(FilterField field) => _pending[Index(field)] != null;

    /// <summary>Drops the pending texts and stops listening; sets nothing. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelWait();
        ClearPending();
        _filters.Changed -= OnFiltersChanged;
    }

    private static int Index(FilterField field)
    {
        int index = (int)field;
        if (index < 0 || index >= ApplyOrder.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(field), field, "Not a filter text field.");
        }

        return index;
    }

    private async Task WaitThenApplyAsync(CancellationTokenSource cts)
    {
        try
        {
            await _delay(TypingDelay, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer key, a flush, an outside change or Dispose took over.
            return;
        }

        if (cts.IsCancellationRequested || _disposed)
        {
            return;
        }

        Flush();
    }

    private void Set(FilterField field, string text)
    {
        switch (field)
        {
            case FilterField.Mask:
                _filters.Mask = text;
                break;
            case FilterField.Pattern:
                _filters.Pattern = text;
                break;
            case FilterField.MinLength:
                _filters.MinLengthText = text;
                break;
            default:
                _filters.MaxLengthText = text;
                break;
        }
    }

    /// <summary>A change this class did not make (Reset, a check box, a preset): what waits would undo it.</summary>
    private void OnFiltersChanged(object? sender, EventArgs e)
    {
        if (_applying)
        {
            return;
        }

        CancelWait();
        ClearPending();
    }

    private void CancelWait()
    {
        _waitCts?.Cancel();
        _waitCts = null;
    }

    private void ClearPending()
    {
        for (int i = 0; i < _pending.Length; i++)
        {
            _pending[i] = null;
        }
    }
}
