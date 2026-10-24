using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public void CliSourceNeverRenamesOrDeletesDirectly(string pattern)
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
                if (File.ReadAllText(file).Contains(pattern))
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
