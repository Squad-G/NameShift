using System;
using System.IO;
using System.Text;

namespace NameShift.Cmd;

/// <summary>Sets the console up for UTF-8 around one run of the command.</summary>
internal static class ConsoleEntry
{
    /// <summary>Sets UTF-8 without a BOM first, then takes the writers (a writer taken earlier keeps the old code page
    /// and writes ?), runs, and restores the previous encoding in a finally. An <see cref="IOException"/> (no console)
    /// is ignored on the read, the set and the restore; any other exception propagates.</summary>
    /// <returns>The exit code <paramref name="run"/> returned.</returns>
    internal static int Run(IConsoleEncoding console, Func<TextWriter> output, Func<TextWriter> error,
        Func<TextWriter, TextWriter, int> run)
    {
        if (console == null)
        {
            throw new ArgumentNullException(nameof(console));
        }

        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (error == null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        if (run == null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        Encoding? previous = null;
        try
        {
            previous = console.OutputEncoding;
        }
        catch (IOException)
        {
            // No console attached: nothing to restore.
        }

        try
        {
            try
            {
                console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // No console attached: the run goes on with what the streams have.
            }

            return run(output(), error());
        }
        finally
        {
            if (previous != null)
            {
                try
                {
                    console.OutputEncoding = previous;
                }
                catch (IOException)
                {
                    // No console attached.
                }
            }
        }
    }
}
