using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each extension mode.</summary>
public static class ExtensionModeText
{
    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value.</exception>
    public static string For(ExtensionMode mode)
    {
        switch (mode)
        {
            case ExtensionMode.Same:
                return Strings.ExtensionModeSame;
            case ExtensionMode.Lower:
                return Strings.ExtensionModeLower;
            case ExtensionMode.Upper:
                return Strings.ExtensionModeUpper;
            case ExtensionMode.Fixed:
                return Strings.ExtensionModeFixed;
            case ExtensionMode.Extra:
                return Strings.ExtensionModeExtra;
            case ExtensionMode.Remove:
                return Strings.ExtensionModeRemove;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown extension mode.");
        }
    }
}
