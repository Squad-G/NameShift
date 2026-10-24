using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The command line's sources rename only through the journaled renamer and never ask anything. A guard
/// against drift on literal patterns, not a proof; the review catches the rest.</summary>
public sealed class CmdSourceScanTests
{
    private static string FindRoot()
    {
        string start = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NameShift.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("NameShift.slnx was not found above " + start);
    }

    [Theory]
    [InlineData("RenameExecutor")]
    [InlineData("DiskFileSystem")]
    [InlineData("File.Move")]
    [InlineData("Directory.Move")]
    [InlineData(".MoveTo(")]
    [InlineData("File.Delete")]
    [InlineData("Directory.Delete")]
    [InlineData("Console.Read")]
    [InlineData("MessageBox")]
    [InlineData("System.Windows.Forms")]
    [InlineData("File.Replace")]
    [InlineData("Console.In")]
    public void CliSourceNeverRenamesOrDeletesDirectly(string pattern)
    {
        AssertNoSourceMatches(text => text.Contains(pattern));
    }

    // Calls a literal pattern misses: new FileInfo(x).Delete(), or `using static System.IO.File;` then Move(a, b).
    // A name that merely ends in the word (Remove(, IsMoved() is not a match.
    [Theory]
    [InlineData(@"(?<![\w])Delete\s*\(")]
    [InlineData(@"(?<![\w])Move(To)?\s*\(")]
    [InlineData(@"using\s+static\s+System\.IO")]
    public void CliSourceCallsNoMoveOrDelete(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        AssertNoSourceMatches(text => regex.IsMatch(text));
    }

    [Theory]
    [InlineData("new FileInfo(path).Delete();", true)]
    [InlineData("Move(a, b);", true)]
    [InlineData("x.MoveTo (b);", true)]
    [InlineData("using static System.IO.File;", true)]
    [InlineData("list.Remove(a); RemoveAll(b); IsMoved();", false)]
    public void MoveOrDeletePatterns_MatchCallsOnly(string code, bool expected)
    {
        string[] patterns = { @"(?<![\w])Delete\s*\(", @"(?<![\w])Move(To)?\s*\(", @"using\s+static\s+System\.IO" };

        Assert.Equal(expected, patterns.Any(p => Regex.IsMatch(code, p, RegexOptions.CultureInvariant)));
    }

    private static void AssertNoSourceMatches(Func<string, bool> matches)
    {
        string root = FindRoot();
        string[] projects = Directory.GetDirectories(Path.Combine(root, "src"), "NameShift.Cmd*");
        var scanned = new List<string>();
        var found = new List<string>();
        foreach (string project in projects)
        {
            foreach (string file in Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                string[] segments = file.Substring(project.Length).Split('\\', '/');
                if (segments.Contains("bin") || segments.Contains("obj"))
                {
                    continue;
                }

                scanned.Add(file);
                if (matches(File.ReadAllText(file)))
                {
                    found.Add(file);
                }
            }
        }

        Assert.True(projects.Length >= 2, "Only " + projects.Length + " NameShift.Cmd* project folders under " + root);
        Assert.True(scanned.Count >= 5, "Only " + scanned.Count + " files were scanned under " + root);
        Assert.Empty(found);
    }

    [Fact]
    public void ExeUsesTheWindowsJournal()
    {
        string path = Path.Combine(FindRoot(), "src", "NameShift.Cmd.Exe", "Program.cs");
        Assert.True(File.Exists(path), path);

        string text = File.ReadAllText(path);

        Assert.Contains("JournaledRenamer.DefaultHistoryRoot", text, StringComparison.Ordinal);
        Assert.Contains("CommandRunner.ForDisk", text, StringComparison.Ordinal);
    }
}
