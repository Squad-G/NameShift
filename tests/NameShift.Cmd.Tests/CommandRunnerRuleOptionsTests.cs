using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The Add and Numbering options of <c>apply</c> through the runner: names, errors and the typing order.
/// Dry runs use a fake renamer; the one real run uses temp folders and the temp history root.</summary>
public sealed class CommandRunnerRuleOptionsTests : IDisposable
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

    private void TwoFiles()
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
    }

    [Fact]
    public void Prefix_And_Suffix_KeepTheExtension()
    {
        _temp.File("IMG_001.JPG");

        Assert.Equal(new[] { "IMG_001.JPG -> Trip_IMG_001.JPG" }, DryRun("--prefix", "Trip_"));
        Assert.Equal(new[] { "IMG_001.JPG -> IMG_001_v2.JPG" }, DryRun("--suffix", "_v2"));
    }

    [Fact]
    public void Number_Defaults()
    {
        TwoFiles();

        Assert.Equal(new[] { "a.txt -> a_1.txt", "b.txt -> b_2.txt" }, DryRun("--number"));
    }

    [Fact]
    public void Number_PrefixPad3()
    {
        TwoFiles();

        Assert.Equal(new[] { "a.txt -> 001_a.txt", "b.txt -> 002_b.txt" }, DryRun("--number-mode", "prefix", "--number-pad", "3"));
    }

    [Fact]
    public void Number_Roman()
    {
        TwoFiles();

        Assert.Equal(new[] { "a.txt -> a_I.txt", "b.txt -> b_II.txt" }, DryRun("--number-system", "roman"));
    }

    [Fact]
    public void Number_HexLowerBeforeSystem()
    {
        TwoFiles();

        Assert.Equal(
            new[] { "a.txt -> a_a.txt", "b.txt -> b_b.txt" },
            DryRun("--number-case", "lower", "--number-system", "hex", "--number-start", "a"));
    }

    [Fact]
    public void Number_RomanLowerBeforeSystem()
    {
        TwoFiles();

        Assert.Equal(
            new[] { "a.txt -> a_iv.txt", "b.txt -> b_v.txt" },
            DryRun("--number-case", "lower", "--number-system", "roman", "--number-start", "iv"));
    }

    [Fact]
    public void UsageError_RenamesNothing()
    {
        TwoFiles();

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--prefix", "");

        Assert.Equal(1, run.Code);
        Assert.Equal(new[] { Strings.EmptyPrefix + " " + Strings.Usage + " " + Strings.HelpPointer }, Cmd.Lines(run.Error));
        Assert.Equal("", run.Output);
        Assert.Equal(new[] { "a.txt", "b.txt" }, _temp.FileNames());
        Assert.False(Directory.Exists(_temp.History));
    }

    [Theory]
    [InlineData("--prefix|")]
    [InlineData("--insert|X|0")]
    [InlineData("--number-start|x")]
    [InlineData("--number-system|octal")]
    public void NewUsageErrors_AreOneLine_AndRenameNothing(string options)
    {
        TwoFiles();
        var renamer = new FakeBatchRenamer();
        var args = new List<string> { "apply", _temp.Files };
        args.AddRange(options.Split('|'));

        var run = Cmd.Run(renamer, _temp.Files, args);

        Assert.Equal(1, run.Code);
        string[] lines = Cmd.Lines(run.Error);
        Assert.Single(lines);
        Assert.EndsWith(Strings.Usage + " " + Strings.HelpPointer, lines[0], StringComparison.Ordinal);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void ThreeOrders_SameNames()
    {
        _temp.File("ax.txt");
        _temp.File("bx.txt");

        string[] one = DryRun("--replace", "x", "y", "--prefix", "p_", "--number", "--case", "upper");
        string[] two = DryRun("--number", "--case", "upper", "--prefix", "p_", "--replace", "x", "y");
        string[] three = DryRun("--case", "upper", "--replace", "x", "y", "--number", "--prefix", "p_");

        Assert.Equal(2, one.Length);
        Assert.Equal(one, two);
        Assert.Equal(one, three);
    }

    [Fact]
    public async Task Insert_FromEnd()
    {
        _temp.File("abcdef.txt");
        var files = new FileListModel(new DiskFileListSource());
        var add = new AddPanelModel { Insert = "X", InsertPositionText = "2", InsertFromEnd = true, Enabled = true };
        var preview = new PreviewModel(new RulePanelHostModel(new IRulePanelModel[] { add }), files);
        await files.LoadAsync(_temp.Files);
        await preview.Pending;
        string expected = "abcdef.txt -> " + preview.CellForRow(0).Text;

        Assert.Equal(new[] { expected }, DryRun("--insert", "X", "2", "--from-end"));
        Assert.NotEqual("abcdef.txt -> abcdef.txt", expected);
    }
}
