using System;
using NameShift.Disk;

namespace NameShift.Ui.Actions;

/// <summary>What the user chose in the Revert dialog. Close (the only button when nothing can run) and the close box
/// are Cancel.</summary>
public enum RevertDialogChoice
{
    /// <summary>Revert without the changed files.</summary>
    RevertRest,

    /// <summary>Revert the changed files too.</summary>
    IncludeModified,

    /// <summary>Revert nothing.</summary>
    Cancel,
}

/// <summary>Which buttons the Revert dialog has for a check.</summary>
public sealed class RevertDialogOptions
{
    private RevertDialogOptions(bool showRevertRest, bool showIncludeModified)
    {
        ShowRevertRest = showRevertRest;
        ShowIncludeModified = showIncludeModified;
    }

    /// <summary>"Revert the rest": only when at least one entry reverts without the override.</summary>
    public bool ShowRevertRest { get; }

    /// <summary>"Revert changed files too": only when at least one entry was changed after the batch.</summary>
    public bool ShowIncludeModified { get; }

    /// <summary>True when nothing can run: the dialog has only Close.</summary>
    public bool CloseOnly => !ShowRevertRest && !ShowIncludeModified;

    public static RevertDialogOptions For(RevertCheck check)
    {
        if (check == null)
        {
            throw new ArgumentNullException(nameof(check));
        }

        return new RevertDialogOptions(check.AnyRevertable, check.AnyModified);
    }
}
