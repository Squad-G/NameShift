using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each name mode.</summary>
public static class NameModeText
{
    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value.</exception>
    public static string For(NameMode mode)
    {
        switch (mode)
        {
            case NameMode.Keep:
                return Strings.NameModeKeep;
            case NameMode.Fixed:
                return Strings.NameModeFixed;
            case NameMode.Remove:
                return Strings.NameModeRemove;
            case NameMode.Reverse:
                return Strings.NameModeReverse;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown name mode.");
        }
    }
}
