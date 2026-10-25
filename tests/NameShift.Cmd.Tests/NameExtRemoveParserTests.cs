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

/// <summary>The Name, Extension and Remove options of <c>apply</c> in the parser: usage errors, the models filled.</summary>
public sealed class NameExtRemoveParserTests
{
    private static readonly string[] Words =
    {
        NameReader.FixedOption, NameReader.EmptyOption, NameReader.ReverseOption,
        ExtensionReader.LowerOption, ExtensionReader.UpperOption, ExtensionReader.FixedOption,
        ExtensionReader.ExtraOption, ExtensionReader.RemoveOption,
        RemoveReader.FirstOption, RemoveReader.LastOption, RemoveReader.FromOption, RemoveReader.ToOption,
        RemoveReader.CharsOption, RemoveReader.TextOption, RemoveReader.BeforeOption, RemoveReader.AfterOption,
        RemoveReader.WholeWordOption, RemoveReader.MatchCaseOption, RemoveReader.KeepMarkerOption,
        RemoveReader.DigitsOption, RemoveReader.SymbolsOption, RemoveReader.AccentsOption,
        RemoveReader.HighCharsOption, RemoveReader.TrimOption, RemoveReader.LeadingDotsOption,
        RemoveReader.DoubleSpacesOption,
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

    public static IEnumerable<object[]> ErrorRows()
    {
        string name = Strings.NameModeOptions;
        string ext = Strings.ExtensionModeOptions;
        yield return new object[] { "--name-fixed|a|--name-reverse", Text("OneModeOnly", name) };
        yield return new object[] { "--name-empty|--name-reverse", Text("OneModeOnly", name) };
        yield return new object[] { "--name-reverse|--name-fixed|a", Text("OneModeOnly", name) };
        yield return new object[] { "--name-fixed|", Text("BlankText", "--name-fixed") };
        yield return new object[] { "--name-fixed|  ", Text("BlankText", "--name-fixed") };
        yield return new object[] { "--name-fixed", Text("OptionNeedsValue", "--name-fixed") };
        yield return new object[] { "--name-fixed|--dry-run", Text("OptionNeedsValue", "--name-fixed") };
        yield return new object[] { "--ext-lower|--ext-remove", Text("OneModeOnly", ext) };
        yield return new object[] { "--ext-fixed|a|--ext-extra|b", Text("OneModeOnly", ext) };
        yield return new object[] { "--ext-fixed|.", Text("BlankExtension", "--ext-fixed") };
        yield return new object[] { "--ext-extra| . ", Text("BlankExtension", "--ext-extra") };
        yield return new object[] { "--ext-fixed|", Text("BlankExtension", "--ext-fixed") };
        yield return new object[] { "--ext-fixed", Text("OptionNeedsValue", "--ext-fixed") };

        foreach (string word in new[] { "--remove-first", "--remove-last" })
        {
            foreach (string value in new[] { "0", "x", "", " " })
            {
                yield return new object[]
                {
                    word + "|" + value, Text("OptionError", word, Cut(UiStrings.RemovePositionInvalid)),
                };
            }
        }

        string range = Text("OptionError", Strings.RemoveRangeOptions, Cut(UiStrings.RemoveRangeInvalid));
        yield return new object[] { "--remove-from|2", range };
        yield return new object[] { "--remove-to|3", range };
        yield return new object[] { "--remove-from||--remove-to|3", range };
        yield return new object[] { "--remove-from||--remove-to|", range };
        yield return new object[] { "--remove-from|4|--remove-to|2", range };
        yield return new object[] { "--remove-from|x|--remove-to|2", range };

        foreach (string word in new[] { "--remove-chars", "--remove-text", "--remove-before", "--remove-after" })
        {
            yield return new object[] { word + "|", Text("EmptyOptionText", word) };
        }

        yield return new object[] { "--remove-whole-word", Text("NeedsOption", "--remove-whole-word", "--remove-text") };
        yield return new object[]
        {
            "--remove-whole-word|--remove-chars|a", Text("NeedsOption", "--remove-whole-word", "--remove-text"),
        };
        yield return new object[]
        {
            "--remove-match-case", Text("NeedsOption", "--remove-match-case", Strings.RemoveMatchCaseNeeds),
        };
        yield return new object[]
        {
            "--remove-match-case|--remove-digits",
            Text("NeedsOption", "--remove-match-case", Strings.RemoveMatchCaseNeeds),
        };
        yield return new object[]
        {
            "--remove-keep-marker|--remove-text|a",
            Text("NeedsOption", "--remove-keep-marker", Strings.RemoveKeepMarkerNeeds),
        };
        yield return new object[]
        {
            "--remove-whole-word|--remove-from|4|--remove-to|2",
            Text("NeedsOption", "--remove-whole-word", "--remove-text"),
        };
        yield return new object[]
        {
            "--number-start|x|--remove-from|2",
            Text("OptionError", "--number-start", Cut(UiStrings.NumberingStartInvalid)),
        };

        foreach (string word in Words)
        {
            bool isCount = word.StartsWith("--remove-", StringComparison.Ordinal)
                && Array.IndexOf(new[] { "first", "last", "from", "to" }, word.Substring(9)) >= 0;
            bool takesText = word == "--name-fixed" || word == "--ext-fixed" || word == "--ext-extra"
                || word == "--remove-chars" || word == "--remove-text" || word == "--remove-before"
                || word == "--remove-after";
            string once = word + (isCount ? "|1" : takesText ? "|a" : "");
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
    public void LiteralMessages()
    {
        Assert.Equal("--remove-first: Type a whole number from 1." + Tail, Parse("--remove-first|0").Error);
        Assert.Equal(
            "--remove-from and --remove-to: Type two whole numbers from 1, From not after To." + Tail,
            Parse("--remove-from|4|--remove-to|2").Error);
        Assert.Equal("Give only one of --name-fixed, --name-empty or --name-reverse." + Tail,
            Parse("--name-empty|--name-reverse").Error);
        Assert.Equal("Give only one of --ext-lower, --ext-upper, --ext-fixed, --ext-extra or --ext-remove." + Tail,
            Parse("--ext-lower|--ext-upper").Error);
    }

    [Fact]
    public void NameFlags_FillTheModel()
    {
        NamePanelModel fixedName = Assert.IsType<NamePanelModel>(Assert.Single(Panels("--name-fixed|Trip").Given));
        Assert.Equal(NameMode.Fixed, fixedName.Mode);
        Assert.Equal("Trip", fixedName.Text);
        Assert.True(fixedName.Enabled);

        Assert.Equal(NameMode.Remove, Assert.IsType<NamePanelModel>(Assert.Single(Panels("--name-empty").Given)).Mode);
        Assert.Equal(NameMode.Reverse, Assert.IsType<NamePanelModel>(Assert.Single(Panels("--name-reverse").Given)).Mode);
        Assert.Equal(" -x", Assert.IsType<NamePanelModel>(Assert.Single(Panels("--name-fixed| -x").Given)).Text);
    }

    [Fact]
    public void ExtFlags_FillTheModel()
    {
        var expected = new (string Options, ExtensionMode Mode, string Text)[]
        {
            ("--ext-lower", ExtensionMode.Lower, ""),
            ("--ext-upper", ExtensionMode.Upper, ""),
            ("--ext-fixed|.jpeg", ExtensionMode.Fixed, ".jpeg"),
            ("--ext-extra|bak", ExtensionMode.Extra, "bak"),
            ("--ext-remove", ExtensionMode.Remove, ""),
        };

        foreach ((string options, ExtensionMode mode, string text) in expected)
        {
            ExtensionPanelModel model = Assert.IsType<ExtensionPanelModel>(Assert.Single(Panels(options).Given));
            Assert.Equal(mode, model.Mode);
            Assert.Equal(text, model.Text);
            Assert.True(model.Enabled);
        }
    }

    [Fact]
    public void RemoveOptions_FillTheModel()
    {
        var rows = new (string Options, Func<RemovePanelModel, object> Read, object Expected)[]
        {
            ("--remove-first|3", m => m.FirstText, "3"),
            ("--remove-last|2", m => m.LastText, "2"),
            ("--remove-from|2|--remove-to|3", m => m.FromText, "2"),
            ("--remove-from|2|--remove-to|3", m => m.ToText, "3"),
            ("--remove-chars|ab", m => m.Characters, "ab"),
            ("--remove-text| ", m => m.Text, " "),
            ("--remove-before|-", m => m.CropBefore, "-"),
            ("--remove-after|-", m => m.CropAfter, "-"),
            ("--remove-text|a|--remove-whole-word", m => m.WholeWord, true),
            ("--remove-text|a|--remove-match-case", m => m.MatchCase, true),
            ("--remove-before|a|--remove-keep-marker", m => m.KeepMarker, true),
            ("--remove-after|a|--remove-keep-marker", m => m.KeepMarker, true),
            ("--remove-digits", m => m.Digits, true),
            ("--remove-symbols", m => m.Symbols, true),
            ("--remove-accents", m => m.Accents, true),
            ("--remove-high-chars", m => m.HighCharacters, true),
            ("--remove-trim", m => m.Trim, true),
            ("--remove-leading-dots", m => m.LeadingDots, true),
            ("--remove-double-spaces", m => m.DoubleSpaces, true),
        };

        foreach ((string options, Func<RemovePanelModel, object> read, object expected) in rows)
        {
            RemovePanelModel model = Assert.IsType<RemovePanelModel>(Assert.Single(Panels(options).Given));
            Assert.True(model.Enabled);
            Assert.Equal(expected, read(model));
        }

        RemovePanelModel alone = Assert.IsType<RemovePanelModel>(Assert.Single(Panels("--remove-digits").Given));
        Assert.False(alone.WholeWord);
        Assert.Equal("", alone.Text);
        Assert.Equal("", alone.FirstText);
    }

    [Theory]
    [InlineData("--remove-first|1")]
    [InlineData("--remove-last|99")]
    [InlineData("--remove-from|2|--remove-to|2")]
    [InlineData("--remove-to|3|--remove-from|1")]
    [InlineData("--remove-chars|ab")]
    [InlineData("--remove-text| ")]
    [InlineData("--remove-text|a|--remove-whole-word")]
    [InlineData("--remove-chars|a|--remove-match-case")]
    [InlineData("--remove-before|a|--remove-keep-marker")]
    [InlineData("--remove-after|a|--remove-keep-marker|--remove-match-case")]
    [InlineData("--remove-digits")]
    [InlineData("--remove-symbols|--remove-accents")]
    [InlineData("--remove-high-chars")]
    [InlineData("--remove-trim")]
    [InlineData("--remove-leading-dots")]
    [InlineData("--remove-double-spaces")]
    public void AcceptedRemove_AlwaysBuildsARule(string options)
    {
        RemovePanelModel model = Assert.IsType<RemovePanelModel>(Assert.Single(Panels(options).Given));

        Assert.NotNull(model.BuildRule());
    }

    [Fact]
    public void EveryWord_AloneWithItsValue_IsARuleOption()
    {
        foreach (string word in Words)
        {
            string options = word switch
            {
                "--name-fixed" or "--ext-fixed" or "--ext-extra" or "--remove-chars" or "--remove-text"
                    or "--remove-before" or "--remove-after" => word + "|a",
                "--remove-first" or "--remove-last" => word + "|1",
                "--remove-from" => word + "|1|--remove-to|2",
                "--remove-to" => word + "|2|--remove-from|1",
                "--remove-whole-word" => word + "|--remove-text|a",
                "--remove-match-case" => word + "|--remove-text|a",
                "--remove-keep-marker" => word + "|--remove-before|a",
                _ => word,
            };

            Assert.Equal(CommandKind.Apply, Parse(options).Kind);
        }
    }


    [Fact]
    public void ApplyHelp_NameExtRemoveLines()
    {
        string[] lines = HelpText.Apply().ToArray();
        int numberCase = Array.FindIndex(lines, l => l.StartsWith("  --number-case ", StringComparison.Ordinal));
        int preset = Array.FindIndex(lines, l => l.StartsWith("  --preset ", StringComparison.Ordinal));

        foreach (string word in Words)
        {
            int[] found = Enumerable.Range(0, lines.Length)
                .Where(n => lines[n].StartsWith("  " + word, StringComparison.Ordinal)
                    && (lines[n].Length == word.Length + 2 || lines[n][word.Length + 2] == ' '))
                .ToArray();
            int at = Assert.Single(found);
            Assert.True(at > numberCase && at < preset, word);
            string line = lines[at];
            Assert.True(line.Length <= 79, word);
            Assert.True(line.Length > 29 && line.Substring(0, 29).TrimEnd().Length < 29 && line[29] != ' ', word);
        }
    }
    [Fact]
    public void Words_AreAllInTheApplyList()
    {
        Assert.All(Words, word => Assert.Contains(word, CommandLineParser.ApplyOptionWords));
        Assert.Equal(26, Words.Length);
    }

    [Fact]
    public void WindowStringsShown_HaveTheMarker()
    {
        string[] shown =
        {
            UiStrings.RemovePositionInvalid,
            UiStrings.RemoveRangeInvalid,
            UiStrings.NumberingStartInvalid,
            UiStrings.NumberingStepInvalid,
            UiStrings.NumberingPositionInvalid,
            string.Format(CultureInfo.InvariantCulture, UiStrings.NumberingPadInvalid, 9),
            UiStrings.NumberingStartInvalidHexadecimal,
            UiStrings.NumberingStartInvalidLetters,
            UiStrings.NumberingStartInvalidRoman,
        };

        foreach (string text in shown)
        {
            Assert.Contains(Strings.SkipSentenceMarker, text, StringComparison.Ordinal);
            string cut = OptionArgs.WithoutSkipSentence(text);
            Assert.DoesNotContain("Until", cut, StringComparison.Ordinal);
            Assert.DoesNotContain("skipped", cut, StringComparison.Ordinal);
            Assert.NotEmpty(cut);
        }
    }
}
