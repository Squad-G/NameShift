using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>NS-94 AC 6: no broken path literal (<c>@"C:</c> at a line's end) in the code. The broken samples here are
/// regular strings with escapes, so none of them is a broken literal on its own line.</summary>
public sealed class BrokenLiteralScanTests
{
    private static readonly string[] Roots = { "src", "tests" };

    [Theory]
    [InlineData("x = @\"C:", true)]
    [InlineData("x = @\"C:\\Tem", true)]
    [InlineData("x = $@\"D:", true)]
    [InlineData("x = @$\"D:\\a", true)]
    [InlineData("// @\"C:", true)]
    [InlineData("x = @\"C:\\x\";", false)]
    [InlineData("x = @\"C:\\x\"\" y\";", false)]
    [InlineData("x = @\"Line one", false)]
    [InlineData("x = @\"a\"\"\\\" @\"C:", true)]
    [InlineData("x = @\"Note: C:", false)]
    [InlineData("x = \"@\\\"C:\";", false)]
    [InlineData("x = '\"'; y = @\"C:\\x\";", false)]
    public void Flags(string line, bool flagged)
    {
        List<(int Line, string Reason)> found = BrokenLiteralScanner.Find(line);

        Assert.Equal(flagged, found.Count == 1);
        if (flagged)
        {
            Assert.Equal(BrokenLiteralScanner.Reason, found[0].Reason);
        }
    }

    [Fact]
    public void TwoLineText_StartingWithWords_IsNotFlagged() =>
        Assert.Empty(BrokenLiteralScanner.Find("x = @\"Line one\nline two\";"));

    [Fact]
    public void BrokenLiteral_AcrossTwoLines_FlagsTheFirst() =>
        Assert.Equal(1, BrokenLiteralScanner.Find("x = @\"C:\nowhere\";")[0].Line);

    [Fact]
    public void Lines_AreOneBased()
    {
        List<(int Line, string Reason)> found = BrokenLiteralScanner.Find("a;\r\nb;\r\nx = @\"C:\\Tem\r\nc;\r\n");

        Assert.Equal(new List<(int Line, string Reason)> { (3, BrokenLiteralScanner.Reason) }, found);
    }

    [Fact]
    public void RepositoryCode_HasNoBrokenPathLiterals()
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

            foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (RepositoryFiles.IsBuildOutput(root, path))
                {
                    continue;
                }

                scanned++;
                string relative = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                foreach ((int line, string reason) in BrokenLiteralScanner.Find(File.ReadAllText(path)))
                {
                    failures.Add(relative + ":" + line + ": " + reason);
                }
            }
        }

        Assert.True(scanned >= 100, "only " + scanned + " files scanned under " + root);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
