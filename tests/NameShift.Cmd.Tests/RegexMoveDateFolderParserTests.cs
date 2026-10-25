using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>The RegEx, Move or copy, Auto date and Folder name options of <c>apply</c> in the parser: usage errors, the
/// models filled, the help block.</summary>
public sealed class RegexMoveDateFolderParserTests
{
    private static readonly string[] Words =
    {
        RegexReader.RegexOption, RegexReader.IgnoreCaseOption, RegexReader.ExtensionOption,
        MoveCopyReader.MoveOption, MoveCopyReader.CopyOption, MoveCopyReader.FromOption, MoveCopyReader.ToOption,
        MoveCopyReader.PositionOption, MoveCopyReader.SeparatorOption,
        AutoDateReader.DateOption, AutoDateReader.SourceOption, AutoDateReader.FormatOption,
        AutoDateReader.PlacementOption, AutoDateReader.SeparatorOption,
        FolderNameReader.FolderNameOption, FolderNameReader.LevelsOption, FolderNameReader.PlacementOption,
        FolderNameReader.SeparatorOption,
    };

    private static string Text(string key, params object[] values)
    {
        PropertyInfo property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ArgumentException("No string " + key, nameof(key));
        return string.Format(CultureInfo.InvariantCulture, (string)property.GetValue(null)!, values);
    }

    private static string Cut(string windowText) =>
        windowText.Substring(0, windowText.LastIndexOf("Until then", StringComparison.Ordinal)).TrimEnd();

    private static string Tail => " " + Strings.Usage + " " + Strings.HelpPointer;

    private static ParsedCommand Parse(string joined)
    {
        var args = new List<string> { "apply", @"D:\P" };
        args.AddRange(joined.Split('|'));
        return CommandLineParser.Parse(args);
    }

    private static ApplyPanels Panels(string joined)
    {
        ParsedCommand command = Parse(joined);
        Assert.True(command.Kind == CommandKind.Apply, command.Error);
        return command.Options!.Panels;
    }

    private static T Only<T>(string joined)
        where T : class, IRulePanelModel =>
        Assert.IsType<T>(Assert.Single(Panels(joined).Given));

    /// <summary>One valid use of a word, for the repeat rows.</summary>
    private static string Once(string word)
    {
        switch (word)
        {
            case RegexReader.RegexOption:
                return word + "|a|b";
            case MoveCopyReader.MoveOption:
            case MoveCopyReader.CopyOption:
            case FolderNameReader.LevelsOption:
                return word + "|1";
            case MoveCopyReader.FromOption:
            case MoveCopyReader.ToOption:
                return word + "|start";
            case FolderNameReader.PlacementOption:
                return word + "|prefix";
            case AutoDateReader.SourceOption:
                return word + "|modified";
            case AutoDateReader.PlacementOption:
                return word + "|prefix";
            case AutoDateReader.FormatOption:
                return word + "|yyyy";
            case MoveCopyReader.PositionOption:
            case MoveCopyReader.SeparatorOption:
            case AutoDateReader.SeparatorOption:
            case FolderNameReader.SeparatorOption:
                return word + "|x";
            default:
                return word;
        }
    }

