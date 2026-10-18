using System;
using System.Collections.Generic;
using System.Text;

namespace NameShift.Core;

/// <summary>Flags new names that Windows would refuse or silently change.</summary>
public sealed class InvalidNameCheck : IConflictCheck
{
    /// <summary>The longest name NTFS accepts, in UTF-16 units.</summary>
    private const int MaxNameLength = 255;

    private static readonly IReadOnlyList<Conflict> None = Array.Empty<Conflict>();

    private static readonly char[] ForbiddenCharacters = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
    };

    /// <summary>Checks the changed results; unchanged and error results will not be renamed, so they are not checked.</summary>
    public IReadOnlyList<IReadOnlyList<Conflict>> Check(IReadOnlyList<RenameResult> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        var found = new IReadOnlyList<Conflict>[results.Count];
        for (var i = 0; i < found.Length; i++)
        {
            found[i] = results[i].Status == RenameStatus.Changed ? CheckName(results[i].NewName) : None;
        }

        return found;
    }

    /// <summary>Returns the conflicts of one full name, in a fixed order; empty when the name is valid.</summary>
    private static IReadOnlyList<Conflict> CheckName(string name)
    {
        if (IsOnlyDotsAndSpaces(name))
        {
            return [new Conflict(ConflictCode.EmptyName)];
        }

        var conflicts = new List<Conflict>();

        var invalid = OffendingCharacters(name);
        if (invalid.Length > 0)
        {
            conflicts.Add(new Conflict(ConflictCode.InvalidCharacter, invalid));
        }

        var firstDot = name.IndexOf('.');
        var stem = (firstDot < 0 ? name : name.Substring(0, firstDot)).TrimEnd(' ');
        if (DeviceNames.Contains(stem))
        {
            conflicts.Add(new Conflict(ConflictCode.ReservedName, stem));
        }

        var last = name[name.Length - 1];
        if (last == '.' || last == ' ')
        {
            conflicts.Add(new Conflict(ConflictCode.TrailingDotOrSpace));
        }

        if (name.Length > MaxNameLength)
        {
            conflicts.Add(new Conflict(ConflictCode.NameTooLong));
        }

        return conflicts;
    }

    private static bool IsOnlyDotsAndSpaces(string name)
    {
        foreach (var c in name)
        {
            if (c != '.' && c != ' ')
            {
                return false;
            }
        }

        return true;
    }

    private static string OffendingCharacters(string name)
    {
        var found = new StringBuilder();
        var seen = new HashSet<char>();
        foreach (var c in name)
        {
            if ((c < ' ' || Array.IndexOf(ForbiddenCharacters, c) >= 0) && seen.Add(c))
            {
                found.Append(c);
            }
        }

        return found.ToString();
    }
}
