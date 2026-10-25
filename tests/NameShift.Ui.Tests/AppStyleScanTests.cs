using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>Reads the .cs files of NameShift.App so the window style cannot drift: gaps from <see cref="Spacing"/>, the one system
/// font, system colours plus the Palette. The scanner itself is tested on snippets first.</summary>
public sealed class AppStyleScanTests
{
    private static readonly string[] AccentSourceFiles = { "AccentSource.cs", "Palette.cs", "RulePanelFrame.cs", "ButtonIcons.cs" };

    // --- the scanner on snippets ---

    [Theory]
    [InlineData("var p = new Padding(3);", 1)]
    [InlineData("var p = new Padding(0, 0, 8, 0);", 1)]
    [InlineData("var p = new Padding(LogicalToDeviceUnits(20));", 1)]
    [InlineData("var p = new Padding(LogicalToDeviceUnits(Spacing.Small), 3);", 1)]
    [InlineData("var p = new Padding(0, 0, LogicalToDeviceUnits(Spacing.Medium), 3);", 1)]
    [InlineData("var p = new Padding(0, 0, LogicalToDeviceUnits(Spacing.Medium), 0);", 0)]
    [InlineData("var p = new Padding(LogicalToDeviceUnits(Spacing.Large + Spacing.Medium));", 0)]
    [InlineData("var p = new Padding(x2);", 0)]
    [InlineData("var p = new Padding(0x10);", 1)]
    [InlineData("var p = new System.Windows.Forms.Padding(3);", 1)]
    [InlineData("var p = Padding.Empty;", 0)]
    [InlineData("_error.SetIconPadding(field, 2);", 0)]
    public void PaddingScanner_Flags(string source, int expectedFlags)
    {
        Assert.Equal(expectedFlags, StyleScanner.FindPaddingLiterals(source).Count);
    }

    [Fact]
    public void PaddingScanner_FlagsTwoLineExpressionOnItsFirstLine()
    {
        List<(int Line, string Message)> found = StyleScanner.FindPaddingLiterals("int a;\nvar p = new Padding(\n    3);\n");

        Assert.Equal(2, Assert.Single(found).Line);
    }

    [Theory]
    [InlineData("var f = new Font(\"Arial\", 9);", 1)]
    [InlineData("var f = new Font(@\"Arial\", 9);", 1)]
    [InlineData("var f = new FontFamily(\"Segoe UI\");", 1)]
    [InlineData("var f = new System.Drawing.Font(\"Arial\", 9);", 1)]
    [InlineData("var f = new System.Drawing.FontFamily(\"Arial\");", 1)]
    [InlineData("var f = new Font(_list.Font, FontStyle.Bold);", 0)]
    public void FontScanner_Flags(string source, int expectedFlags)
    {
        Assert.Equal(expectedFlags, StyleScanner.FindNamedFonts(source).Count);
    }

    [Theory]
    [InlineData("var c = Color.Red;", 1)]
    [InlineData("var c = Color.FromArgb(1, 2, 3);", 1)]
    [InlineData("var c = ColorTranslator.FromHtml(x);", 1)]
    [InlineData("var b = Brushes.Red;", 1)]
    [InlineData("var p = Pens.Black;", 1)]
    [InlineData("var k = KnownColor.Red;", 1)]
    [InlineData("var c = SystemColors.Highlight;", 0)]
    [InlineData("var c = Color.Empty;", 0)]
    [InlineData("var c = Color.Transparent;", 0)]
    [InlineData("_check.ForeColor = x;", 0)]
    public void ColourScanner_Flags(string source, int expectedFlags)
    {
        Assert.Equal(expectedFlags, StyleScanner.FindColours(source).Count);
    }

    // --- the App sources ---

    [Fact]
    public void Padding_UsesSpacingOnly()
    {
        AssertNoFindings(AppSources(), (_, text) => StyleScanner.FindPaddingLiterals(text));
    }

    [Fact]
    public void Fonts_NoNamedFontOrFamily()
    {
        AssertNoFindings(AppSources(), (_, text) => StyleScanner.FindNamedFonts(text));
    }

    [Fact]
    public void Colours_OnlySystemAccentOrPalette()
    {
        AssertNoFindings(
            AppSources(),
            (path, text) => Path.GetFileName(path) == "Palette.cs" ? new List<(int Line, string Message)>() : StyleScanner.FindColours(text));
    }

    [Fact]
    public void AccentSource_OnlyInItsFourFiles()
    {
        string accentSource = Path.Combine(AppRoot(), "Style", "AccentSource.cs");
        Assert.True(File.Exists(accentSource), "missing " + accentSource);
        Assert.Contains("ColorizationColor", File.ReadAllText(accentSource));

        var outside = new List<string>();
        foreach (string path in AppSources())
        {
            string name = Path.GetFileName(path);
            if (Array.IndexOf(AccentSourceFiles, name) >= 0)
            {
                continue;
            }

            foreach (int line in StyleScanner.FindWord(File.ReadAllText(path), "AccentSource"))
            {
                outside.Add(Relative(path) + ":" + line + ": AccentSource is only for " + string.Join(", ", AccentSourceFiles));
            }
        }

        Assert.True(outside.Count == 0, string.Join(Environment.NewLine, outside));
    }

    [Fact]
    public void AccentSource_IsReadOnly()
    {
        string text = File.ReadAllText(Path.Combine(AppRoot(), "Style", "AccentSource.cs"));
        string[] forbidden = { "SetValue(", "CreateSubKey(", "DeleteValue(", "DeleteSubKey", "RegistryKey.OpenBaseKey", "LocalMachine" };

        foreach (string word in forbidden)
        {
            Assert.DoesNotContain(word, text);
        }

        Assert.False(Regex.IsMatch(text, @"OpenSubKey\([^)]*,\s*true"), "OpenSubKey must open read only");

        // The one key and the one value, nothing else.
        Assert.Single(Regex.Matches(text, @"OpenSubKey\("));
        Assert.Contains(@"OpenSubKey(@""Software\Microsoft\Windows\DWM"")", text);
        Assert.Single(Regex.Matches(text, @"GetValue\("));
        Assert.Contains(@"GetValue(""ColorizationColor"")", text);
    }

    // --- helpers ---

    private static void AssertNoFindings(
        IReadOnlyList<string> files,
        Func<string, string, List<(int Line, string Message)>> scan)
    {
        var failures = new List<string>();
        foreach (string path in files)
        {
            foreach ((int line, string message) in scan(path, File.ReadAllText(path)))
            {
                failures.Add(Relative(path) + ":" + line + ": " + message);
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string AppRoot() => Path.Combine(RepositoryFiles.Root(), "src", "NameShift.App");

    private static IReadOnlyList<string> AppSources()
    {
        string root = AppRoot();
        List<string> files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !RepositoryFiles.IsBuildOutput(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        Assert.True(files.Count >= 20, "only " + files.Count + " .cs files found under " + root);
        return files;
    }

    private static string Relative(string path) =>
        path.Substring(RepositoryFiles.Root().Length).TrimStart('\\', '/').Replace('\\', '/');
}
