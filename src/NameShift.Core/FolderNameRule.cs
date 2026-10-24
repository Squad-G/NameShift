using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Core;

/// <summary>Adds the names of the item's containing folders (outer first, as read in the path) before or after the
/// base name. Reads only the text of RenameItem.FolderPath: never the disk. The drive, and the server and share of a
/// network path, are never used.</summary>
public sealed class FolderNameRule : IRenameRule
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>Creates the rule.</summary>
    /// <exception cref="ArgumentOutOfRangeException">placement is not a defined value.</exception>
    /// <exception cref="ArgumentNullException">separator is null.</exception>
    public FolderNameRule(FolderNamePlacement placement = FolderNamePlacement.Prefix, int levels = 1,
        string separator = "_")
    {
        if (!Enum.IsDefined(typeof(FolderNamePlacement), placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown folder name placement.");
        }

        Placement = placement;
        Levels = levels;
        Separator = separator ?? throw new ArgumentNullException(nameof(separator));
    }

    /// <summary>Where the folder names go.</summary>
    public FolderNamePlacement Placement { get; }

    /// <summary>How many folders, counted up from the item; below 1 disables the rule; more than the path has uses
    /// them all.</summary>
    public int Levels { get; }

    /// <summary>Between the folder names, and between them and the base name; may be empty.</summary>
    public string Separator { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.FolderName;

    /// <summary>True when Levels is at least 1.</summary>
    public bool Enabled => Levels >= 1;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (Levels < 1)
        {
            return name;
        }

        var names = FolderNames(context.Item.FolderPath);
        if (names.Count == 0)
        {
            return name;
        }

        var take = Math.Min(Levels, names.Count);
        var text = string.Join(Separator, names.Skip(names.Count - take));
        return Placement == FolderNamePlacement.Prefix
            ? name.WithBaseName(text + Separator + name.BaseName)
            : name.WithBaseName(name.BaseName + Separator + text);
    }

    /// <summary>The usable folder names of a path, outer first: segments split on '\' and '/', empty ones dropped,
    /// without the drive (a first segment ending in ':') or, for a path starting with two separators, the server and
    /// share. Never null, never throws for a non-null path.</summary>
    private static IReadOnlyList<string> FolderNames(string folderPath)
    {
        var segments = folderPath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var isNetwork = folderPath.Length >= 2 && IsSeparator(folderPath[0]) && IsSeparator(folderPath[1]);
        int skip;
        if (isNetwork)
        {
            skip = Math.Min(2, segments.Length);
        }
        else
        {
            skip = segments.Length > 0 && segments[0][segments[0].Length - 1] == ':' ? 1 : 0;
        }

        return segments.Skip(skip).ToList();
    }

    private static bool IsSeparator(char c) => c == '\\' || c == '/';
}
