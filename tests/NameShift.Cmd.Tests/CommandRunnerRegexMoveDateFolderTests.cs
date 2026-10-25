using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The RegEx, Move or copy, Auto date and Folder name options of <c>apply</c> through the runner, on real temp
/// files (dry runs use a fake renamer).</summary>
public sealed class CommandRunnerRegexMoveDateFolderTests : IDisposable
{
    private static readonly DateTime Written = new DateTime(2024, 3, 9, 14, 5, 7, DateTimeKind.Local);
    private static readonly DateTime Created = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Local);

    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private string[] DryRunIn(string folder, IEnumerable<string> ruleArgs)
    {
        var args = new List<string> { "apply", folder, "--dry-run" };
        args.AddRange(ruleArgs);

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, args);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        return Cmd.Lines(run.Output);
    }

    private string[] DryRun(params string[] ruleArgs) => DryRunIn(_temp.Files, ruleArgs);

    private string[] DryRunOne(string fileName, params string[] ruleArgs)
    {
        string path = _temp.File(fileName);
        string[] lines = DryRun(ruleArgs);
        File.Delete(path);
        return lines;
    }

    [Theory]
    [InlineData("abcdef.txt", "defabc.txt", "--move|3")]
    [InlineData("abcdef.txt", "abcdefabc.txt", "--copy|3")]
    [InlineData("abcdef.txt", "defabc.txt", "--move|3|--move-from|end|--move-to|start")]
    [InlineData("abcdef.txt", "cabdef.txt", "--move|2|--move-to|position|--move-position|2")]
    [InlineData("abcdef.txt", "defabc.txt", "--move|3|--move-to|end")]
    public void MoveCopyExamples(string name, string expected, string options)
    {
        Assert.Equal(new[] { name + " -> " + expected }, DryRunOne(name, options.Split('|')));
    }

    [Fact]
    public void Regex_Example()
    {
        Assert.Equal(new[] { "IMG_001.JPG -> IMG_[001].JPG" }, DryRunOne("IMG_001.JPG", "--regex", @"(\d+)", "[$1]"));
    }

    [Fact]
    public void Regex_LeavesTheExtension_UnlessAsked()
    {
        Assert.Equal(new[] { "a1.t1 -> a_.t1" }, DryRunOne("a1.t1", "--regex", @"\d", "_"));
        Assert.Equal(new[] { "a1.t1 -> a_.t_" }, DryRunOne("a1.t1", "--regex", @"\d", "_", "--regex-extension"));
    }

    [Fact]
    public void Regex_IgnoreCase()
    {
        Assert.Equal(new[] { "ABC.txt -> X.txt" }, DryRunOne("ABC.txt", "--regex", "abc", "X", "--regex-ignore-case"));
        Assert.Equal(new[] { Strings.NothingToRename }, DryRunOne("ABC.txt", "--regex", "abc", "X"));
    }

    [Fact]
    public void AutoDate_Examples()
    {
        string path = _temp.File("a.txt");
        File.SetCreationTime(path, Created);
        File.SetLastWriteTime(path, Written);
        File.SetCreationTime(path, Created);

        Assert.Equal(new[] { "a.txt -> 2024-03-09_a.txt" }, DryRun("--date"));
        Assert.Equal(new[] { "a.txt -> a_20240309.txt" },
            DryRun("--date-placement", "suffix", "--date-format", "yyyyMMdd"));
        Assert.Equal(new[] { "a.txt -> 2023-01-02_a.txt" }, DryRun("--date-source", "created"));
        Assert.Equal(new[] { "a.txt -> 2024-03-09-a.txt" }, DryRun("--date-separator", "-"));
    }

    [Fact]
    public void FolderName_Examples()
    {
        string trip = Path.Combine(_temp.Files, "Trip");
        Directory.CreateDirectory(trip);
        File.WriteAllText(Path.Combine(trip, "a.txt"), "content");

        Assert.Equal(new[] { "a.txt -> Trip_a.txt" }, DryRunIn(trip, new[] { "--folder-name" }));
        Assert.Equal(new[] { "a.txt -> a_Trip.txt" }, DryRunIn(trip, new[] { "--folder-placement", "suffix" }));
        Assert.Equal(new[] { "a.txt -> Trip-a.txt" }, DryRunIn(trip, new[] { "--folder-separator", "-" }));
        Assert.Equal(new[] { "a.txt -> files_Trip_a.txt" }, DryRunIn(trip, new[] { "--folder-levels", "2" }));
    }

    [Fact]
    public void DateFormat_GivingAnInvalidName_IsRefused_Exit2()
    {
        _temp.File("a.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--date-format", "yyyy:MM" });

        Assert.Equal(2, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(new[] { "a.txt" }, _temp.FileNames());
    }

    [Theory]
    [InlineData("--regex|a")]
    [InlineData("--regex|(|x")]
    [InlineData("--move|0")]
    [InlineData("--move-to|end")]
    [InlineData("--move|1|--move-to|position")]
    [InlineData("--date-format|")]
    [InlineData("--folder-levels|0")]
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
        string path = _temp.File("abc.txt");
        File.SetLastWriteTime(path, Written);
        string other = _temp.File("xbc.txt");
        File.SetLastWriteTime(other, Written);

        string[] one = DryRun("--regex", "a", "b", "--move", "1", "--date", "--folder-name", "--suffix", "_x");
        string[] two = DryRun("--suffix", "_x", "--folder-name", "--date", "--move", "1", "--regex", "a", "b");
        string[] three = DryRun("--date", "--regex", "a", "b", "--suffix", "_x", "--move", "1", "--folder-name");

        Assert.Equal(2, one.Length);
        Assert.Equal(one, two);
        Assert.Equal(one, three);
    }

    [Fact]
    public void AllElevenPanels_RulesInStageOrder()
    {
        string[] units =
        {
            "--regex|a|b", "--name-reverse", "--replace|x|y", "--case|lower", "--remove-digits", "--move|1",
            "--prefix|p", "--date", "--number", "--folder-name", "--ext-upper",
        };
        string[][] orders =
        {
            new[] { units[5], units[10], units[2], units[0], units[8], units[3], units[9], units[1], units[7], units[4], units[6] },
            units.Reverse().ToArray(),
            new[] { units[7], units[3], units[10], units[0], units[6], units[9], units[2], units[5], units[1], units[8], units[4] },
        };
        RuleStage[] expected = Enum.GetValues(typeof(RuleStage)).Cast<RuleStage>().OrderBy(s => (int)s).ToArray();

        foreach (string[] order in orders)
        {
            var args = new List<string> { "apply", @"D:\P" };
            args.AddRange(string.Join("|", order).Split('|'));
            ParsedCommand command = CommandLineParser.Parse(args);
            Assert.True(command.Kind == CommandKind.Apply, command.Error);
            ApplyOptions options = command.Options!;

            RuleStage[] stages = options.BuildRules().Select(r => r.Stage).ToArray();

            Assert.Equal(11, stages.Length);
            Assert.Equal(expected, stages);
            Assert.Equal(9, options.Panels.Given.Count);
        }
    }
}
