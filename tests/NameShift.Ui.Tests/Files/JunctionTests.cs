using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-74 AC 8 on the real disk: a junction in a temp tree is listed, never followed. The junction is always
/// removed on its own (never recursively, which would delete its target's files) before the tree is deleted.</summary>
public sealed class JunctionTests
{
    [Fact]
    public void RealJunction_NotFollowed()
    {
        string root = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "target");
        string sub = Path.Combine(root, "sub");
        string link = Path.Combine(sub, "link");
        string inside = Path.Combine(target, "inside.txt");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(sub);
        File.WriteAllText(inside, "keep me");
        try
        {
            MakeJunction(link, target);

            FileListing listing = new DiskFileListSource().ListEntries(root, true, CancellationToken.None);

            Assert.Null(listing.Problem);
            Assert.Equal(0, listing.SkippedFolders);
            RenameItem only = Assert.Single(listing.Items, item => item.Name == "inside.txt");
            Assert.Equal(target, only.FolderPath);
            RenameItem linkItem = Assert.Single(listing.Items, item => item.Name == "link");
            Assert.Equal(ItemKind.Folder, linkItem.Kind);
            Assert.Equal(sub, linkItem.FolderPath);
            Assert.DoesNotContain(listing.Items, item => item.FolderPath.StartsWith(link, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // The junction first, on its own: a recursive delete through it would delete the target's files.
            if (Directory.Exists(link))
            {
                Directory.Delete(link, false);
            }

            Assert.True(File.Exists(inside), "The junction's target lost its file.");
            Directory.Delete(root, true);
        }
    }

    /// <summary>A junction made by <c>cmd /c mklink /J</c> (no administrator rights needed). Fails, with the command's
    /// output, when it could not be made: never skips.</summary>
    private static void MakeJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(10000), "mklink did not finish in 10 s.");
        string said = output.Result + error.Result;
        Assert.True(process.ExitCode == 0, "mklink failed (" + process.ExitCode + "): " + said);
        Assert.True(Directory.Exists(link), "No junction after mklink: " + said);
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, "The link is not a reparse point: " + said);
        Assert.True(Directory.EnumerateFileSystemEntries(link).Any(),"The junction does not show its target: " + said);
    }
}
