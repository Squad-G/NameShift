using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>
/// The filter settings as typed, and the one <see cref="ItemFilter"/> built from them. Plain properties: each change
/// rebuilds <see cref="Filter"/> and <see cref="Error"/>, then raises <see cref="Changed"/> synchronously on the
/// calling thread. It never touches the file list: the window passes <see cref="Filter"/> to
/// <see cref="FileListModel.SetFilterAsync"/>.
/// </summary>
public sealed class FilterPanelModel
{
    private string _mask = "";
    private string _pattern = "";
    private bool _ignoreCase;
    private bool _includeFiles = true;
    private bool _includeFolders = true;
    private bool _includeHidden;
    private string _minLengthText = "";
    private string _maxLengthText = "";

    /// <summary>A model with every setting at its default: its filter behaves as <c>new ItemFilter()</c>.</summary>
    public FilterPanelModel()
    {
        Filter = new ItemFilter();
        Rebuild();
    }

    /// <summary>Raised on the calling thread after a setting changed, once <see cref="Filter"/> and
    /// <see cref="Error"/> describe the new settings.</summary>
    public event EventHandler? Changed;

    /// <summary>The mask on the full name, as typed ("" by default; null is stored as "").</summary>
    public string Mask
    {
        get => _mask;
        set => SetField(ref _mask, value ?? "");
    }

    /// <summary>The regular expression on the base name, as typed ("" by default; null is stored as "").</summary>
    public string Pattern
    {
        get => _pattern;
        set => SetField(ref _pattern, value ?? "");
    }

    /// <summary>True: the pattern ignores case. False by default.</summary>
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetField(ref _ignoreCase, value);
    }

    /// <summary>False: files are out. True by default.</summary>
    public bool IncludeFiles
    {
        get => _includeFiles;
        set => SetField(ref _includeFiles, value);
    }

    /// <summary>False: folders are out. True by default.</summary>
    public bool IncludeFolders
    {
        get => _includeFolders;
        set => SetField(ref _includeFolders, value);
    }

    /// <summary>True: hidden and system items are in. False by default.</summary>
    public bool IncludeHidden
    {
        get => _includeHidden;
        set => SetField(ref _includeHidden, value);
    }

    /// <summary>The smallest base-name length, exactly as typed ("" by default: no minimum; null is stored as "").
    /// Text that is not a whole number from 0 up is ignored and reported in <see cref="Error"/>.</summary>
    public string MinLengthText
    {
        get => _minLengthText;
        set => SetField(ref _minLengthText, value ?? "");
    }

    /// <summary>The largest base-name length, exactly as typed ("" by default: no maximum; null is stored as "").
    /// Text that is not a whole number from 0 up is ignored and reported in <see cref="Error"/>.</summary>
    public string MaxLengthText
    {
        get => _maxLengthText;
        set => SetField(ref _maxLengthText, value ?? "");
    }

    /// <summary>The filter of the current settings, built once per change (its pattern is compiled once, never per
    /// item), before <see cref="Changed"/> is raised.</summary>
    public ItemFilter Filter { get; private set; }

    /// <summary>What is wrong with the settings, or null: the filter's own text (<see cref="ItemFilter.Error"/>), then
    /// "Enter a whole number for the length." once when a length text is invalid, joined by one space.</summary>
    public string? Error { get; private set; }

    /// <summary>True when every setting is at its default (the texts compared as stored: " " is not default).</summary>
    public bool IsDefault =>
        _mask.Length == 0
        && _pattern.Length == 0
        && !_ignoreCase
        && _includeFiles
        && _includeFolders
        && !_includeHidden
        && _minLengthText.Length == 0
        && _maxLengthText.Length == 0;

    /// <summary>Puts every setting back to its default and raises <see cref="Changed"/> once; does nothing when
    /// already default.</summary>
    public void Reset()
    {
        if (IsDefault)
        {
            return;
        }

        _mask = "";
        _pattern = "";
        _ignoreCase = false;
        _includeFiles = true;
        _includeFolders = true;
        _includeHidden = false;
        _minLengthText = "";
        _maxLengthText = "";
        Rebuild();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Rebuild();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        bool minValid = TryParseLength(_minLengthText, out int? min);
        bool maxValid = TryParseLength(_maxLengthText, out int? max);
        Filter = new ItemFilter(_mask, _pattern, _ignoreCase, _includeFiles, _includeFolders, _includeHidden, min, max);

        string? error = Filter.Error;
        if (!minValid || !maxValid)
        {
            error = error is null ? Strings.FilterLengthInvalid : error + " " + Strings.FilterLengthInvalid;
        }

        Error = error;
    }

    /// <summary>Empty or white space is no limit; otherwise a whole number from 0 up in ASCII digits (no sign, no
    /// separator). False, with no limit, for anything else.</summary>
    private static bool TryParseLength(string text, out int? value)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            value = null;
            return true;
        }

        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }
}
