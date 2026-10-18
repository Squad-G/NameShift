using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each numbering mode.</summary>
public static class NumberingModeText
{
    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value.</exception>
    public static string For(NumberingMode mode)
    {
        switch (mode)
        {
            case NumberingMode.Prefix:
                return Strings.NumberingModePrefix;
            case NumberingMode.Suffix:
                return Strings.NumberingModeSuffix;
            case NumberingMode.Insert:
                return Strings.NumberingModeInsert;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown numbering mode.");
        }
    }
}
