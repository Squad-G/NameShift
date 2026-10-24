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
            (Strings.RemoveTitle, new[]
            {
                Strings.RemoveFirst, Strings.RemoveLast, Strings.RemoveFrom, Strings.RemoveTo,
                Strings.RemoveCharacters, Strings.RemoveText, Strings.RemoveWholeWord, Strings.RemoveMatchCase,
                Strings.RemoveCropBefore, Strings.RemoveCropAfter, Strings.RemoveKeepMarker, Strings.RemoveDigits,
                Strings.RemoveSymbols, Strings.RemoveAccents, Strings.RemoveHighCharacters, Strings.RemoveTrim,
                Strings.RemoveLeadingDots, Strings.RemoveDoubleSpaces,
            }),
            (Strings.AddTitle, new[]
            {
                Strings.AddPrefix, Strings.AddInsert, Strings.AddPosition, Strings.AddFromEnd, Strings.AddSuffix,
                Strings.AddWordSpace,
            }),
            (Strings.NumberingTitle, new[]
            {
                Strings.NumberingMode, Strings.NumberingStart, Strings.NumberingStep, Strings.NumberingPad,
                Strings.NumberingSeparator, Strings.NumberingPosition, Strings.NumberingSystem,
                Strings.NumberingCase, Strings.NumberingRestartPerFolder,
            }),
            (Strings.NameTitle, new[] { Strings.NameMode, Strings.NameText }),
            (Strings.ExtensionTitle, new[] { Strings.ExtensionMode, Strings.ExtensionText }),
            (Strings.RegexTitle, new[]
            {
                Strings.RegexPattern, Strings.RegexReplacement, Strings.RegexIgnoreCase,
                Strings.RegexIncludeExtension,
            }),
            (Strings.MoveCopyTitle, new[]
            {
                Strings.MoveCopyMode, Strings.MoveCopySource, Strings.MoveCopyCount, Strings.MoveCopyTarget,
                Strings.MoveCopyPosition, Strings.MoveCopySeparator,
            }),
            (Strings.AutoDateTitle, new[]
            {
                Strings.AutoDateSource, Strings.AutoDateFormat, Strings.AutoDatePlace, Strings.AutoDateSeparator,
            }),
            (Strings.FolderNameTitle, new[]
            {
                Strings.FolderNamePlace, Strings.FolderNameLevels, Strings.FolderNameSeparator,
            }),
        };

        Assert.Empty(MnemonicCheck.Problems(panels));
        Assert.Equal(MnemonicCheck.LetterOf(Strings.AddPosition), MnemonicCheck.LetterOf(Strings.AddPositionFromEnd));
    }

    [Fact]
    public void NewPanels_UseTheStoryLetters()
    {
        Assert.Equal(
            new[] { 'M', 'E', 'T' },
            new[] { Strings.NameTitle, Strings.NameMode, Strings.NameText }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.Equal(
            new[] { 'X', 'M', 'T' },
            new[] { Strings.ExtensionTitle, Strings.ExtensionMode, Strings.ExtensionText }
                .Select(MnemonicCheck.LetterOf).ToArray());
        Assert.Equal(
            new[] { 'R', 'C', 'E', 'A', 'N', 'M', 'X', 'G', 'V', 'D', 'F' },
            new[]
            {
                Strings.ReplaceTitle, Strings.CaseTitle, Strings.RemoveTitle, Strings.AddTitle,
                Strings.NumberingTitle, Strings.NameTitle, Strings.ExtensionTitle, Strings.RegexTitle,
                Strings.MoveCopyTitle, Strings.AutoDateTitle, Strings.FolderNameTitle,
            }.Select(MnemonicCheck.LetterOf).ToArray());
    }

    [Fact]
    public void NumberingSystemAndCase_UseTheStoryLetters()
    {
        Assert.Equal(
            new[] { 'S', 'C' },
            new[] { Strings.NumberingSystem, Strings.NumberingCase }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.All(
            new[]
            {
                Strings.NumberingSystemDecimal, Strings.NumberingSystemHexadecimal, Strings.NumberingSystemLetters,
                Strings.NumberingSystemRoman, Strings.NumberingCaseUpper, Strings.NumberingCaseLower,
            },
            word => Assert.DoesNotContain("&", word));
    }

    [Fact]
    public void NumberingRestart_UsesLetterT()
    {
        Assert.Equal('T', MnemonicCheck.LetterOf(Strings.NumberingRestartPerFolder));
        Assert.Equal("Res&tart in each folder", Strings.NumberingRestartPerFolder);
        Assert.Equal("Restart in each folder", RulePanelText.PlainTitle(Strings.NumberingRestartPerFolder));
    }

    [Fact]
    public void AutoDateAndFolderName_UseTheStoryLetters()
    {
        Assert.Equal(
            new[] { 'D', 'S', 'F', 'P', 'B' },
            new[]
            {
                Strings.AutoDateTitle, Strings.AutoDateSource, Strings.AutoDateFormat, Strings.AutoDatePlace,
                Strings.AutoDateSeparator,
            }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.Equal(
            new[] { 'F', 'P', 'L', 'B' },
            new[]
            {
                Strings.FolderNameTitle, Strings.FolderNamePlace, Strings.FolderNameLevels,
                Strings.FolderNameSeparator,
            }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.All(
            new[]
            {
                Strings.AutoDateSourceModified, Strings.AutoDateSourceCreated, Strings.AutoDatePlaceBefore,
                Strings.AutoDatePlaceAfter, Strings.FolderNamePlaceBefore, Strings.FolderNamePlaceAfter,
                Strings.AutoDateExample,
            },
            word => Assert.DoesNotContain("&", word));
    }

    [Fact]
    public void AddAutoDateNumberingFolder_InStageOrder()
    {
        var add = new AddPanelModel { Enabled = true, Suffix = "-x" };
        var autoDate = new AutoDatePanelModel { Enabled = true, Placement = DatePlacement.Suffix };
        var numbering = new NumberingPanelModel { Enabled = true };
        var folderName = new FolderNamePanelModel { Enabled = true, Placement = FolderNamePlacement.Suffix };
        var host = new RulePanelHostModel(new IRulePanelModel[] { folderName, numbering, add, autoDate });

        Assert.Equal(
            new[] { RuleStage.Add, RuleStage.AutoDate, RuleStage.Numbering, RuleStage.FolderName },
            host.Rules.Select(r => r.Stage).ToArray());
        var item = new RenameItem("a.jpg", @"C:\Photos\Paris", modified: new DateTime(2024, 3, 9, 14, 5, 7));
        var results = new RenamePipeline(host.Rules).Run(new[] { item });
        Assert.Equal("a-x_2024-03-09_1_Paris.jpg", results[0].NewName);
    }

    [Fact]
    public void RegexAndMoveCopy_UseTheStoryLetters()
    {
        Assert.Equal(
            new[] { 'G', 'P', 'M', 'I', 'X' },
            new[]
            {
                Strings.RegexTitle, Strings.RegexPattern, Strings.RegexReplacement, Strings.RegexIgnoreCase,
                Strings.RegexIncludeExtension,
            }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.Equal(
            new[] { 'V', 'T', 'F', 'C', 'P', 'S', 'B' },
            new[]
            {
                Strings.MoveCopyTitle, Strings.MoveCopyMode, Strings.MoveCopySource, Strings.MoveCopyCount,
                Strings.MoveCopyTarget, Strings.MoveCopyPosition, Strings.MoveCopySeparator,
            }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.All(
            new[]
            {
                Strings.MoveCopyModeMove, Strings.MoveCopyModeCopy, Strings.MoveCopySourceStart,
                Strings.MoveCopySourceEnd, Strings.MoveCopyTargetStart, Strings.MoveCopyTargetEnd,
                Strings.MoveCopyTargetPosition,
            },
            word => Assert.DoesNotContain("&", word));
    }

    [Fact]
    public void RemovePanel_UsesTheStoryLetters()
    {
        Assert.Equal(
            new[] { 'E', 'F', 'L', 'M', 'T', 'H', 'X', 'W', 'C', 'B', 'A', 'K', 'D', 'S', 'N', 'G', 'R', 'O', 'P' },
            new[]
            {
                Strings.RemoveTitle, Strings.RemoveFirst, Strings.RemoveLast, Strings.RemoveFrom, Strings.RemoveTo,
                Strings.RemoveCharacters, Strings.RemoveText, Strings.RemoveWholeWord, Strings.RemoveMatchCase,
                Strings.RemoveCropBefore, Strings.RemoveCropAfter, Strings.RemoveKeepMarker, Strings.RemoveDigits,
                Strings.RemoveSymbols, Strings.RemoveAccents, Strings.RemoveHighCharacters, Strings.RemoveTrim,
                Strings.RemoveLeadingDots, Strings.RemoveDoubleSpaces,
            }.Select(MnemonicCheck.LetterOf).ToArray());
        Assert.All(
            new[]
            {
                Strings.RemoveGroupPosition, Strings.RemoveGroupText, Strings.RemoveGroupClasses,
                Strings.RemoveGroupTidy,
            },
            caption => Assert.DoesNotContain("&", caption));
    }

    [Fact]
    public void NameFixedAndExtensionLower_PreviewTripJpg()
    {
        var name = new NamePanelModel { Enabled = true, Mode = NameMode.Fixed, Text = "trip" };
        var extension = new ExtensionPanelModel { Enabled = true, Mode = ExtensionMode.Lower };
        var host = new RulePanelHostModel(new IRulePanelModel[] { extension, name });

        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("IMG_9.JPG", @"C:\f") });

        Assert.Equal("trip.jpg", results[0].NewName);
    }

    [Fact]
    public void AllElevenPanels_ListedInPipelineOrder()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[]
        {
            new ExtensionPanelModel(), new MoveCopyPanelModel(), new FolderNamePanelModel(), new AddPanelModel(),
            new NamePanelModel(), new NumberingPanelModel(), new CasePanelModel(), new AutoDatePanelModel(),
            new RegexPanelModel(), new ReplacePanelModel(), new RemovePanelModel(),
        });

        Assert.Equal(
            new[]
            {
                RuleStage.RegEx, RuleStage.Name, RuleStage.Replace, RuleStage.Case, RuleStage.Remove,
                RuleStage.MoveCopy, RuleStage.Add, RuleStage.AutoDate, RuleStage.Numbering, RuleStage.FolderName,
                RuleStage.Extension,
            },
            host.Panels.Select(p => p.Stage).ToArray());
    }

    [Fact]
    public void ReplaceRunsBeforeRemove()
    {
        var replace = new ReplacePanelModel { Enabled = true, Find = "a", With = "x" };
        var remove = new RemovePanelModel { Enabled = true, Characters = "x" };
        var host = new RulePanelHostModel(new IRulePanelModel[] { remove, replace });

        Assert.Equal(new[] { RuleStage.Replace, RuleStage.Remove }, host.Rules.Select(r => r.Stage).ToArray());
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("abc.txt", @"C:\f") });
        Assert.Equal("bc.txt", results[0].NewName);
    }

    [Fact]
    public void RegexRunsBeforeReplace()
    {
        var regex = new RegexPanelModel { Enabled = true, Pattern = @"IMG_(\d+)", Replacement = "Photo $1" };
        var replace = new ReplacePanelModel { Enabled = true, Find = "Photo", With = "Pic" };
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, regex });

        Assert.Equal(new[] { RuleStage.RegEx, RuleStage.Replace }, host.Rules.Select(r => r.Stage).ToArray());
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("IMG_042.jpg", @"C:\f") });
        Assert.Equal("Pic 042.jpg", results[0].NewName);
    }

    [Fact]
    public void RemoveMoveCopyAdd_InStageOrder()
    {
        var remove = new RemovePanelModel { Enabled = true, FirstText = "1" };
        var moveCopy = new MoveCopyPanelModel { Enabled = true, CountText = "2", Separator = "_" };
        var add = new AddPanelModel { Enabled = true, Prefix = "P-" };
        var host = new RulePanelHostModel(new IRulePanelModel[] { add, remove, moveCopy });

        Assert.Equal(
            new[] { RuleStage.Remove, RuleStage.MoveCopy, RuleStage.Add },
            host.Rules.Select(r => r.Stage).ToArray());
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("xabcd.txt", @"C:\f") });
        Assert.Equal("P-cd_ab.txt", results[0].NewName);
    }

    [Fact]
    public void EnabledPanelsWithNullRule_ChangeNothing()
    {
        var keep = new NamePanelModel { Enabled = true };
        var blankFixed = new NamePanelModel { Enabled = true, Mode = NameMode.Fixed, Text = "   " };
        var same = new ExtensionPanelModel { Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { keep, same });
        var blankHost = new RulePanelHostModel(new IRulePanelModel[] { blankFixed, same });

        Assert.Null(keep.BuildRule());
        Assert.Null(same.BuildRule());
        Assert.All(host.Rules, r => Assert.False(r.Enabled));
        Assert.All(blankHost.Rules, r => Assert.False(r.Enabled));
        Assert.Equal(
            RenameStatus.Unchanged,
            new RenamePipeline(host.Rules).Run(new[] { new RenameItem("a.txt", @"C:\f") })[0].Status);
        Assert.Equal(
            RenameStatus.Unchanged,
            new RenamePipeline(blankHost.Rules).Run(new[] { new RenameItem("a.txt", @"C:\f") })[0].Status);
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
        var name = new NamePanelModel { Mode = NameMode.Fixed, Text = text! };
        var extension = new ExtensionPanelModel { Mode = ExtensionMode.Fixed, Text = text! };
        var regex = new RegexPanelModel { Pattern = text!, Replacement = text! };
        var remove = new RemovePanelModel
        {
            FirstText = text!, LastText = text!, FromText = text!, ToText = text!, Characters = text!, Text = text!,
            WholeWord = true, MatchCase = true, CropBefore = text!, CropAfter = text!, KeepMarker = true,
            Digits = true, Symbols = true, Accents = true, HighCharacters = true, Trim = true, LeadingDots = true,
            DoubleSpaces = true,
        };
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
                name.BuildRule();
                extension.BuildRule();
                remove.BuildRule();
                regex.BuildRule();
                foreach (var target in MoveCopyPanelModel.Targets)
                {
                    new MoveCopyPanelModel { CountText = text!, Target = target, PositionText = text! }.BuildRule();
                }

                var autoDate = new AutoDatePanelModel { Format = text!, Separator = text! };
                autoDate.BuildRule();
                _ = autoDate.Example;
                _ = autoDate.ExampleText;
                _ = autoDate.FormatError;
                new FolderNamePanelModel { LevelsText = text!, Separator = text! }.BuildRule();
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
        var name = new NamePanelModel();
        var extension = new ExtensionPanelModel();
        var remove = new RemovePanelModel();
        var regex = new RegexPanelModel();
        var moveCopy = new MoveCopyPanelModel();
        foreach (IRulePanelModel model in new IRulePanelModel[]
        {
            replace, @case, add, name, extension, remove, regex, moveCopy,
        })
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
        Assert.Equal(NameMode.Keep, name.Mode);
        Assert.Equal(ExtensionMode.Same, extension.Mode);
        var engineRegex = new RegexRule("a", "");
        Assert.Equal(engineRegex.IgnoreCase, regex.IgnoreCase);
        Assert.Equal(engineRegex.IncludeExtension, regex.IncludeExtension);
        var engineMoveCopy = new MoveCopyRule();
        Assert.Equal(engineMoveCopy.Mode, moveCopy.Mode);
        Assert.Equal(engineMoveCopy.Source, moveCopy.Source);
        Assert.Equal(engineMoveCopy.Target, moveCopy.Target);
        Assert.Equal(engineMoveCopy.Separator, moveCopy.Separator);

        var autoDate = new AutoDatePanelModel();
        var folderName = new FolderNamePanelModel();
        foreach (IRulePanelModel model in new IRulePanelModel[] { autoDate, folderName })
        {
            Assert.True(model.IsDefault);
            Assert.False(model.Enabled);
        }

        var engineAutoDate = new AutoDateRule();
        Assert.Equal(engineAutoDate.Source, autoDate.Source);
        Assert.Equal(engineAutoDate.Format, autoDate.Format);
        Assert.Equal(engineAutoDate.Placement, autoDate.Placement);
        Assert.Equal(engineAutoDate.Separator, autoDate.Separator);
        var engineFolderName = new FolderNameRule();
        Assert.Equal(engineFolderName.Placement, folderName.Placement);
        Assert.Equal(engineFolderName.Separator, folderName.Separator);
        Assert.Equal(engineFolderName.Levels, folderName.Levels);
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
        Change(new NamePanelModel(), m => m.Mode = NameMode.Fixed),
        Change(new NamePanelModel(), m => m.Text = "x"),
        Change(new ExtensionPanelModel(), m => m.Mode = ExtensionMode.Upper),
        Change(new ExtensionPanelModel(), m => m.Text = "x"),
        Change(new AddPanelModel(), m => m.Prefix = "x"),
        Change(new AddPanelModel(), m => m.Insert = "x"),
        Change(new AddPanelModel(), m => m.InsertPositionText = "2"),
        Change(new AddPanelModel(), m => m.InsertFromEnd = true),
        Change(new AddPanelModel(), m => m.Suffix = "x"),
        Change(new AddPanelModel(), m => m.WordSpace = true),
        Change(new RemovePanelModel(), m => m.FirstText = "1"),
        Change(new RemovePanelModel(), m => m.LastText = "1"),
        Change(new RemovePanelModel(), m => m.FromText = "1"),
        Change(new RemovePanelModel(), m => m.ToText = "1"),
        Change(new RemovePanelModel(), m => m.Characters = "x"),
        Change(new RemovePanelModel(), m => m.Text = "x"),
        Change(new RemovePanelModel(), m => m.WholeWord = true),
        Change(new RemovePanelModel(), m => m.MatchCase = true),
        Change(new RemovePanelModel(), m => m.CropBefore = "x"),
        Change(new RemovePanelModel(), m => m.CropAfter = "x"),
        Change(new RemovePanelModel(), m => m.KeepMarker = true),
        Change(new RemovePanelModel(), m => m.Digits = true),
        Change(new RemovePanelModel(), m => m.Symbols = true),
        Change(new RemovePanelModel(), m => m.Accents = true),
        Change(new RemovePanelModel(), m => m.HighCharacters = true),
        Change(new RemovePanelModel(), m => m.Trim = true),
        Change(new RemovePanelModel(), m => m.LeadingDots = true),
        Change(new RemovePanelModel(), m => m.DoubleSpaces = true),
        Change(new RegexPanelModel(), m => m.Pattern = "x"),
        Change(new RegexPanelModel(), m => m.Replacement = "x"),
        Change(new RegexPanelModel(), m => m.IgnoreCase = true),
        Change(new RegexPanelModel(), m => m.IncludeExtension = true),
        Change(new MoveCopyPanelModel(), m => m.Mode = MoveCopyMode.Copy),
        Change(new MoveCopyPanelModel(), m => m.Source = MoveCopySource.End),
        Change(new MoveCopyPanelModel(), m => m.CountText = "2"),
        Change(new MoveCopyPanelModel(), m => m.Target = MoveCopyTarget.Start),
        Change(new MoveCopyPanelModel(), m => m.PositionText = "2"),
        Change(new MoveCopyPanelModel(), m => m.Separator = "x"),
        Change(new AutoDatePanelModel(), m => m.Source = DateSource.Created),
        Change(new AutoDatePanelModel(), m => m.Format = "yyyy"),
        Change(new AutoDatePanelModel(), m => m.Placement = DatePlacement.Suffix),
        Change(new AutoDatePanelModel(), m => m.Separator = "x"),
        Change(new FolderNamePanelModel(), m => m.Placement = FolderNamePlacement.Suffix),
        Change(new FolderNamePanelModel(), m => m.LevelsText = "2"),
        Change(new FolderNamePanelModel(), m => m.Separator = "x"),
        Change(new NumberingPanelModel(), m => m.Numerals = NumeralSystem.Hexadecimal),
        Change(new NumberingPanelModel(), m => m.UpperCase = false),
        Change(new NumberingPanelModel(), m => m.RestartPerFolder = true),
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
