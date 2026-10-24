using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Presets;

/// <summary>
/// The state of the Presets menu and its save, load and delete flows. Runs on the UI thread: the store is called on the
/// pool, and everything after an <c>await</c> (the panels, the questions, <see cref="Items"/>) is back on the UI thread,
/// so nothing here uses <c>ConfigureAwait(false)</c>. Each flow returns the status text to show, or null when there is
/// nothing to say; none of them throws for a store problem.
/// </summary>
public sealed class PresetMenuModel
{
    private readonly PresetStore _store;
    private readonly RulePanelHostModel _rules;
    private readonly Func<bool> _isBusy;
    private int _version;

    public PresetMenuModel(PresetStore store, RulePanelHostModel rules, Func<bool> isBusy)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _isBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));
        Items = Placeholder(Strings.Loading);
    }

    /// <summary>True when a panel is enabled or not at default: a preset of nothing is a mistake.</summary>
    public bool CanSave => _rules.CanResetAny;

    /// <summary>What both sub-menus show. Starts as the Loading item; the store is read when the menu opens.</summary>
    public IReadOnlyList<PresetMenuItem> Items { get; private set; }

    /// <summary>The name the save dialog starts with: "" until a preset was saved or loaded in this run.</summary>
    public string LastName { get; private set; } = "";

    /// <summary>Raised on the calling thread after <see cref="Items"/> changed.</summary>
    public event EventHandler? ItemsChanged;

    /// <summary>Shows the Loading item, reads the list on the pool and shows the presets. Returns the "N presets could
    /// not be read." text, or null; also null when a newer opening took over.</summary>
    public async Task<string?> OpenAsync()
    {
        int version = ++_version;
        SetItems(Placeholder(Strings.Loading));
        PresetListing listing = await Task.Run(() => _store.List());
        if (version != _version)
        {
            return null;
        }

        SetItems(listing.Presets.Count == 0
            ? Placeholder(Strings.PresetsNone)
            : listing.Presets.Select(entry => new PresetMenuItem(PresetText.MenuText(entry.Name), entry.Name, true)).ToList());
        return PresetText.Unreadable(listing.UnusableCount);
    }

    /// <summary>Saves the panels under the typed name. When the name is taken, asks <paramref name="confirmReplace"/>
    /// with the question text; No writes nothing.</summary>
    public async Task<string?> SaveAsync(string typedName, Func<string, bool> confirmReplace)
    {
        if (confirmReplace == null)
        {
            throw new ArgumentNullException(nameof(confirmReplace));
        }

        PresetNameCheck check = PresetNames.Check(typedName);
        if (check.Verdict != PresetNameVerdict.Ok)
        {
            return null;
        }

        // Reads the panels: before the first await, on the UI thread.
        Preset preset = Preset.FromPanels(check.Name, _rules.Panels);
        PresetSaveResult result = await Task.Run(() => _store.Save(preset, false));
        if (result.Status == PresetSaveStatus.Exists)
        {
            if (!confirmReplace(PresetText.ReplaceQuestion(check.Name)))
            {
                return null;
            }

            result = await Task.Run(() => _store.Save(preset, true));
        }

        switch (result.Status)
        {
            case PresetSaveStatus.Saved:
                LastName = check.Name;
                return PresetText.Saved(check.Name);
            case PresetSaveStatus.Failed:
                return PresetText.SaveFailed(result.SystemMessage);
            default:
                // InvalidName cannot happen after the check, nor Exists after an overwrite.
                return null;
        }
    }

    /// <summary>Reads the preset on the pool and applies it to the panels (one preview run). Nothing is applied when the
    /// window closed or a Rename or Revert started meanwhile.</summary>
    public async Task<string?> LoadAsync(string name, Func<bool> isClosed)
    {
        if (isClosed == null)
        {
            throw new ArgumentNullException(nameof(isClosed));
        }

        PresetLoadResult result = await Task.Run(() => _store.Load(name));
        if (isClosed())
        {
            return null;
        }

        if (!result.Found)
        {
            return PresetText.NotFound(name);
        }

        if (_isBusy())
        {
            return Strings.PresetNotLoadedBusy;
        }

        _rules.ApplyPreset(result.Preset!);
        LastName = name;
        return PresetText.Loaded(name);
    }

    /// <summary>Asks <paramref name="confirmDelete"/> with the question text, then deletes the preset on the pool.</summary>
    public async Task<string?> DeleteAsync(string name, Func<string, bool> confirmDelete)
    {
        if (confirmDelete == null)
        {
            throw new ArgumentNullException(nameof(confirmDelete));
        }

        if (!confirmDelete(PresetText.DeleteQuestion(name)))
        {
            return null;
        }

        PresetDeleteResult result = await Task.Run(() => _store.Delete(name));
        switch (result.Status)
        {
            case PresetDeleteStatus.Deleted:
                return PresetText.Deleted(name);
            case PresetDeleteStatus.NotFound:
                return PresetText.NotFound(name);
            default:
                return PresetText.DeleteFailed(result.SystemMessage);
        }
    }

    private static IReadOnlyList<PresetMenuItem> Placeholder(string text) =>
        new[] { new PresetMenuItem(text, null, false) };

    private void SetItems(IReadOnlyList<PresetMenuItem> items)
    {
        Items = items;
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }
}
