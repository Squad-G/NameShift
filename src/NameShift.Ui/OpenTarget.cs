using System;
using System.Globalization;
using NameShift.Ui.Folders;

namespace NameShift.Ui;

/// <summary>
/// Reads a path given on the command line or dropped on the window. Results are identical on .NET Framework and .NET;
/// the only disk access is one <see cref="IFolderSource.CheckFolder"/>.
/// </summary>
public static class OpenTarget
{
    /// <summary>The first argument that is not empty, white space or an option (starts with <c>-</c>), as given; else null.</summary>
    /// <exception cref="ArgumentNullException">args is null.</exception>
    public static string? FirstPathArgument(string[] args)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.Trim().StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            return arg;
        }

        return null;
    }

    /// <summary>
    /// What to open for <paramref name="text"/>; relative paths are joined to <paramref name="currentDirectory"/>.
    /// May block on the disk (one CheckFolder): call it off the UI thread. Never throws for any text.
    /// </summary>
    /// <exception cref="ArgumentNullException">currentDirectory or source is null.</exception>
    public static OpenRequest Resolve(string? text, string currentDirectory, IFolderSource source)
    {
        if (currentDirectory == null)
        {
            throw new ArgumentNullException(nameof(currentDirectory));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // (a) No text.
        if (text == null)
        {
            return OpenRequest.Nothing;
        }

        // (b) Trim, and remove one pair of surrounding quotes.
        string t = text.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"')
        {
            t = t.Substring(1, t.Length - 2).Trim();
        }

        // (c) "D:\My Pics\" arrives as D:\My Pics": cut at the first quote left.
        int q = t.IndexOf('"');
        if (q >= 0)
        {
            t = t.Substring(0, q).Trim();
        }

        // (d) Nothing left.
        if (t.Length == 0)
        {
            return OpenRequest.Nothing;
        }

        // (e) Slashes first, so //./C/x is caught as a device path; all before Path sees the text.
        t = t.Replace('/', '\\');
        if (PathInput.HasForbiddenCharacter(t)
            || t.StartsWith(@"\\?\", StringComparison.Ordinal)
            || t.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return OpenRequest.ForMessage(Strings.PathInvalid);
        }

        // (f) Nothing more for slashes: done at the start of (e).
        string full;
        if (PathInput.IsFullyQualified(t))
        {
            // (g) Fully qualified: normalised as the path bar does, without expanding %VAR%.
            if (PathInput.TryNormalize(t, out full, expandVariables: false) != PathInputResult.Ok)
            {
                return OpenRequest.ForMessage(Strings.PathInvalid);
            }
        }
        else
        {
            // (h) Relative. Root-relative (\Pics) and drive-relative (D:Pics) depend on state the window does not track.
            if (t[0] == '\\' || (t.Length >= 2 && t[1] == ':'))
            {
                return OpenRequest.ForMessage(Strings.PathInvalid);
            }

            string current = currentDirectory.Trim();
            if (!PathInput.IsFullyQualified(current))
            {
                return OpenRequest.ForMessage(Strings.PathInvalid);
            }

            string combined = current.TrimEnd('\\') + "\\" + t;
            if (PathInput.TryNormalize(combined, out full, expandVariables: false) != PathInputResult.Ok)
            {
                return OpenRequest.ForMessage(Strings.PathInvalid);
            }
        }

        // (i) One disk check.
        switch (source.CheckFolder(full))
        {
            case FolderCheck.Folder:
                return OpenRequest.ForFolder(full);
            case FolderCheck.File:
                int slash = full.LastIndexOf('\\');
                string parent = full.Substring(0, slash);
                if (parent.Length == 2 && parent[1] == ':')
                {
                    parent += "\\";
                }

                return OpenRequest.ForFile(parent, full.Substring(slash + 1));
            default:
                // The normalised path on purpose: it shows what was looked for.
                return OpenRequest.ForMessage(string.Format(CultureInfo.CurrentCulture, Strings.PathNotFound, full));
        }
    }
}
