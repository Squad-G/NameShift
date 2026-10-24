using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class FirstPanelsTests
{
    [Fact]
    public void AllEnabled_RunReplaceThenCaseThenAdd()
    {
        var replace = new ReplacePanelModel { Enabled = true, Find = "a", With = "b", MatchCase = true };
        var @case = new CasePanelModel { Enabled = true, Mode = CaseMode.Upper };
        var add = new AddPanelModel { Enabled = true, Prefix = "x" };
        var host = new RulePanelHostModel(new IRulePanelModel[] { add, @case, replace });

        Assert.Equal(
            new[] { RuleStage.Replace, RuleStage.Case, RuleStage.Add },
            host.Rules.Select(r => r.Stage).ToArray());
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("abc.txt", @"C:\f") });
        Assert.Equal("xBBC.txt", results[0].NewName);
    }

    [Fact]
    public void Numbering_RunsAfterAdd_AtStage9()
    {
        var add = new AddPanelModel { Enabled = true, Prefix = "x" };
        var numbering = new NumberingPanelModel { Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { numbering, add });

        Assert.Equal(new[] { RuleStage.Add, RuleStage.Numbering }, host.Rules.Select(r => r.Stage).ToArray());
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("a.txt", @"C:\f") });
        Assert.Equal("xa_1.txt", results[0].NewName);
    }

    [Fact]
    public void Mnemonics_UniqueWithinEachPanel_TitlesDistinct()
    {
        var panels = new (string Title, string[] Labels)[]
        {
            (Strings.ReplaceTitle, new[]
            {
                Strings.ReplaceFind, Strings.ReplaceWith, Strings.ReplaceMatchCase, Strings.ReplaceKeepCase,
                Strings.ReplaceIncludeExtension,
            }),
            (Strings.CaseTitle, new[] { Strings.CaseMode, Strings.CaseExceptions }),
            (Strings.AddTitle, new[]
            {
                Strings.AddPrefix, Strings.AddInsert, Strings.AddPosition, Strings.AddFromEnd, Strings.AddSuffix,
                Strings.AddWordSpace,
            }),
            (Strings.NumberingTitle, new[]
            {
                Strings.NumberingMode, Strings.NumberingStart, Strings.NumberingStep, Strings.NumberingPad,
                Strings.NumberingSeparator, Strings.NumberingPosition,
            }),
        };

        Assert.Empty(MnemonicCheck.Problems(panels));
        Assert.Equal(MnemonicCheck.LetterOf(Strings.AddPosition), MnemonicCheck.LetterOf(Strings.AddPositionFromEnd));
    }

    [Fact]
    public void Strings_ReplaceKeepCase_HasAltC()
    {
        Assert.Equal('C', MnemonicCheck.LetterOf(Strings.ReplaceKeepCase));
        Assert.Equal("Match the case of the found text", Strings.ReplaceKeepCase.Replace("&", ""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("+3")]
    [InlineData("99999999999999999999")]
    [InlineData("1.5")]
    [InlineData("\u0663")]
    [InlineData("\0")]
    public void BuildRule_NeverThrows(string? text)
    {
        var replace = new ReplacePanelModel { Find = text!, With = text! };
        var @case = new CasePanelModel { Mode = CaseMode.Title, ExceptionsText = text! };
        foreach (bool fromEnd in new[] { false, true })
        {
            var add = new AddPanelModel
            {
                Prefix = text!, Insert = text!, InsertPositionText = text!, InsertFromEnd = fromEnd, Suffix = text!,
            };

            var exception = Record.Exception(() =>
            {
                replace.BuildRule();
                @case.BuildRule();
                add.BuildRule();
            });

            Assert.Null(exception);
        }
    }

    [Fact]
    public void Defaults_MatchTheEngine()
    {
        var replace = new ReplacePanelModel();
        var @case = new CasePanelModel();
        var add = new AddPanelModel();
        foreach (IRulePanelModel model in new IRulePanelModel[] { replace, @case, add })
        {
            Assert.True(model.IsDefault);
            Assert.False(model.Enabled);
            Assert.Null(model.BuildRule());
        }

        var engineReplace = new ReplaceRule("a", "");
        Assert.Equal(engineReplace.MatchCase, replace.MatchCase);
        Assert.Equal(engineReplace.IncludeExtension, replace.IncludeExtension);
        Assert.Equal(engineReplace.KeepCase, replace.KeepCase);
        var engineAdd = new AddRule();
        Assert.Equal(engineAdd.Prefix, add.Prefix);
        Assert.Equal(engineAdd.Insert, add.Insert);
        Assert.Equal(engineAdd.InsertPosition, add.InsertPosition);
        Assert.Equal(engineAdd.InsertFromEnd, add.InsertFromEnd);
        Assert.Equal(engineAdd.Suffix, add.Suffix);
        Assert.Equal(engineAdd.WordSpace, add.WordSpace);
        Assert.Equal(CaseMode.Same, @case.Mode);
    }

    public static IEnumerable<object[]> SettingChanges() => new[]
    {
        Change(new ReplacePanelModel(), m => m.Find = "x"),
        Change(new ReplacePanelModel(), m => m.With = "x"),
        Change(new ReplacePanelModel(), m => m.MatchCase = true),
        Change(new ReplacePanelModel(), m => m.IncludeExtension = true),
        Change(new ReplacePanelModel(), m => m.KeepCase = true),
        Change(new CasePanelModel(), m => m.Mode = CaseMode.Upper),
        Change(new CasePanelModel(), m => m.ExceptionsText = "x"),
        Change(new AddPanelModel(), m => m.Prefix = "x"),
        Change(new AddPanelModel(), m => m.Insert = "x"),
        Change(new AddPanelModel(), m => m.InsertPositionText = "2"),
        Change(new AddPanelModel(), m => m.InsertFromEnd = true),
        Change(new AddPanelModel(), m => m.Suffix = "x"),
        Change(new AddPanelModel(), m => m.WordSpace = true),
    };

    [Theory]
    [MemberData(nameof(SettingChanges))]
    public void SettingChange_RaisesChangedOnceAtOnce(IRulePanelModel model, Action change)
    {
        int raised = 0;
        model.Changed += (_, _) => raised++;

        change();
        Assert.Equal(1, raised);

        change(); // same value again
        Assert.Equal(1, raised);
    }

    private static object[] Change<T>(T model, Action<T> set)
        where T : class, IRulePanelModel => new object[] { model, (Action)(() => set(model)) };
}
