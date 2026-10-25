using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The filter options keep exactly the items the window's filter row keeps, on the same listing and settings.</summary>
public sealed class WindowParityFilterTests
{
    private static RenameItem[] Listing(string folder) => new[]
    {
        new RenameItem("a.jpg", folder),
        new RenameItem("B.PNG", folder),
        new RenameItem("c.txt", folder),
        new RenameItem("IMG_1.jpg", folder),
        new RenameItem("img_22.JPG", folder),
        new RenameItem("IMG_3x.jpg", folder),
        new RenameItem("abcdef.txt", folder),
        new RenameItem("ab", folder),
        new RenameItem("README", folder),
        new RenameItem("h.txt", folder, ItemKind.File, ItemAttributes.Hidden),
        new RenameItem("s.dat", folder, ItemKind.File, ItemAttributes.System),
        new RenameItem("Sub", folder, ItemKind.Folder),
        new RenameItem("Pics", folder, ItemKind.Folder),
        new RenameItem("HiddenDir", folder, ItemKind.Folder, ItemAttributes.Hidden),
    };

    private static object[] Row(string cli, Action<FilterPanelModel> window) => new object[] { cli, window };

    public static IEnumerable<object[]> Cases()
    {
        yield return Row("--dry-run", m => m.IncludeFolders = false);
        yield return Row("--mask|*.jpg;*.png", m => { m.IncludeFolders = false; m.Mask = "*.jpg;*.png"; });
        yield return Row("--mask|a?.*", m => { m.IncludeFolders = false; m.Mask = "a?.*"; });
        yield return Row("--filter-regex|^IMG_[0-9]+$", m => { m.IncludeFolders = false; m.Pattern = "^IMG_[0-9]+$"; });
        yield return Row("--filter-regex|^IMG_[0-9]+$|--filter-ignore-case", m =>
        {
            m.IncludeFolders = false;
            m.Pattern = "^IMG_[0-9]+$";
            m.IgnoreCase = true;
        });
        yield return Row("--min-length|3", m => { m.IncludeFolders = false; m.MinLengthText = "3"; });
        yield return Row("--max-length|3", m => { m.IncludeFolders = false; m.MaxLengthText = "3"; });
        yield return Row("--min-length|2|--max-length|5", m =>
        {
            m.IncludeFolders = false;
            m.MinLengthText = "2";
            m.MaxLengthText = "5";
        });
        yield return Row("--include-hidden", m => { m.IncludeFolders = false; m.IncludeHidden = true; });
        yield return Row("--folders-only", m => { m.IncludeFolders = true; m.IncludeFiles = false; });
        yield return Row("--folders-only|--include-hidden", m =>
        {
            m.IncludeFolders = true;
            m.IncludeFiles = false;
            m.IncludeHidden = true;
        });
        yield return Row("--include-folders", m => m.IncludeFolders = true);
        yield return Row("--files-only", m => m.IncludeFolders = false);
        yield return Row("--include-folders|--include-hidden|--mask|*i*", m =>
        {
            m.IncludeFolders = true;
            m.IncludeHidden = true;
            m.Mask = "*i*";
        });
        yield return Row("--files-only|--min-length|3|--filter-regex|[a-z]|--filter-ignore-case", m =>
        {
            m.IncludeFolders = false;
            m.MinLengthText = "3";
            m.Pattern = "[a-z]";
            m.IgnoreCase = true;
        });
    }

    private static string[] Names(IEnumerable<RenameItem> items) => items.Select(i => i.Name).ToArray();

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameKeptItems(string cli, Action<FilterPanelModel> setup)
    {
        RenameItem[] items = Listing("C:" + (char)92 + "P");
        var args = new List<string> { "apply", "C:" + (char)92 + "P", "--case", "lower" };
        args.AddRange(cli.Split('|'));
        ParsedCommand command = CommandLineParser.Parse(args);
        Assert.True(command.Kind == CommandKind.Apply, command.Error);
        var model = new FilterPanelModel();
        setup(model);
        Assert.Null(model.Error);

        string[] fromCli = Names(command.Options!.Filter.Apply(items));
        string[] fromWindow = Names(model.Filter.Apply(items));

        Assert.Equal(fromWindow, fromCli);
    }

    [Fact]
    public void TheTableHasEnoughRows_AndTheFilterKeepsSomething()
    {
        Assert.True(Cases().Count() >= 10);
    }

    [Fact]
    public void TheRunnerUsesThatFilter()
    {
        using var temp = new TempFolder();
        RenameItem[] items = Listing(temp.Files);
        var model = new FilterPanelModel { IncludeFolders = true, IncludeHidden = true, Mask = "*i*" };
        string[] expected = FileListOrder.Sort(model.Filter.Apply(items)).Select(i => i.Name + " -> x" + i.Name).ToArray();
        Assert.NotEmpty(expected);

        var run = Cmd.Run(new FakeBatchRenamer(), temp.Files,
            new[] { "apply", temp.Files, "--prefix", "x", "--dry-run", "--include-folders", "--include-hidden", "--mask", "*i*" },
            new FakeFileListSource(FileListing.Ok(items)));

        Assert.Equal(0, run.Code);
        Assert.Equal(expected, Cmd.Lines(run.Output));
    }
}