    public static IEnumerable<object[]> ErrorRows()
    {
        string number = Cut(UiStrings.MoveCopyNumberInvalid);
        string regexNeeds = Strings.RegexNeedsValues;
        yield return new object[] { "--regex|a", regexNeeds };
        yield return new object[] { "--regex|a|--dry-run", regexNeeds };
        yield return new object[] { "--regex|--dry-run|b", regexNeeds };
        yield return new object[] { "--regex", regexNeeds };
        yield return new object[] { "--regex||b", Text("EmptyOptionText", "--regex") };
        yield return new object[]
        {
            "--regex|(|x", Text("OptionError", "--regex", Cut(new RegexPanelModel { Pattern = "(" }.PatternError)),
        };
        yield return new object[]
            { "--regex-ignore-case", Text("NeedsOption", "--regex-ignore-case", "--regex") };
        yield return new object[] { "--regex-extension", Text("NeedsOption", "--regex-extension", "--regex") };
        yield return new object[]
            { "--regex-extension|--regex-ignore-case", Text("NeedsOption", "--regex-ignore-case", "--regex") };

        yield return new object[] { "--move|1|--copy|1", Text("OneModeOnly", Strings.MoveModeOptions) };
        yield return new object[] { "--copy|1|--move|1", Text("OneModeOnly", Strings.MoveModeOptions) };
        yield return new object[] { "--move|1|--move|2", Text("RepeatedOption", "--move") };
        yield return new object[] { "--move", Text("OptionNeedsValue", "--move") };
        yield return new object[] { "--move|--dry-run", Text("OptionNeedsValue", "--move") };
        foreach (string word in new[] { "--move", "--copy" })
        {
            foreach (string value in new[] { "0", "x", "", " " })
            {
                yield return new object[] { word + "|" + value, Text("OptionError", word, number) };
            }
        }

        foreach (string modifier in new[]
            { "--move-from|end", "--move-to|start", "--move-position|2", "--move-separator|-" })
        {
            yield return new object[]
                { modifier, Text("NeedsOption", modifier.Split('|')[0], Strings.MoveModeOptions) };
        }

        string position = Text("OptionError", "--move-position", number);
        yield return new object[] { "--move|2|--move-to|position", position };
        yield return new object[] { "--move|2|--move-to|position|--move-position|0", position };
        yield return new object[] { "--move|2|--move-to|position|--move-position|x", position };
        yield return new object[] { "--move|2|--move-to|position|--move-position|", position };
        yield return new object[]
        {
            "--move-from|end|--move-to|position", Text("NeedsOption", "--move-from", Strings.MoveModeOptions),
        };
        yield return new object[]
        {
            "--move-from|middle", Text("BadChoice", "middle", "--move-from", Strings.MoveFromValues),
        };
        yield return new object[] { "--move-to|top", Text("BadChoice", "top", "--move-to", Strings.MoveToValues) };

        yield return new object[]
        {
            "--date-source|photo", Text("BadChoice", "photo", "--date-source", Strings.DateSourceValues),
        };
        yield return new object[]
        {
            "--date-placement|middle", Text("BadChoice", "middle", "--date-placement", Strings.PlacementValues),
        };
        yield return new object[]
        {
            "--folder-placement|middle", Text("BadChoice", "middle", "--folder-placement", Strings.PlacementValues),
        };
        yield return new object[] { "--date-format|", Text("EmptyOptionText", "--date-format") };
        foreach (string format in new[] { "yyyy zzz", "yyyy%" })
        {
            yield return new object[]
            {
                "--date-format|" + format,
                Text("OptionError", "--date-format", Cut(new AutoDatePanelModel { Format = format }.FormatError)),
            };
        }

        string levels = Text("OptionError", "--folder-levels", Cut(UiStrings.FolderNameLevelsInvalid));
        yield return new object[] { "--folder-levels|0", levels };
        yield return new object[] { "--folder-levels|x", levels };
        yield return new object[] { "--folder-levels|", levels };

        yield return new object[]
        {
            "--remove-whole-word|--regex-ignore-case", Text("NeedsOption", "--remove-whole-word", "--remove-text"),
        };

        foreach (string word in Words)
        {
            string once = Once(word);
            yield return new object[] { once + "|" + once, Text("RepeatedOption", word) };
        }
    }

    [Theory]
    [MemberData(nameof(ErrorRows))]
    public void UsageErrors(string options, string message)
    {
        ParsedCommand command = Parse(options);

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(message + Tail, command.Error);
    }

