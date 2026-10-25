using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The Name, Extension and Remove options give the window's own names on the same files and settings.</summary>
public sealed class WindowParityNameExtRemoveTests
{
    private static readonly string[] Plain = { "IMG_001.JPG", "abc.txt", "README", "Trip - a.txt", "a  b.txt" };

    public static IEnumerable<object[]> Cases()
    {
        object[] Row(string[] files, string args, Func<IRulePanelModel[]> window) =>
            new object[] { string.Join("\n", files), args, window };

        string[] one = { "IMG_001.JPG" };
        string[] two = { "abcdef.txt", "Trip - a.txt" };

        yield return Row(one, "--name-fixed|Trip", () => new IRulePanelModel[]
            { new NamePanelModel { Mode = NameMode.Fixed, Text = "Trip", Enabled = true } });
        yield return Row(Plain, "--name-reverse", () => new IRulePanelModel[]
            { new NamePanelModel { Mode = NameMode.Reverse, Enabled = true } });
        yield return Row(one, "--name-empty", () => new IRulePanelModel[]
            { new NamePanelModel { Mode = NameMode.Remove, Enabled = true } });
        yield return Row(Plain, "--ext-lower", () => new IRulePanelModel[]
            { new ExtensionPanelModel { Mode = ExtensionMode.Lower, Enabled = true } });
        yield return Row(Plain, "--ext-upper", () => new IRulePanelModel[]
            { new ExtensionPanelModel { Mode = ExtensionMode.Upper, Enabled = true } });
        yield return Row(Plain, "--ext-fixed|.md", () => new IRulePanelModel[]
            { new ExtensionPanelModel { Mode = ExtensionMode.Fixed, Text = ".md", Enabled = true } });
        yield return Row(Plain, "--ext-extra|bak", () => new IRulePanelModel[]
            { new ExtensionPanelModel { Mode = ExtensionMode.Extra, Text = "bak", Enabled = true } });
        yield return Row(Plain, "--ext-remove", () => new IRulePanelModel[]
            { new ExtensionPanelModel { Mode = ExtensionMode.Remove, Enabled = true } });

        yield return RemoveRow(Plain, "--remove-first|3", m => m.FirstText = "3");
        yield return RemoveRow(Plain, "--remove-last|1", m => m.LastText = "1");
        yield return RemoveRow(two, "--remove-from|2|--remove-to|3", m => { m.FromText = "2"; m.ToText = "3"; });
        yield return RemoveRow(Plain, "--remove-chars|aeiou", m => m.Characters = "aeiou");
        yield return RemoveRow(Plain, "--remove-text|img", m => m.Text = "img");
        yield return RemoveRow(Plain, "--remove-text|a|--remove-whole-word", m => { m.Text = "a"; m.WholeWord = true; });
        yield return RemoveRow(Plain, "--remove-text|IMG|--remove-match-case", m => { m.Text = "IMG"; m.MatchCase = true; });
        yield return RemoveRow(Plain, "--remove-before|-", m => m.CropBefore = "-");
        yield return RemoveRow(Plain, "--remove-after|_", m => m.CropAfter = "_");
        yield return RemoveRow(Plain, "--remove-before|-|--remove-keep-marker", m => { m.CropBefore = "-"; m.KeepMarker = true; });
        yield return RemoveRow(Plain, "--remove-digits", m => m.Digits = true);
        yield return RemoveRow(Plain, "--remove-symbols", m => m.Symbols = true);
        yield return RemoveRow(new[] { "caf\u00E9.txt" }, "--remove-accents", m => m.Accents = true);
        yield return RemoveRow(new[] { "caf\u00E9.txt", "a\u4E2Db.txt" }, "--remove-high-chars", m => m.HighCharacters = true);
        yield return RemoveRow(new[] { " a .txt", "b.txt" }, "--remove-trim", m => m.Trim = true);
        yield return RemoveRow(new[] { "..a.txt", "b.txt" }, "--remove-leading-dots", m => m.LeadingDots = true);
        yield return RemoveRow(Plain, "--remove-double-spaces", m => m.DoubleSpaces = true);
        yield return RemoveRow(new[] { "\U0001F600a.txt" }, "--remove-first|1", m => m.FirstText = "1");

        // Pairs.
        yield return RemoveRow(Plain, "--remove-digits|--remove-symbols", m => { m.Digits = true; m.Symbols = true; });
        yield return RemoveRow(Plain, "--remove-trim|--remove-double-spaces", m => { m.Trim = true; m.DoubleSpaces = true; });
        yield return RemoveRow(Plain, "--remove-first|1|--remove-last|1", m => { m.FirstText = "1"; m.LastText = "1"; });
        yield return RemoveRow(Plain, "--remove-accents|--remove-high-chars", m => { m.Accents = true; m.HighCharacters = true; });
        yield return RemoveRow(Plain, "--remove-leading-dots|--remove-digits", m => { m.LeadingDots = true; m.Digits = true; });
        yield return RemoveRow(Plain, "--remove-after|_|--remove-keep-marker", m => { m.CropAfter = "_"; m.KeepMarker = true; });
        yield return RemoveRow(Plain, "--remove-chars|a|--remove-match-case", m => { m.Characters = "a"; m.MatchCase = true; });
        yield return Row(Plain, "--name-reverse|--ext-upper", () => new IRulePanelModel[]
        {
            new NamePanelModel { Mode = NameMode.Reverse, Enabled = true },
            new ExtensionPanelModel { Mode = ExtensionMode.Upper, Enabled = true },
        });
    }

    private static object[] RemoveRow(string[] files, string args, Action<RemovePanelModel> set) =>
        new object[]
        {
            string.Join("\n", files), args, (Func<IRulePanelModel[]>)(() =>
            {
                var model = new RemovePanelModel { Enabled = true };
                set(model);
                return new IRulePanelModel[] { model };
            }),
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CliEqualsTheWindow(string fileNames, string options, Func<IRulePanelModel[]> window)
    {
        using var temp = new TempFolder();
        foreach (string name in fileNames.Split('\n'))
        {
            temp.File(name);
        }

        var files = new FileListModel(new DiskFileListSource());
        var preview = new PreviewModel(new RulePanelHostModel(window()), files);
        await files.LoadAsync(temp.Files);
        await preview.Pending;
        Assert.Equal(FileListState.Loaded, files.State);
        var windowLines = new List<string>();
        bool windowProblem = false;
        for (int row = 0; row < files.Items.Count; row++)
        {
            PreviewCell cell = preview.CellForRow(row);
            if (cell.Kind == PreviewCellKind.Changed)
            {
                windowLines.Add(files.Items[row].Name + " -> " + cell.Text);
            }
            else if (cell.Kind != PreviewCellKind.Unchanged)
            {
                windowProblem = true;
            }
        }

        var args = new List<string> { "apply", temp.Files, "--dry-run" };
        args.AddRange(options.Split('|'));
        var run = Cmd.Run(new FakeBatchRenamer(), temp.Files, args);

        if (windowProblem)
        {
            Assert.Equal(2, run.Code);
            return;
        }

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(
            windowLines.Count == 0 ? new[] { Strings.NothingToRename } : windowLines.ToArray(), Cmd.Lines(run.Output));
    }
}
