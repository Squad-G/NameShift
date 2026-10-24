using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each move or copy source.</summary>
public static class MoveCopySourceText
{
    /// <exception cref="ArgumentOutOfRangeException">The source is not a defined value.</exception>
    public static string For(MoveCopySource source)
    {
        switch (source)
        {
            case MoveCopySource.Start:
                return Strings.MoveCopySourceStart;
            case MoveCopySource.End:
                return Strings.MoveCopySourceEnd;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown move or copy source.");
        }
    }
}
