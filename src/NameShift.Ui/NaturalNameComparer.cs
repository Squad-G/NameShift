using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NameShift.Ui;

/// <summary>Explorer's name order (file2 before file10, case-insensitive), via StrCmpLogicalW.</summary>
public sealed class NaturalNameComparer : IComparer<string>
{
    public static NaturalNameComparer Instance { get; } = new NaturalNameComparer();

    public int Compare(string? x, string? y)
    {
        if (x is null)
        {
            return y is null ? 0 : -1;
        }

        if (y is null)
        {
            return 1;
        }

        return StrCmpLogicalW(x, y);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string x, string y);
}
