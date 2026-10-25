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

/// <summary>The RegEx, Move or copy, Auto date and Folder name options give the window's own names on the same files
/// and settings.</summary>
public sealed class WindowParityRegexMoveDateFolderTests
{
    private static readonly string[] Plain = { "IMG_001.JPG", "abc.txt", "README", "Trip - a.txt", "a  b.txt" };

    private static readonly DateTime Written = new DateTime(2024, 3, 9, 14, 5, 7, DateTimeKind.Local);
    private static readonly DateTime Created = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Local);

    public static IEnumerable<object[]> Cases()
    {
        // Regex.
        yield return RegexRow(Plain, @"--regex|(\d+)|[$1]", m => { m.Pattern = @"(\d+)"; m.Replacement = "[$1]"; });
        yield return RegexRow(Plain, @"--regex|(?<n>\d+)|[${n}]", m => { m.Pattern = @"(?<n>\d+)"; m.Replacement = "[${n}]"; });
        yield return RegexRow(Plain, @"--regex|\d+|", m => m.Pattern = @"\d+");
        yield return RegexRow(Plain, "--regex|t|T|--regex-extension", m =>
        {
            m.Pattern = "t";
            m.Replacement = "T";
            m.IncludeExtension = true;
        });
        yield return RegexRow(Plain, "--regex|img|X|--regex-ignore-case", m =>
        {
            m.Pattern = "img";
            m.Replacement = "X";
            m.IgnoreCase = true;
        });
        yield return RegexRow(Plain, "--regex|IMG|X", m => { m.Pattern = "IMG"; m.Replacement = "X"; });
        yield return RegexRow(Plain, "--regex|-|_", m => { m.Pattern = "-"; m.Replacement = "_"; });

        // Move and copy.
        string[] words = { "abcdef.txt", "caf00E9.txt", "README" };
        yield return MoveRow(words, "--move|3", m => { m.Mode = MoveCopyMode.Move; m.CountText = "3"; });
        yield return MoveRow(words, "--copy|3", m => { m.Mode = MoveCopyMode.Copy; m.CountText = "3"; });
        yield return MoveRow(words, "--move|3|--move-from|end|--move-to|start", m =>
        {
            m.CountText = "3";
            m.Source = MoveCopySource.End;
            m.Target = MoveCopyTarget.Start;
        });
        yield return MoveRow(words, "--copy|2|--move-from|end|--move-to|end", m =>
        {
            m.Mode = MoveCopyMode.Copy;
            m.CountText = "2";
            m.Source = MoveCopySource.End;
            m.Target = MoveCopyTarget.End;
        });
        yield return MoveRow(words, "--move|2|--move-to|start", m => { m.CountText = "2"; m.Target = MoveCopyTarget.Start; });
        yield return MoveRow(words, "--move|2|--move-to|position|--move-position|2", m =>
        {
            m.CountText = "2";
            m.Target = MoveCopyTarget.Position;
            m.PositionText = "2";
        });
        yield return MoveRow(words, "--copy|2|--move-to|position|--move-position|1|--move-separator|-", m =>
        {
            m.Mode = MoveCopyMode.Copy;
            m.CountText = "2";
            m.Target = MoveCopyTarget.Position;
            m.PositionText = "1";
            m.Separator = "-";
        });
        yield return MoveRow(words, "--move|1|--move-separator|_", m => { m.CountText = "1"; m.Separator = "_"; });
        yield return MoveRow(words, "--move|2|--move-to|start|--move-position|x", m =>
        {
            m.CountText = "2";
            m.Target = MoveCopyTarget.Start;
            m.PositionText = "x";
        });
        yield return MoveRow(new[] { "caf00E9.txt" }, "--move|1", m => m.CountText = "1");
        yield return MoveRow(new[] { "\U0001F600abc.txt" }, "--move|1", m => m.CountText = "1");
        yield return MoveRow(new[] { "\U0001F600abc.txt" }, "--copy|1|--move-from|start|--move-to|end", m =>
        {
            m.Mode = MoveCopyMode.Copy;
            m.CountText = "1";
        });

        // Auto date (times set in local time).
        yield return DateRow("--date", m => { });
        yield return DateRow("--date-source|modified", m => m.Source = DateSource.Modified);
        yield return DateRow("--date-source|created", m => m.Source = DateSource.Created);
        yield return DateRow("--date-format|yyyyMMdd", m => m.Format = "yyyyMMdd");
        yield return DateRow("--date-format|yyyy-MM-dd HH.mm", m => m.Format = "yyyy-MM-dd HH.mm");
        yield return DateRow("--date-placement|suffix", m => m.Placement = DatePlacement.Suffix);
        yield return DateRow("--date-placement|prefix", m => m.Placement = DatePlacement.Prefix);
        yield return DateRow("--date-separator|-", m => m.Separator = "-");
        yield return DateRow("--date-separator|", m => m.Separator = "");
        yield return DateRow("--date-source|created|--date-format|yyyyMMdd|--date-placement|suffix|--date-separator|.", m =>
        {
            m.Source = DateSource.Created;
            m.Format = "yyyyMMdd";
            m.Placement = DatePlacement.Suffix;
            m.Separator = ".";
        });

        // Append folder name (the temp folder, never a drive root).
        yield return FolderRow("--folder-name", m => { });
        yield return FolderRow("--folder-levels|2", m => m.LevelsText = "2");
        yield return FolderRow("--folder-placement|suffix", m => m.Placement = FolderNamePlacement.Suffix);
        yield return FolderRow("--folder-placement|prefix", m => m.Placement = FolderNamePlacement.Prefix);
        yield return FolderRow("--folder-separator|-", m => m.Separator = "-");
        yield return FolderRow("--folder-separator|", m => m.Separator = "");
        yield return FolderRow("--folder-levels|2|--folder-placement|suffix|--folder-separator|+", m =>
        {
            m.LevelsText = "2";
            m.Placement = FolderNamePlacement.Suffix;
            m.Separator = "+";
        });

        // Panels together.
        yield return new object[]
        {
            string.Join("\n", Plain), "--regex|a|b|--move|1|--folder-name", false, (Func<IRulePanelModel[]>)(() =>
                new IRulePanelModel[]
                {
                    new RegexPanelModel { Pattern = "a", Replacement = "b", Enabled = true },
                    new MoveCopyPanelModel { CountText = "1", Enabled = true },
                    new FolderNamePanelModel { Enabled = true },
                }),
        };
    }

    private static object[] RegexRow(string[] files, string args, Action<RegexPanelModel> set) =>
        Row(files, args, false, () =>
        {
            var model = new RegexPanelModel { Enabled = true };
            set(model);
            return model;
        });

    private static object[] MoveRow(string[] files, string args, Action<MoveCopyPanelModel> set) =>
        Row(files, args, false, () =>
        {
            var model = new MoveCopyPanelModel { Enabled = true };
            set(model);
            return model;
        });

    private static object[] DateRow(string args, Action<AutoDatePanelModel> set) =>
        Row(Plain, args, true, () =>
        {
            var model = new AutoDatePanelModel { Enabled = true };
            set(model);
            return model;
        });

    private static object[] FolderRow(string args, Action<FolderNamePanelModel> set) =>
        Row(Plain, args, false, () =>
        {
            var model = new FolderNamePanelModel { Enabled = true };
            set(model);
            return model;
        });

    private static object[] Row(string[] files, string args, bool setTimes, Func<IRulePanelModel> model) =>
        new object[]
        {
            string.Join("\n", files), args, setTimes, (Func<IRulePanelModel[]>)(() => new[] { model() }),
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CliEqualsTheWindow(string fileNames, string options, bool setTimes, Func<IRulePanelModel[]> window)
    {
        using var temp = new TempFolder();
        foreach (string name in fileNames.Split('\n'))
        {
            string path = temp.File(name);
            if (setTimes)
            {
                // Written last: writing a file can change its times.
                File.SetCreationTime(path, Created);
                File.SetLastWriteTime(path, Written);
                File.SetCreationTime(path, Created);
            }
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
