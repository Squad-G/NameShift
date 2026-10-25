using System;
using System.IO;
using System.Linq;

namespace NameShift.Ui.Tests;

/// <summary>Where the repository is, for the tests that read its files.</summary>
internal static class RepositoryFiles
{
    /// <summary>The folder holding NameShift.slnx, found above the test binaries: the working tree the test runs from.</summary>
    public static string Root()
    {
        string start = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NameShift.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("NameShift.slnx not found above " + start);
    }

    public static bool IsBuildOutput(string root, string path)
    {
        string[] parts = path.Substring(root.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part == "bin" || part == "obj");
    }
}
