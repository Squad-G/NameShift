using System;

namespace NameShift.Cmd;

/// <summary>The result of reading a command line: what it asks for, with the options of <c>apply</c>, the
/// <c>--dry-run</c> of <c>undo</c>, or the one-line error. Immutable.</summary>
internal sealed class ParsedCommand
{
    private readonly ApplyOptions? _options;
    private readonly string? _error;

    private ParsedCommand(CommandKind kind, ApplyOptions? options = null, string? error = null, bool undoDryRun = false)
    {
        Kind = kind;
        UndoDryRun = undoDryRun;
        _options = options;
        _error = error;
    }

    /// <summary>What the command line asks for.</summary>
    public CommandKind Kind { get; }

    /// <summary>The options of <c>apply</c>; null for every other kind.</summary>
    public ApplyOptions? Options => _options;

    /// <summary>True for <c>undo --dry-run</c>; false for every other kind.</summary>
    public bool UndoDryRun { get; }

    /// <summary>The usage error as one line (<c>message usage pointer</c>); empty for every other kind.</summary>
    public string Error => _error ?? "";

    /// <summary>The main help.</summary>
    public static ParsedCommand Help() => new ParsedCommand(CommandKind.Help);

    /// <summary>The help of <c>apply</c>.</summary>
    public static ParsedCommand ApplyHelp() => new ParsedCommand(CommandKind.ApplyHelp);

    /// <summary>The help of <c>undo</c>.</summary>
    public static ParsedCommand UndoHelp() => new ParsedCommand(CommandKind.UndoHelp);

    /// <summary>The version line.</summary>
    public static ParsedCommand Version() => new ParsedCommand(CommandKind.Version);

    /// <summary><c>apply</c> with its options.</summary>
    public static ParsedCommand Apply(ApplyOptions options) =>
        new ParsedCommand(CommandKind.Apply, options ?? throw new ArgumentNullException(nameof(options)));

    /// <summary><c>undo</c>, listing only when <paramref name="dryRun"/> is set.</summary>
    public static ParsedCommand Undo(bool dryRun) => new ParsedCommand(CommandKind.Undo, undoDryRun: dryRun);

    /// <summary>A usage error; <paramref name="line"/> is the whole line for the error stream.</summary>
    public static ParsedCommand Failed(string line) =>
        new ParsedCommand(CommandKind.Error, error: line ?? throw new ArgumentNullException(nameof(line)));
}
