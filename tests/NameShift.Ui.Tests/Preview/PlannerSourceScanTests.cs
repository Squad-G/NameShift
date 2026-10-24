using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

/// <summary>The three checks are named in one place only: the planner.</summary>
public sealed class PlannerSourceScanTests
{
    private const string PlannerPath = "src/NameShift.Ui/Preview/RenamePlanner.cs";

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

    private static string Relative(string root, string path) =>
        path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');

    [Fact]
    public void PreviewModel_NamesNoCheck()
    {
        string path = Path.Combine(FindRoot(), "src", "NameShift.Ui", "Preview", "PreviewModel.cs");
        Assert.True(File.Exists(path), path);

        string text = File.ReadAllText(path);

        foreach (string name in new[] { "InvalidNameCheck", "DuplicateTargetCheck", "PathLengthCheck", "ConflictChecker" })
        {
            Assert.False(text.Contains(name), "PreviewModel.cs names " + name);
        }
    }

    // The patterns are literal: a spacing variant ("new PathLengthCheck ()") or a target-typed new() would slip past.
    // A guard against drift, not a proof; the review catches the rest.
    [Theory]
    [InlineData("new InvalidNameCheck(")]
    [InlineData("new DuplicateTargetCheck(")]
    [InlineData("new PathLengthCheck(")]
    [InlineData("ConflictChecker.Run(")]
    public void ChecksAreBuiltOnlyInThePlanner(string pattern)
    {
        string root = FindRoot();
        string coreFolder = "src/NameShift.Core/";
        var scanned = new List<string>();
        var found = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string relative = Relative(root, file);
            string[] segments = relative.Split('/');
            if (relative.StartsWith(coreFolder, StringComparison.Ordinal) || segments.Contains("bin") || segments.Contains("obj"))
            {
                continue;
            }

            scanned.Add(relative);
            if (File.ReadAllText(file).Contains(pattern))
            {
                found.Add(relative);
            }
        }

        Assert.True(scanned.Count > 20, "Only " + scanned.Count + " files were scanned under " + root);
        Assert.Equal(new[] { PlannerPath }, found.ToArray());
    }
}
