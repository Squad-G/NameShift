using System;
using System.Text;

namespace NameShift.Cmd;

/// <summary>The real console's output encoding.</summary>
internal sealed class SystemConsoleEncoding : IConsoleEncoding
{
    /// <inheritdoc />
    public Encoding OutputEncoding
    {
        get => Console.OutputEncoding;
        set => Console.OutputEncoding = value;
    }
}
