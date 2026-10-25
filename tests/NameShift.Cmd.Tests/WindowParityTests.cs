using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The command line's new names equal the window's preview on the same folder and settings.</summary>
public sealed class WindowParityTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public WindowParityTests()
    {
        foreach (string name in new[]
        {
            "IMG_001.JPG", "img_002.jpeg", "My IMG.Img", "file2.txt", "file10.txt", "\u00c9t\u00e9 img.png", "x.tar.gz",
            "MyHolidayPic.jpg",
        })
        {
            _temp.File(name);
        }

        _temp.File("IMG_h.JPG", FileAttributes.Hidden);
    }

    public void Dispose() => _temp.Dispose();

    private static CaseMode? ToMode(string? caseName)
    {
        switch (caseName)
        {
            case null:
                return null;
            case "lower":
                return CaseMode.Lower;
            case "upper":
                return CaseMode.Upper;
            case "title":
                return CaseMode.Title;
            case "sentence":
                return CaseMode.Sentence;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown case.");
        }
    }

    private Task<string[]> WindowLinesAsync(string? find, string? with, CaseMode? mode)
    {
        var replace = new ReplacePanelModel { Find = find ?? "", With = with ?? "", Enabled = true };
        var casePanel = new CasePanelModel { Mode = mode ?? CaseMode.Same, Enabled = true };
        return WindowLinesAsync(new IRulePanelModel[] { replace, casePanel });
    }

    private async Task<string[]> WindowLinesAsync(IRulePanelModel[] panels)
    {
        var files = new FileListModel(new DiskFileListSource());
        var host = new RulePanelHostModel(panels);
        var preview = new PreviewModel(host, files);

        await files.LoadAsync(_temp.Files);
        await preview.Pending;

        Assert.Equal(FileListState.Loaded, files.State);
        Assert.True(preview.IsCurrent);
        var lines = new List<string>();
        for (int row = 0; row < files.Items.Count; row++)
        {
            PreviewCell cell = preview.CellForRow(row);
            if (cell.Kind == PreviewCellKind.Changed)
            {
                lines.Add(files.Items[row].Name + " -> " + cell.Text);
            }
        }

        return lines.ToArray();
    }

    [Theory]
    [InlineData("img", "Pic", null)]
    [InlineData(null, null, "lower")]
    [InlineData(null, null, "upper")]
    [InlineData(null, null, "title")]
    [InlineData(null, null, "sentence")]
    [InlineData("img", "Pic", "title")]
    public async Task CliNamesEqualTheWindows(string? find, string? with, string? caseName)
    {
        string[] window = await WindowLinesAsync(find, with, ToMode(caseName));

        var args = new List<string> { "apply", _temp.Files, "--dry-run" };
        if (find != null)
        {
            args.AddRange(new[] { "--replace", find, with! });
        }

        if (caseName != null)
        {
            args.AddRange(new[] { "--case", caseName });
        }

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, args);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        string[] cli = Cmd.Lines(run.Output);
        Assert.NotEmpty(cli);
        Assert.Equal(window, cli);
        Assert.DoesNotContain(cli, line => line.StartsWith("IMG_h.JPG", StringComparison.Ordinal));
    }

    private string[] CliLines(string options)
    {
        var args = new List<string> { "apply", _temp.Files, "--dry-run" };
        args.AddRange(options.Split(' '));

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files, args);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        return Cmd.Lines(run.Output);
    }

    /// <summary>The window models the parser filled, copied onto fresh models in the parser's own order (Numerals
    /// before UpperCase and StartText), so the parsed values are what drives the window.</summary>
    private IRulePanelModel[] WindowModelsFor(string options)
    {
        var args = new List<string> { "apply", _temp.Files };
        args.AddRange(options.Split(' '));
        ParsedCommand command = CommandLineParser.Parse(args);
        Assert.Equal(CommandKind.Apply, command.Kind);
        ApplyPanels panels = command.Options!.Panels;

        var models = new List<IRulePanelModel>();
        if (panels.Add != null)
        {
            models.Add(new AddPanelModel
            {
                Prefix = panels.Add.Prefix,
                Suffix = panels.Add.Suffix,
                Insert = panels.Add.Insert,
                InsertPositionText = panels.Add.InsertPositionText,
                InsertFromEnd = panels.Add.InsertFromEnd,
                WordSpace = panels.Add.WordSpace,
                Enabled = true,
            });
        }

        if (panels.Numbering != null)
        {
            NumberingPanelModel from = panels.Numbering;
            var numbering = new NumberingPanelModel { Mode = from.Mode, Numerals = from.Numerals };
            numbering.UpperCase = from.UpperCase;
            numbering.StartText = from.StartText;
            numbering.StepText = from.StepText;
            numbering.PadText = from.PadText;
            numbering.Separator = from.Separator;
            numbering.InsertPositionText = from.InsertPositionText;
            numbering.Enabled = true;
            models.Add(numbering);
        }

        return models.ToArray();
    }

    [Theory]
    [InlineData("--prefix Trip_")]
    [InlineData("--suffix _v2")]
    [InlineData("--insert X 3")]
    [InlineData("--insert X 2 --from-end")]
    [InlineData("--word-space")]
    [InlineData("--number")]
    [InlineData("--number-mode prefix --number-pad 3")]
    [InlineData("--number-mode insert --number-position 2 --number-separator -")]
    [InlineData("--number-start 5 --number-step 10")]
    [InlineData("--number-case lower --number-system hex --number-start ff")]
    [InlineData("--number-case lower --number-system roman")]
    [InlineData("--number-system letters")]
    [InlineData("--prefix p_ --number --suffix _s")]
    public async Task CliAddAndNumberingEqualTheWindows(string options)
    {
        string[] window = await WindowLinesAsync(WindowModelsFor(options));

        string[] cli = CliLines(options);

        Assert.NotEmpty(cli);
        Assert.Equal(window, cli);
    }

    [Fact]
    public async Task HandBuiltAddModel_EqualsTheCli()
    {
        var add = new AddPanelModel { Prefix = "Trip_", Suffix = "_v2", Insert = "X", InsertPositionText = "2", InsertFromEnd = true, Enabled = true };

        string[] window = await WindowLinesAsync(new IRulePanelModel[] { add });

        Assert.Equal(window, CliLines("--prefix Trip_ --suffix _v2 --insert X 2 --from-end"));
    }

    [Fact]
    public async Task HandBuiltNumberingModel_EqualsTheCli()
    {
        var numbering = new NumberingPanelModel { Mode = NumberingMode.Prefix, Numerals = NumeralSystem.Hexadecimal };
        numbering.UpperCase = false;
        numbering.StartText = "ff";
        numbering.PadText = "4";
        numbering.Enabled = true;

        string[] window = await WindowLinesAsync(new IRulePanelModel[] { numbering });

        Assert.Equal(
            window,
            CliLines("--number-case lower --number-start ff --number-pad 4 --number-system hex --number-mode prefix"));
    }
}
