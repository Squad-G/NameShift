using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Presets;

namespace NameShift.App.Controls;

/// <summary>The menu bar with the Presets menu. Renders <see cref="PresetMenuModel"/> and forwards the clicks; no logic
/// of its own.</summary>
internal sealed class MainMenu : MenuStrip
{
    private readonly PresetMenuModel _model;
    private readonly Func<string, bool, bool> _askYesNo;
    private readonly Func<string, string?> _askName;
    private readonly Action<string> _showStatus;
    private readonly Func<bool> _isClosed;
    private readonly ToolStripMenuItem _presets = new ToolStripMenuItem(Strings.PresetsMenu);
    private readonly ToolStripMenuItem _save = new ToolStripMenuItem(Strings.PresetsSave);
    private readonly ToolStripMenuItem _load = new ToolStripMenuItem(Strings.PresetsLoad);
    private readonly ToolStripMenuItem _delete = new ToolStripMenuItem(Strings.PresetsDelete);

    public MainMenu(
        PresetMenuModel model,
        Func<string, bool, bool> askYesNo,
        Func<string, string?> askName,
        Action<string> showStatus,
        Func<bool> isClosed)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _askYesNo = askYesNo ?? throw new ArgumentNullException(nameof(askYesNo));
        _askName = askName ?? throw new ArgumentNullException(nameof(askName));
        _showStatus = showStatus ?? throw new ArgumentNullException(nameof(showStatus));
        _isClosed = isClosed ?? throw new ArgumentNullException(nameof(isClosed));

        _presets.DropDownItems.Add(_save);
        _presets.DropDownItems.Add(_load);
        _presets.DropDownItems.Add(_delete);
        Items.Add(_presets);
        RebuildLists();

        _presets.DropDownOpening += OnOpening;
        _save.Click += OnSaveClick;
        _model.ItemsChanged += (_, _) => RebuildLists();
    }

    private void RebuildLists()
    {
        Fill(_load, OnLoad);
        Fill(_delete, OnDelete);
    }

    /// <summary>Replaces the sub-menu's entries with the model's. Never <c>DropDownItems.Add(string)</c>: a preset named
    /// "-" would become a separator.</summary>
    private void Fill(ToolStripMenuItem menu, Action<string> onClick)
    {
        var old = new List<ToolStripItem>();
        foreach (ToolStripItem item in menu.DropDownItems)
        {
            old.Add(item);
        }

        menu.DropDownItems.Clear();
        foreach (PresetMenuItem entry in _model.Items)
        {
            var item = new ToolStripMenuItem(entry.Text) { Enabled = entry.Enabled };
            string? name = entry.PresetName;
            if (name != null)
            {
                item.Click += (_, _) => onClick(name);
            }

            menu.DropDownItems.Add(item);
        }

        foreach (ToolStripItem item in old)
        {
            item.Dispose();
        }
    }

    private async void OnOpening(object? sender, EventArgs e)
    {
        try
        {
            _save.Enabled = _model.CanSave;
            string? text = await _model.OpenAsync();
            if (text != null && !_isClosed())
            {
                _showStatus(text);
            }
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private async void OnSaveClick(object? sender, EventArgs e)
    {
        try
        {
            string? typed = _askName(_model.LastName);
            if (typed is null)
            {
                return;
            }

            ShowResult(await _model.SaveAsync(typed, question => _askYesNo(question, false)));
        }
        catch (Exception)
        {
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private async void OnLoad(string name)
    {
        try
        {
            ShowResult(await _model.LoadAsync(name, _isClosed));
        }
        catch (Exception)
        {
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private async void OnDelete(string name)
    {
        try
        {
            ShowResult(await _model.DeleteAsync(name, question => _askYesNo(question, false)));
        }
        catch (Exception)
        {
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private void ShowResult(string? text)
    {
        if (text != null && !_isClosed())
        {
            _showStatus(text);
        }
    }
}
