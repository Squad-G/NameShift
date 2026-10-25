using System;
using System.Threading.Tasks;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>
/// Sends every change of the filter row's model to the file list (NS-70): one
/// <see cref="FileListModel.SetFilterAsync"/> per <see cref="FilterPanelModel.Changed"/>, fire and forget. The list
/// ignores the filter it already has and applies the requested one to every later load, so the filters stay when
/// another folder opens. <see cref="FilterPanelModel.IncludeSubfolders"/> is not forwarded here (NS-78). Created once
/// by the window; every member is called on the UI thread.
/// </summary>
public sealed class FilterBinding : IDisposable
{
    private readonly FilterPanelModel _filters;
    private readonly FileListModel _files;
    private bool _disposed;

    /// <summary>Starts forwarding. Nothing runs now: both models start at the default filter.</summary>
    public FilterBinding(FilterPanelModel filters, FileListModel files)
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _filters.Changed += OnFiltersChanged;
    }

    /// <summary>The latest run, for tests (never faults).</summary>
    internal Task<bool> LastRun { get; private set; } = Task.FromResult(false);

    /// <summary>Stops forwarding. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _filters.Changed -= OnFiltersChanged;
    }

    private void OnFiltersChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        LastRun = RunAsync(_filters.Filter);
    }

    private async Task<bool> RunAsync(ItemFilter filter)
    {
        try
        {
            return await _files.SetFilterAsync(filter);
        }
        catch (Exception)
        {
            // The model never throws, but a fire-and-forget run must never take the window down.
            return false;
        }
    }
}
