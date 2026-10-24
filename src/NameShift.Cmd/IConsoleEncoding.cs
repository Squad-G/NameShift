using System.Text;

namespace NameShift.Cmd;

/// <summary>The console's output encoding behind a seam, so a test can make it throw.</summary>
internal interface IConsoleEncoding
{
    /// <summary>The encoding the console writes with. Either side throws <see cref="System.IO.IOException"/> when no
    /// console is attached.</summary>
    Encoding OutputEncoding { get; set; }
}
