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

    private async Task<string[]> WindowLinesAsync(string? find, string? with, CaseMode? mode)
    {
        var files = new FileListModel(new DiskFileListSource());
        var replace = new ReplacePanelModel { Find = find ?? "", With = with ?? "", Enabled = true };
        var casePanel = new CasePanelModel { Mode = mode ?? CaseMode.Same, Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, casePanel });
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
}