    [Fact]
    public void ErrorRows_HaveNoDuplicateOptions()
    {
        string[] duplicated = ErrorRows()
            .GroupBy(row => (string)row[0], StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(duplicated.Length == 0, "Rows yielded twice: " + string.Join(", ", duplicated));
    }

    [Fact]
    public void LiteralMessages()
    {
        Assert.Equal("--regex needs two values: the pattern and the replacement." + Tail, Parse("--regex|a").Error);
        Assert.Equal("Give only one of --move or --copy." + Tail, Parse("--move|1|--copy|1").Error);
        Assert.Equal("--move-from needs --move or --copy." + Tail, Parse("--move-from|end").Error);
        Assert.Equal("--move: Type a whole number from 1." + Tail, Parse("--move|0").Error);
        Assert.Equal("--copy: Type a whole number from 1." + Tail, Parse("--copy| ").Error);
        Assert.Equal("--move-position: Type a whole number from 1." + Tail,
            Parse("--move|2|--move-to|position").Error);
        Assert.Equal("\"middle\" is not a value of --move-from: use start or end." + Tail,
            Parse("--move-from|middle").Error);
        Assert.Equal("\"top\" is not a value of --move-to: use start, end or position." + Tail,
            Parse("--move-to|top").Error);
        Assert.Equal("--folder-levels: Type a whole number from 1." + Tail, Parse("--folder-levels|0").Error);
        Assert.StartsWith("--regex: This pattern is not valid.", Parse("--regex|(|x").Error, StringComparison.Ordinal);
        Assert.StartsWith("--date-format: This format is not valid.", Parse("--date-format|yyyy zzz").Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RegexFlags_FillTheModel()
    {
        RegexPanelModel plain = Only<RegexPanelModel>(@"--regex|(\d+)|[$1]");
        Assert.Equal(@"(\d+)", plain.Pattern);
        Assert.Equal("[$1]", plain.Replacement);
        Assert.False(plain.IgnoreCase);
        Assert.False(plain.IncludeExtension);
        Assert.True(plain.Enabled);

        RegexPanelModel both = Only<RegexPanelModel>("--regex-extension|--regex-ignore-case|--regex|a|");
        Assert.Equal("", both.Replacement);
        Assert.True(both.IgnoreCase);
        Assert.True(both.IncludeExtension);

        Assert.Equal("-", Only<RegexPanelModel>("--regex|-|_").Pattern);
    }

    [Fact]
    public void MoveCopy_FillsTheModel()
    {
        MoveCopyPanelModel move = Only<MoveCopyPanelModel>("--move|3");
        Assert.Equal(MoveCopyMode.Move, move.Mode);
        Assert.Equal("3", move.CountText);
        Assert.Equal(MoveCopySource.Start, move.Source);
        Assert.Equal(MoveCopyTarget.End, move.Target);
        Assert.Equal("", move.PositionText);
        Assert.Equal("", move.Separator);
        Assert.True(move.Enabled);

        MoveCopyPanelModel copy = Only<MoveCopyPanelModel>(
            "--move-separator|-|--copy|2|--move-position|5|--move-to|position|--move-from|end");
        Assert.Equal(MoveCopyMode.Copy, copy.Mode);
        Assert.Equal("2", copy.CountText);
        Assert.Equal(MoveCopySource.End, copy.Source);
        Assert.Equal(MoveCopyTarget.Position, copy.Target);
        Assert.Equal("5", copy.PositionText);
        Assert.Equal("-", copy.Separator);

        Assert.Equal(MoveCopyTarget.Start, Only<MoveCopyPanelModel>("--move|1|--move-to|START").Target);
    }

    [Fact]
    public void MovePosition_WithOtherTarget_IsIgnored()
    {
        MoveCopyPanelModel model = Only<MoveCopyPanelModel>("--move|2|--move-to|start|--move-position|x");

        Assert.Equal("x", model.PositionText);
        Assert.NotNull(model.BuildRule());
    }

    [Fact]
    public void AutoDate_SourceTaken_Parses_PhotoIsRefused()
    {
        Assert.Equal(DateSource.Taken, Only<AutoDatePanelModel>("--date-source|taken").Source);
        Assert.Equal(DateSource.Taken, Only<AutoDatePanelModel>("--date-source|TAKEN").Source);
        Assert.Equal("modified, created or taken", Strings.DateSourceValues);
    }

    [Fact]
    public void AutoDate_AnyWordTurnsItOn()
    {
        var rows = new (string Options, Func<AutoDatePanelModel, object> Read, object Expected)[]
        {
            ("--date", m => m.Source, DateSource.Modified),
            ("--date-source|created", m => m.Source, DateSource.Created),
            ("--date-source|taken", m => m.Source, DateSource.Taken),
            ("--date-format|yyyyMMdd", m => m.Format, "yyyyMMdd"),
            ("--date-placement|suffix", m => m.Placement, DatePlacement.Suffix),
            ("--date-separator|", m => m.Separator, ""),
            ("--date-separator|-", m => m.Separator, "-"),
        };

        foreach ((string options, Func<AutoDatePanelModel, object> read, object expected) in rows)
        {
            AutoDatePanelModel model = Only<AutoDatePanelModel>(options);
            Assert.True(model.Enabled);
            Assert.Equal(expected, read(model));
        }

        AutoDatePanelModel created = Only<AutoDatePanelModel>("--date-source|created");
        Assert.Equal("yyyy-MM-dd", created.Format);
        Assert.Equal(DatePlacement.Prefix, created.Placement);
        Assert.Equal("_", created.Separator);

        AutoDatePanelModel plain = Only<AutoDatePanelModel>("--date");
        Assert.Equal("yyyy-MM-dd", plain.Format);
        Assert.Equal(DatePlacement.Prefix, plain.Placement);
        Assert.Equal("_", plain.Separator);
    }

    [Fact]
    public void FolderName_AnyWordTurnsItOn()
    {
        FolderNamePanelModel plain = Only<FolderNamePanelModel>("--folder-name");
        Assert.True(plain.Enabled);
        Assert.Equal("1", plain.LevelsText);
        Assert.Equal(FolderNamePlacement.Prefix, plain.Placement);
        Assert.Equal("_", plain.Separator);

        FolderNamePanelModel levels = Only<FolderNamePanelModel>("--folder-levels|2");
        Assert.Equal("2", levels.LevelsText);
        Assert.Equal(FolderNamePlacement.Prefix, levels.Placement);
        Assert.Equal("_", levels.Separator);

        FolderNamePanelModel placement = Only<FolderNamePanelModel>("--folder-placement|suffix");
        Assert.Equal(FolderNamePlacement.Suffix, placement.Placement);
        Assert.Equal("1", placement.LevelsText);

        FolderNamePanelModel separator = Only<FolderNamePanelModel>("--folder-separator|");
        Assert.Equal("", separator.Separator);
        Assert.Equal("1", separator.LevelsText);
    }

    [Fact]
    public void EveryWord_AloneWithItsValue_IsARuleOption()
    {
        foreach (string word in Words)
        {
            string options = word switch
            {
                "--regex-ignore-case" or "--regex-extension" => word + "|--regex|a|b",
                "--move-from" or "--move-to" or "--move-position" or "--move-separator" => Once(word) + "|--move|1",
                _ => Once(word),
            };

            ParsedCommand command = Parse(options);
            Assert.True(command.Kind == CommandKind.Apply, word + ": " + command.Error);
        }
    }

    [Fact]
    public void Words_AreAllInTheApplyList()
    {
        Assert.All(Words, word => Assert.Contains(word, CommandLineParser.ApplyOptionWords));
        Assert.Equal(18, Words.Length);
    }

    [Fact]
    public void ApplyHelp_RegexMoveDateFolderLines()
    {
        string[] lines = HelpText.Apply().ToArray();
        int last = Array.FindIndex(lines, l => l.StartsWith("  --remove-double-spaces ", StringComparison.Ordinal));
        int preset = Array.FindIndex(lines, l => l.StartsWith("  --preset ", StringComparison.Ordinal));
        string[] ownLine =
        {
            "--regex", "--move-to", "--date-source", "--date-placement", "--folder-placement",
        };

        foreach (string word in Words)
        {
            int[] found = Enumerable.Range(0, lines.Length)
                .Where(n => lines[n].StartsWith("  " + word, StringComparison.Ordinal)
                    && (lines[n].Length == word.Length + 2 || lines[n][word.Length + 2] == ' '))
                .ToArray();
            int at = Assert.Single(found);
            Assert.True(at > last && at < preset, word);
            string line = lines[at];
            Assert.True(line.Length <= 79, word);
            if (line.Substring(0, Math.Min(line.Length, 29)).TrimEnd().Length < 29 && line.Length > 29)
            {
                Assert.True(line[29] != ' ', word);
                Assert.DoesNotContain(word, ownLine);
            }
            else
            {
                Assert.Contains(word, ownLine);
                string next = lines[at + 1];
                Assert.True(next.Length > 29 && next.Substring(0, 29).Trim().Length == 0 && next[29] != ' ', word);
                Assert.True(next.Length <= 79, word);
            }
        }
    }
}
