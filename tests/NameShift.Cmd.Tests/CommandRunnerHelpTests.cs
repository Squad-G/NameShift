using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The help, the version, the usage errors and the exit code table of the command line. None of them lists,
/// plans or renames.</summary>
public sealed class CommandRunnerHelpTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private static string[] Args(string joined) => joined.Split('|');

    private static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    private static string[] TextLines(string text) => text.Replace("\r", "").Split('\n');

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    [InlineData("help")]
    [InlineData("HELP")]
    [InlineData("help|apply")]
    [InlineData("--help|x")]
    public void Help_FirstArgument_Exit0_RestIgnored(string joined)
    {
        var renamer = new FakeBatchRenamer();
        var files = new CountingFileListSource();

        var run = Cmd.Run(renamer, _temp.Files, Args(joined), files);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(HelpText.Main().ToArray()), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(0, files.Calls);
    }

    [Theory]
    [InlineData("apply|--help")]
    [InlineData(@"apply|C:\x|-h")]
    [InlineData("apply|--case|nope|/?")]
    [InlineData("apply|--replace|a|--help")]
    public void ApplyHelp_Anywhere_WinsOverErrors(string joined)
    {
        var renamer = new FakeBatchRenamer();
        var files = new CountingFileListSource();

        var run = Cmd.Run(renamer, _temp.Files, Args(joined), files);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(HelpText.Apply().ToArray()), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(0, files.Calls);
    }

    [Theory]
    [InlineData("undo|--help")]
    [InlineData(@"undo|C:\x|-h")]
    [InlineData("undo|--dry-run|--dry-run|/?")]
    [InlineData("UNDO|--HELP")]
    public void UndoHelp_Anywhere_Exit0(string joined)
    {
        var renamer = new FakeBatchRenamer();
        var files = new CountingFileListSource();

        var run = Cmd.Run(renamer, _temp.Files, Args(joined), files);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(HelpText.Undo().ToArray()), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.LoadCalls);
        Assert.Equal(0, renamer.RevertCalls);
        Assert.Equal(0, files.Calls);
    }

    [Fact]
    public void UndoHelp_ExactLines()
    {
        Assert.Equal(
            new[]
            {
                "Usage:",
                "  NameShiftCmd undo [--dry-run]",
                "",
                "Options of undo:",
                "  --dry-run       List the names undo would restore; change nothing. Reads the",
                "                  history only: it does not check the files.",
                "  --help, -h, /?  Show this help.",
                "Undo takes back the last batch, also one made in the window.",
                "",
                "Example of undo:",
                "  NameShiftCmd undo --dry-run",
            },
            HelpText.Undo().ToArray());
    }

    [Fact]
    public void MainHelp_OneUsageHeader_BothUsageLines()
    {
        string[] main = HelpText.Main().ToArray();

        Assert.Single(main, line => line == "Usage:");
        int header = Array.IndexOf(main, "Usage:");
        Assert.Equal("  NameShiftCmd apply <folder> <options>", main[header + 1]);
        Assert.Equal("  NameShiftCmd undo [--dry-run]", main[header + 2]);
        Assert.Contains("  undo             Take back the last batch (also one made in the window).", main);
        Assert.Contains("  NameShiftCmd undo --dry-run", main);
    }

    [Fact]
    public void UndoHelp_TurkishCulture_StillFindsTheVerb()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo turkish = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkish;
            CultureInfo.CurrentUICulture = turkish;

            Assert.Equal(CommandKind.UndoHelp, CommandLineParser.Parse(Args("undo|--HELP")).Kind);
            ParsedCommand dry = CommandLineParser.Parse(Args("UNDO|--DRY-RUN"));
            Assert.Equal(CommandKind.Undo, dry.Kind);
            Assert.True(dry.UndoDryRun);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Fact]
    public void ApplyHelp_HasItsUsageOptionsAndOneExample()
    {
        string[] lines = HelpText.Apply().ToArray();

        Assert.Equal(TextLines(Strings.HelpUsageApply)[0], lines[0]);
        foreach (string word in CommandLineParser.ApplyOptionWords)
        {
            Assert.Contains(lines, line => line.Contains(word));
        }

        Assert.Single(lines, line => line.StartsWith("  NameShiftCmd apply ", StringComparison.Ordinal) && line.Contains("--dry-run"));
    }

    [Fact]
    public void ApplyHelp_ListsQuietAndVerbose()
    {
        string[] quiet = { "  --quiet, -q              Print nothing on success; errors are still shown." };
        string[] verbose = { "  --verbose, -v            Also list each renamed file and each unchanged file." };

        foreach (string[] lines in new[] { HelpText.Apply().ToArray(), HelpText.Main().ToArray() })
        {
            Assert.Single(lines, line => line == quiet[0]);
            Assert.Single(lines, line => line == verbose[0]);
        }
    }

    [Fact]
    public void ApplyHelp_HelpWordAfterApply_IsAFolder()
    {
        Directory.CreateDirectory(Path.Combine(_temp.Files, "help"));
        File.WriteAllText(Path.Combine(_temp.Files, "help", "A.TXT"), "x");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "help", "--case", "lower", "--dry-run" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("A.TXT -> a.TXT"), run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--version|apply|x")]
    public void Version_PrintsNameShiftAndVersion(string joined)
    {
        var renamer = new FakeBatchRenamer();
        var files = new CountingFileListSource();

        var run = Cmd.Run(renamer, _temp.Files, Args(joined), files);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("NameShift 1.2.3"), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(0, files.Calls);
    }

    [Fact]
    public void Version_AfterApply_IsAnUnknownOption()
    {
        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, new[] { "apply", _temp.Files, "--version" });

        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(Format(Strings.UnknownOption, "--version") + " " + Strings.Usage + " " + Strings.HelpPointer), run.Error);
    }

    [Theory]
    [MemberData(nameof(CommandLineParserTests.UsageCases), MemberType = typeof(CommandLineParserTests))]
    public void UsageError_OneLine_WithPointer(string joined, string key, string? value0, string? value1)
    {
        var renamer = new FakeBatchRenamer();
        object[] values = new[] { value0, value1 }.Where(v => v != null).Cast<object>().ToArray();
        string message = Format((string)typeof(Strings).GetProperty(key)!.GetValue(null)!, values);

        var run = Cmd.Run(renamer, _temp.Files, Args(joined));

        Assert.Equal(1, run.Code);
        Assert.Equal(new[] { message + " " + Strings.Usage + " " + Strings.HelpPointer }, Cmd.Lines(run.Error));
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void NoArguments_IsUsageError()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, Array.Empty<string>());

        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(Strings.NoCommand + " " + Strings.Usage + " " + Strings.HelpPointer), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void QuoteInFolder_KeepsItsMessage()
    {
        string folder = "D:\\Pics\" --case lower";

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, new[] { "apply", folder });

        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(Format(Strings.QuoteInFolder, folder)), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void HelpLines_AtMost79_AsciiOnly()
    {
        foreach (string line in HelpText.Main().Concat(HelpText.Apply()).Concat(HelpText.Undo()))
        {
            Assert.True(line.Length <= 79, line.Length + " characters: " + line);
            Assert.All(line, c => Assert.InRange((int)c, 0x20, 0x7E));
        }
    }

    [Fact]
    public void HelpTexts_AreInTheComposedHelps()
    {
        string[] main = HelpText.Main().ToArray();
        string[] apply = HelpText.Apply().ToArray();
        string[] undo = HelpText.Undo().ToArray();

        foreach (string text in new[] { Strings.HelpUsageApply, Strings.HelpUsageUndo, Strings.HelpMain, Strings.HelpApply, Strings.HelpUndo })
        {
            Assert.All(TextLines(text), line => Assert.Contains(line, main));
        }

        Assert.All(TextLines(Strings.HelpUsageUndo), line => Assert.Contains(line, undo));
        Assert.All(TextLines(Strings.HelpUndo), line => Assert.Contains(line, undo));

        Assert.All(TextLines(Strings.HelpUsageApply), line => Assert.Contains(line, apply));
        Assert.All(TextLines(Strings.HelpApply), line => Assert.Contains(line, apply));
    }

    [Fact]
    public void EveryWord_IsInTheHelp()
    {
        string[] mainTokens = Tokens(Strings.HelpMain);
        string[] applyTokens = Tokens(Strings.HelpApply);
        string[] undoTokens = Tokens(Strings.HelpUndo);

        Assert.All(CommandLineParser.GlobalWords, word => Assert.Contains(word, mainTokens));
        Assert.All(CommandLineParser.ApplyOptionWords, word => Assert.Contains(word, applyTokens));
        Assert.All(CommandLineParser.UndoOptionWords, word => Assert.Contains(word, undoTokens));
    }

    /// <summary>The whole words of a help text: lines split on spaces and commas, compared ordinally.</summary>
    private static string[] Tokens(string text) =>
        text.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void EveryExitCode_HasItsLine()
    {
        string[] main = HelpText.Main().ToArray();

        foreach (ExitCode code in Enum.GetValues(typeof(ExitCode)))
        {
            // The text comes from Strings by the code's number, not from the code under test.
            string text = (string)typeof(Strings).GetProperty("ExitCode" + (int)code)!.GetValue(null)!;
            Assert.NotEmpty(text);
            Assert.Contains("  " + (int)code + "  " + text, main);
        }

        Assert.Equal(5, Enum.GetValues(typeof(ExitCode)).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => HelpText.ExitCodeText((ExitCode)9));
        Assert.Contains(Strings.HelpExitCodes, main);
    }

    /// <summary>Counts the listings: help and version must never list a folder.</summary>
    private sealed class CountingFileListSource : IFileListSource
    {
        public int Calls { get; private set; }

        public FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
        {
            Calls++;
            return FileListing.Ok(Array.Empty<RenameItem>());
        }
    }
}
