using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each case mode.</summary>
public static class CaseModeText
{
    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value.</exception>
    public static string For(CaseMode mode)
    {
        switch (mode)
        {
            case CaseMode.Same:
                return Strings.CaseModeSame;
            case CaseMode.Lower:
                return Strings.CaseModeLower;
            case CaseMode.Upper:
                return Strings.CaseModeUpper;
            case CaseMode.Title:
                return Strings.CaseModeTitle;
            case CaseMode.Sentence:
                return Strings.CaseModeSentence;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown case mode.");
        }
    }
}
