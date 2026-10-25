using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>NS-79: the apply parser is split into readers; these tests pin what the split must keep (words, error
/// precedence) and the seam NS-75 and NS-76 extend.</summary>
public sealed class ApplyReadersTests
{
    private static string[] Args(string joined) => joined.Split('|');

    private static string Text(string key, params object[] values)
    {
        PropertyInfo property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ArgumentException("No string " + key, nameof(key));
        return string.Format(CultureInfo.InvariantCulture, (string)property.GetValue(null)!, values);
    }

    [Fact]
    public void RepeatedDryRun_IsAccepted()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|lower|--dry-run|--dry-run"));

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.True(command.Options!.DryRun);
    }

    [Fact]
    public void CaseValueThatIsAnOption_IsBadCase()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|--zzz"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith(Text("BadCase", "--zzz"), command.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownWordAsPrefixValue_IsTheValue()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--prefix|--zzz"));

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.Equal("--zzz", command.Options!.Panels.Add!.Prefix);
    }

    [Theory]
    [InlineData("-q|--quiet", "--quiet")]
    [InlineData("-v|--verbose", "--verbose")]
    public void ShortAliases_RepeatName_TheLongOption(string joined, string option)
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|lower|" + joined));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith(Text("RepeatedOption", option), command.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--zzz|--case", "UnknownOption", "--zzz", null)]
    [InlineData(@"apply|D:\P|D:\Q|-q|-v", "TwoFolders", @"D:\P", @"D:\Q")]
    [InlineData(@"apply||-q|-v|--case|lower", "NoFolder", null, null)]
    [InlineData(@"apply|D:\P|-q|-v|--dry-run|--case|lower", "QuietWithVerbose", null, null)]
    [InlineData(@"apply|D:\P|-q|--dry-run|--overwrite|--case|lower", "QuietWithDryRun", null, null)]
    [InlineData(@"apply|D:\P|--overwrite|--from-end|--case|lower", "OverwriteNeedsSave", null, null)]
    [InlineData(@"apply|D:\P|--from-end|--number-start|x", "FromEndNeedsInsert", null, null)]
    [InlineData(@"apply|--dry-run", "NoFolder", null, null)]
    public void ErrorPrecedence_FirstMistakeWins(string joined, string key, string? value0, string? value1)
    {
        object[] values = new[] { value0, value1 }.Where(v => v != null).Cast<object>().ToArray();

        ParsedCommand command = CommandLineParser.Parse(Args(joined));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(Text(key, values) + " " + Strings.Usage + " " + Strings.HelpPointer, command.Error);
    }

    [Fact]
    public void ErrorPrecedence_NumberingBeatsTheMissingFolder()
    {
        ParsedCommand command = CommandLineParser.Parse(Args("apply|--number-start|x"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith("--number-start: ", command.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.NoFolder, command.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorPrecedence_StartErrorBeatsStepError()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--number-start|x|--number-step|0"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith("--number-start: ", command.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryApplyWord_IsClaimedByExactlyOneReader()
    {
        var readers = ApplyReaders.Create();
        foreach (string word in CommandLineParser.ApplyOptionWords)
        {
            int claims = readers.Count(r => r.Words.Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase)));
            Assert.True(claims == 1, word + " is claimed by " + claims + " readers.");
        }

        foreach (ApplyOptionReader reader in readers)
        {
            foreach (string own in reader.Words)
            {
                Assert.True(
                    CommandLineParser.ApplyOptionWords.Contains(own, StringComparer.OrdinalIgnoreCase),
                    own + " is a reader word that ApplyOptionWords lacks.");
            }
        }
    }

    [Fact]
    public void FakeReader_IsDispatched_WithoutTouchingTheLoop()
    {
        var fake = new FakeReader();

        ParsedCommand command = CommandLineParser.ParseApply(
            Args(@"apply|--fake|v|D:\P|--case|lower"), ApplyReaders.Create().Concat(new[] { fake }).ToList());

        Assert.Equal(CommandKind.Apply, command.Kind);
        ApplyOptions options = command.Options!;
        Assert.Equal(@"D:\P", options.FolderText);
        Assert.Equal(new[] { "v" }, fake.Values);
        Assert.Equal(CaseMode.Lower, options.Case);
        Assert.Contains(fake.Model, options.Panels.Given);
    }

    [Fact]
    public void FakeReader_FinishError_IsTheResult()
    {
        var fake = new FakeReader { FinishError = OptionArgs.Fail("x") };

        ParsedCommand command = CommandLineParser.ParseApply(
            Args(@"apply|--fake|v|D:\P|--case|lower"), ApplyReaders.Create().Concat(new[] { fake }).ToList());

        Assert.Same(fake.FinishError, command);
    }

    [Fact]
    public void ApplyPanels_SortsByStage_AndRefusesTwoOfAStage()
    {
        var add = new AddPanelModel { Enabled = true };
        var numbering = new NumberingPanelModel { Enabled = true };

        var panels = new ApplyPanels(new IRulePanelModel[] { numbering, add });

        Assert.Equal(new IRulePanelModel[] { add, numbering }, panels.Given);
        Assert.Same(add, panels.Add);
        Assert.Same(numbering, panels.Numbering);
        Assert.Empty(ApplyPanels.None.Given);
        Assert.Throws<ArgumentException>(
            () => new ApplyPanels(new IRulePanelModel[] { add, new AddPanelModel { Enabled = true } }));
        Assert.Throws<ArgumentNullException>(() => new ApplyPanels(null!));
        Assert.Throws<ArgumentNullException>(() => new ApplyPanels(new IRulePanelModel[] { add, null! }));
    }

    private sealed class FakeReader : ApplyOptionReader
    {
        private static readonly string[] OwnWords = { "--fake" };

        public List<string> Values { get; } = new List<string>();

        public RemovePanelModel Model { get; } = new RemovePanelModel { Enabled = true };

        public ParsedCommand? FinishError { get; set; }

        public override IReadOnlyList<string> Words => OwnWords;

        protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int index, string word)
        {
            if (!OptionArgs.TakeOne(args, ref index, word, out string? value, out ParsedCommand? error))
            {
                return OptionRead.Failed(error!);
            }

            Values.Add(value!);
            return OptionRead.Taken;
        }

        public override ParsedCommand? Finish(ApplyDraft draft)
        {
            draft.Panels.Add(Model);
            return FinishError;
        }
    }
}
