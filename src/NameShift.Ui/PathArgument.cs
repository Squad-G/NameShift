using System;

namespace NameShift.Ui;

/// <summary>One path argument and the directory its relative form is joined to (a request may come from a process
/// that started in another directory).</summary>
public sealed class PathArgument
{
    /// <exception cref="ArgumentNullException">text or currentDirectory is null.</exception>
    public PathArgument(string text, string currentDirectory)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        CurrentDirectory = currentDirectory ?? throw new ArgumentNullException(nameof(currentDirectory));
    }

    /// <summary>The argument as given (quotes and slashes are read by <see cref="OpenTarget"/>).</summary>
    public string Text { get; }

    /// <summary>The directory a relative <see cref="Text"/> is joined to.</summary>
    public string CurrentDirectory { get; }
}
