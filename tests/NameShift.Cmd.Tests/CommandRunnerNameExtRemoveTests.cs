using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The Name, Extension and Remove options of <c>apply</c> through the runner, on real temp files (dry runs use a
/// fake renamer).</summary>
public sealed class CommandRunnerNameExtRemoveTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private string[] DryRun(params string[] ruleArgs)
    {
        var args = new List<string> { "apply", _temp.Files, "--dry-run" };
        args.AddRange(ruleArgs);

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, args);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        return Cmd.Lines(run.Output);
    }

    private string[] DryRunOne(string fileName, params string[] ruleArgs)
    {
        string path = _temp.File(fileName);
        string[] lines = DryRun(ruleArgs);
        File.Delete(path);
        return lines;
    }

    [Theory]
    [InlineData("IMG_001.JPG", "Trip.JPG", "--name-fixed|Trip")]
    [InlineData("abc.txt", "cba.txt", "--name-reverse")]
    [InlineData("IMG_001.JPG", "IMG_001.jpg", "--ext-lower")]
    [InlineData("a.txt", "a.TXT", "--ext-upper")]
    [InlineData("a.jpg", "a.jpeg", "--ext-fixed|jpeg")]
    [InlineData("a.jpg", "a.jpeg", "--ext-fixed|.jpeg")]
    [InlineData("a.txt", "a.txt.bak", "--ext-extra|bak")]
    [InlineData("a.txt", "a", "--ext-remove")]
    [InlineData("README", "README.md", "--ext-fixed|md")]
    [InlineData("IMG_001.JPG", "_001.JPG", "--remove-first|3")]
    [InlineData("IMG_001.JPG", "IMG_00.JPG", "--remove-last|1")]
    [InlineData("IMG_001.JPG", "IMG_.JPG", "--remove-digits")]
    [InlineData("IMG_001.JPG", "_001.JPG", "--remove-text|img")]
    [InlineData("abcdef.txt", "adef.txt", "--remove-from|2|--remove-to|3")]
    [InlineData("Trip - a.txt", "a.txt", "--remove-before| - ")]
    [InlineData("Trip - a.txt", " - a.txt", "--remove-before| - |--remove-keep-marker")]
    public void Examples(string name, string expected, string options)
    {
        Assert.Equal(new[] { name + " -> " + expected }, DryRunOne(name, options.Split('|')));
    }

    [Fact]
    public void RemoveText_MatchCase_ChangesNothing()
    {
        _temp.File("IMG_001.JPG");

        Assert.Equal(new[] { Strings.NothingToRename }, DryRun("--remove-text", "img", "--remove-match-case"));
    }

    [Fact]
    public void FoldersAreNeverListed()
    {
        _temp.File("a.txt");
        Directory.CreateDirectory(Path.Combine(_temp.Files, "folder.txt"));

        Assert.Equal(new[] { "a.txt -> a.TXT" }, DryRun("--ext-upper"));
    }

    [Fact]
    public void NameFixed_TwoFiles_Conflict_Exit2()
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--name-fixed", "x" });

        Assert.Equal(2, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(new[] { "a.txt", "b.txt" }, _temp.FileNames());
    }

    [Theory]
    [InlineData("--name-fixed|")]
    [InlineData("--ext-fixed|..")]
    [InlineData("--remove-first|0")]
    [InlineData("--remove-whole-word")]
    [InlineData("--remove-from|3")]
    public void UsageError_RenamesNothing(string options)
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
        var renamer = new FakeBatchRenamer();
        var args = new List<string> { "apply", _temp.Files };
        args.AddRange(options.Split('|'));

        var run = Cmd.Run(renamer, _temp.Files, args);

        Assert.Equal(1, run.Code);
        Assert.Single(Cmd.Lines(run.Error));
        Assert.EndsWith(Strings.Usage + " " + Strings.HelpPointer, run.Error.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void SeveralPanels_ThreeOrders_SameNames()
    {
        _temp.File("ax1.txt");
        _temp.File("bx2.txt");

        string[] one = DryRun("--replace", "x", "y", "--name-reverse", "--ext-upper", "--remove-digits", "--prefix", "p_");
        string[] two = DryRun("--prefix", "p_", "--remove-digits", "--ext-upper", "--name-reverse", "--replace", "x", "y");
        string[] three = DryRun("--ext-upper", "--prefix", "p_", "--replace", "x", "y", "--remove-digits", "--name-reverse");

        Assert.Equal(2, one.Length);
        Assert.Equal(one, two);
        Assert.Equal(one, three);
    }
}
