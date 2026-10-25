namespace NameShift.Cmd;

/// <summary>The outcome of offering one argument to a reader.</summary>
internal sealed class OptionRead
{
    private OptionRead(bool isMine, ParsedCommand? error)
    {
        IsMine = isMine;
        Error = error;
    }

    /// <summary>Not this reader's word; the index is untouched.</summary>
    public static readonly OptionRead NotMine = new OptionRead(false, null);

    /// <summary>Read; the index is on the last value consumed.</summary>
    public static readonly OptionRead Taken = new OptionRead(true, null);

    /// <summary>The reader's word, but its use is a usage error.</summary>
    public static OptionRead Failed(ParsedCommand error) => new OptionRead(true, error);

    /// <summary>True when the word was this reader's (read or failed).</summary>
    public bool IsMine { get; }

    /// <summary>The usage error, or null.</summary>
    public ParsedCommand? Error { get; }
}
