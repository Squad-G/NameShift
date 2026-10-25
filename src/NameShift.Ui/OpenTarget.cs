using System;
using System.Collections.Generic;
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

    /// <summary>Every argument that is not empty, white space or an option (starts with <c>-</c>), as given, in order.</summary>
    /// <exception cref="ArgumentNullException">args is null.</exception>
    public static IReadOnlyList<string> PathArguments(string[] args)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        var result = new List<string>();
        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.Trim().StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(arg);
        }

        return result;
    }

    /// <summary>
    /// What to open for several paths: the parent of the first path that exists, and the names of the paths in that same
    /// folder, which is one disk check per path tried and none after the first that exists. A path that is a folder is an
    /// item to select, never a place to open. May block on the disk: call it off the UI thread. Never throws for any text.
    /// </summary>
    /// <exception cref="ArgumentNullException">paths or source is null.</exception>
    public static MultiOpenRequest ResolveMany(IReadOnlyList<PathArgument> paths, IFolderSource source)
    {
        if (paths == null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        string? folder = null;
        string? firstMessage = null;
        int rejected = 0;
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PathArgument path in paths)
        {
            NormalizedPath normalized = Normalize(path.Text, path.CurrentDirectory);
            if (normalized.IsBlank)
            {
                continue;
            }

            if (normalized.Message != null)
            {
                rejected++;
                firstMessage ??= normalized.Message;
                continue;
            }

            string full = normalized.FullPath!;
            string? parent = ParentOf(full);
            if (folder == null)
            {
                // A drive root has no parent to select in: no check, and it does not decide the folder.
                if (parent == null)
                {
                    rejected++;
                    continue;
                }

                if (source.CheckFolder(full) == FolderCheck.NotFound)
                {
                    rejected++;
                    firstMessage ??= NotFoundMessage(full);
                    continue;
                }

                folder = parent;
            }
            else if (parent == null || !PathInput.SameFolder(parent, folder))
            {
                rejected++;
                continue;
            }

            string name = full.Substring(full.LastIndexOf('\\') + 1);
            if (seen.Add(name))
            {
                names.Add(name);
            }
        }

        return folder == null
            ? new MultiOpenRequest(null, names, rejected, firstMessage)
            : new MultiOpenRequest(folder, names, rejected, null);
    }

    /// <summary>
    /// What to open for <paramref name="text"/>; relative paths are joined to <paramref name="currentDirectory"/>.
    /// May block on the disk (one CheckFolder): call it off the UI thread. Never throws for any text.
    /// </summary>
    /// <exception cref="ArgumentNullException">currentDirectory or source is null.</exception>
    public static OpenRequest Resolve(string? text, string currentDirectory, IFolderSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        NormalizedPath normalized = Normalize(text, currentDirectory);
        if (normalized.IsBlank)
        {
            return OpenRequest.Nothing;
        }

        if (normalized.Message != null)
        {
            return OpenRequest.ForMessage(normalized.Message);
        }

        string full = normalized.FullPath!;

        // (i) One disk check.
        switch (source.CheckFolder(full))
        {
            case FolderCheck.Folder:
                return OpenRequest.ForFolder(full);
            case FolderCheck.File:
                string? parent = ParentOf(full);
                if (parent != null)
                {
                    return OpenRequest.ForFile(parent, full.Substring(full.LastIndexOf('\\') + 1));
                }

                return NotFound(full);
            default:
                return NotFound(full);
        }
    }

    // The normalised path on purpose: it shows what was looked for.
    private static OpenRequest NotFound(string full) => OpenRequest.ForMessage(NotFoundMessage(full));

    private static string NotFoundMessage(string full) =>
        string.Format(CultureInfo.CurrentCulture, Strings.PathNotFound, full);

    /// <summary>Steps (a) to (h) of <see cref="Resolve"/>: the full path of <paramref name="text"/>, a message when it is
    /// not a usable path, or blank. No disk access. Never throws for any text.</summary>
    /// <exception cref="ArgumentNullException">currentDirectory is null.</exception>
    internal static NormalizedPath Normalize(string? text, string currentDirectory)
    {
        if (currentDirectory == null)
        {
            throw new ArgumentNullException(nameof(currentDirectory));
        }

        // (a) No text.
        if (text == null)
        {
            return NormalizedPath.Blank;
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
            return NormalizedPath.Blank;
        }

        // (e) Slashes first, so //./C/x is caught as a device path; all before Path sees the text.
        t = t.Replace('/', '\\');
        if (PathInput.HasForbiddenCharacter(t)
            || t.StartsWith(@"\\?\", StringComparison.Ordinal)
            || t.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return NormalizedPath.Invalid;
        }

        // (f) Nothing more for slashes: done at the start of (e).
        string full;
        if (PathInput.IsFullyQualified(t))
        {
            // (g) Fully qualified: normalised as the path bar does, without expanding %VAR%.
            if (PathInput.TryNormalize(t, out full, expandVariables: false) != PathInputResult.Ok)
            {
                return NormalizedPath.Invalid;
            }
        }
        else
        {
            // (h) Relative. Root-relative (\Pics) and drive-relative (D:Pics) depend on state the window does not track.
            if (t[0] == '\\' || (t.Length >= 2 && t[1] == ':'))
            {
                return NormalizedPath.Invalid;
            }

            string current = currentDirectory.Trim();
            if (!PathInput.IsFullyQualified(current))
            {
                return NormalizedPath.Invalid;
            }

            string combined = current.TrimEnd('\\') + "\\" + t;
            if (PathInput.TryNormalize(combined, out full, expandVariables: false) != PathInputResult.Ok)
            {
                return NormalizedPath.Invalid;
            }
        }

        return NormalizedPath.Ok(full);
    }

    /// <summary>The parent folder of a full path, with a drive root kept as <c>D:\</c>; null for a drive root or a
    /// network share root, which has no parent to select in.</summary>
    internal static string? ParentOf(string fullPath)
    {
        if (fullPath.Length == 3 && fullPath[1] == ':' && fullPath[2] == '\\')
        {
            return null;
        }

        int slash = fullPath.LastIndexOf('\\');
        if (slash <= 0)
        {
            return null;
        }

        string parent = fullPath.Substring(0, slash);
        if (parent.Length == 2 && parent[1] == ':')
        {
            return parent + "\\";
        }

        if (parent.Length == 0 || parent.EndsWith(":", StringComparison.Ordinal))
        {
            return null;
        }

        // \\server or \\server\share is not a folder to select in.
        if (parent.StartsWith("\\\\", StringComparison.Ordinal) && parent.IndexOf('\\', 2) < 0)
        {
            return null;
        }

        return parent;
    }
}
