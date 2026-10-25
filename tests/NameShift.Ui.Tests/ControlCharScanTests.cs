using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>No control character hides in a text file of the repository. The offending texts here are built from escapes: a raw
/// byte in this file would fail the guard on itself.</summary>
public sealed class ControlCharScanTests
{
    private static readonly string[] Roots = { "src", "tests", "docs", "stories", "installer" };

    private static readonly string[] Extensions = { ".cs", ".resx", ".csproj", ".md", ".iss", ".json", ".props" };

    [Fact]
    public void Find_CleanText_Nothing() =>
        Assert.Empty(ControlCharScanner.Find("class A {}\nint b;\n"));

    [Fact]
    public void Find_TabAndLineEnds_Nothing() =>
        Assert.Empty(ControlCharScanner.Find("a\tb\r\nc\nd\r\n"));

    [Theory]
    [InlineData("ab\u0008c", 1, 0x08)]
    [InlineData("x\ny\u0007", 2, 0x07)]
    [InlineData("\u0000", 1, 0x00)]
    [InlineData("a\r\nb\r\n\u007F", 3, 0x7F)]
    public void Find_EachControlChar_ItsLine(string text, int line, int codePoint) =>
        Assert.Equal(new List<(int Line, int CodePoint)> { (line, codePoint) }, ControlCharScanner.Find(text));

    [Fact]
    public void Find_FormFeed_Found() =>
        Assert.Equal(new List<(int Line, int CodePoint)> { (1, 0x0C) }, ControlCharScanner.Find("a\u000Cb"));

    [Fact]
    public void Find_TwoOnOneLine_TwoFindings() =>
        Assert.Equal(
            new List<(int Line, int CodePoint)> { (1, 1), (1, 0x1F) },
            ControlCharScanner.Find("\u0001x\u001F"));

    [Fact]
    public void RepositoryTextFiles_HaveNoControlCharacters()
    {
        string root = RepositoryFiles.Root();
        var failures = new List<string>();
        int scanned = 0;
        foreach (string folder in Roots)
        {
            string directory = Path.Combine(root, folder);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                    || RepositoryFiles.IsBuildOutput(root, path))
                {
                    continue;
                }

                scanned++;
                string relative = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                foreach ((int line, int codePoint) in ControlCharScanner.Find(File.ReadAllText(path)))
                {
                    failures.Add(relative + ":" + line + ": U+" + codePoint.ToString("X4"));
                }
            }
        }

        Assert.True(scanned >= 100, "only " + scanned + " files scanned under " + root);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
