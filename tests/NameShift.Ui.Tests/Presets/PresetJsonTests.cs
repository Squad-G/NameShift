using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NameShift.Core;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-36 AC 4, 6 and 7: the version 1 preset format.</summary>
public sealed class PresetJsonTests
{
    private static Preset Sample() => Preset.FromPanels("Photos", new IRulePanelModel[]
    {
        new CasePanelModel { Mode = CaseMode.Lower, Enabled = true },
        new ReplacePanelModel { Find = " a ", With = "b", MatchCase = true },
    });

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private static string Text(byte[] content) => new UTF8Encoding(false).GetString(content);

    [Fact]
    public void Serialize_Shape()
    {
        byte[] content = PresetJson.Serialize(Sample());
        string text = Text(content);

        Assert.Equal((byte)'{', content[0]);
        Assert.Contains("\n  ", text);
        Assert.Matches(new Regex("\"version\"\\s*:\\s*1\\b"), text);
        Assert.Matches(new Regex("\"name\"\\s*:\\s*\"Photos\""), text);
        Assert.Matches(new Regex("\"stage\"\\s*:\\s*\"Case\""), text);
        Assert.Matches(new Regex("\"enabled\"\\s*:\\s*true"), text);
        Assert.Matches(new Regex("\"settings\"\\s*:\\s*\\{"), text);
        Assert.Matches(new Regex("\"Mode\"\\s*:\\s*\"Lower\""), text);
        Assert.DoesNotContain("\"Key\"", text);
        Assert.DoesNotContain("\"Value\"", text);
        Assert.True(
            text.IndexOf("\"version\"", StringComparison.Ordinal) < text.IndexOf("\"name\"", StringComparison.Ordinal)
            && text.IndexOf("\"name\"", StringComparison.Ordinal) < text.IndexOf("\"panels\"", StringComparison.Ordinal),
            text);
    }

    [Fact]
    public void FromPanels_EnabledOrNotDefault()
    {
        Preset preset = Preset.FromPanels("P", new IRulePanelModel[]
        {
            new NumberingPanelModel { Enabled = true },
            new AddPanelModel(),
            new ReplacePanelModel { Find = "a" },
        });

        Assert.Equal(new[] { RuleStage.Replace, RuleStage.Numbering }, preset.Panels.Select(p => p.Stage).ToArray());
        Assert.False(preset.Panels[0].Enabled);
        Assert.True(preset.Panels[1].Enabled);
        Assert.Equal("a", preset.Panels[0].Settings["Find"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tr-TR")]
    public void RoundTrip(string culture)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture);
            var original = new Preset("Fotos \u0130 \uD83D\uDCF7", Sample().Panels);

            PresetRead read = PresetJson.Read(PresetJson.Serialize(original));

            Assert.Equal(PresetReadStatus.Ok, read.Status);
            Preset preset = read.Preset!;
            Assert.Equal(original.Name, preset.Name);
            Assert.Equal(original.Panels.Select(p => p.Stage), preset.Panels.Select(p => p.Stage));
            Assert.Equal(original.Panels.Select(p => p.Enabled), preset.Panels.Select(p => p.Enabled));
            for (int i = 0; i < original.Panels.Count; i++)
            {
                Assert.Equal(
                    original.Panels[i].Settings.OrderBy(p => p.Key, StringComparer.Ordinal),
                    preset.Panels[i].Settings.OrderBy(p => p.Key, StringComparer.Ordinal));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Read_HigherVersion_Unsupported()
    {
        PresetRead read = PresetJson.Read(Utf8("{\"version\":2,\"name\":\"Photos\",\"panels\":[]}"));

        Assert.Equal(PresetReadStatus.UnsupportedVersion, read.Status);
        Assert.Null(read.Preset);
    }

    [Fact]
    public void Read_UnknownStage_MissingFields_ExtraFields()
    {
        string json = "{\"name\":\"P\",\"extra\":[1,2],\"panels\":["
            + "{\"stage\":\"Sideways\",\"enabled\":true,\"settings\":{}},"
            + "{\"stage\":\"3\",\"enabled\":true,\"settings\":{}},"
            + "{\"enabled\":true},"
            + "{\"stage\":\"Case\",\"settings\":{\"Mode\":\"Upper\"},\"more\":{\"a\":1}},"
            + "{\"stage\":\"Replace\",\"enabled\":true}"
            + "]}";

        PresetRead read = PresetJson.Read(Utf8(json));

        Assert.Equal(PresetReadStatus.Ok, read.Status);
        Preset preset = read.Preset!;
        Assert.Equal("P", preset.Name);
        Assert.Equal(new[] { RuleStage.Replace, RuleStage.Case }, preset.Panels.Select(p => p.Stage).ToArray());
        Assert.True(preset.Panels[0].Enabled);
        Assert.Empty(preset.Panels[0].Settings);
        Assert.False(preset.Panels[1].Enabled);
        Assert.Equal("Upper", preset.Panels[1].Settings["Mode"]);
    }

    [Fact]
    public void Read_MissingPanels_NullSettingValue()
    {
        PresetRead noPanels = PresetJson.Read(Utf8("{\"version\":1,\"name\":\"P\"}"));
        PresetRead nullValue = PresetJson.Read(Utf8(
            "{\"version\":1,\"name\":\"P\",\"panels\":[null,{\"stage\":\"Case\",\"enabled\":true,\"settings\":{\"Mode\":null}}]}"));

        Assert.Equal(PresetReadStatus.Ok, noPanels.Status);
        Assert.Empty(noPanels.Preset!.Panels);
        Assert.Equal(PresetReadStatus.Ok, nullValue.Status);
        Assert.Equal("", Assert.Single(nullValue.Preset!.Panels).Settings["Mode"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"name\":\"  \"}")]
    [InlineData("{\"version\":1,\"name\":null}")]
    [InlineData("{\"version\":-1,\"name\":\"P\"}")]
    [InlineData("{\"version\":\"one\",\"name\":\"P\"}")]
    [InlineData("not json")]
    public void Read_Unreadable(string text)
    {
        PresetRead read = PresetJson.Read(Utf8(text));

        Assert.Equal(PresetReadStatus.Unreadable, read.Status);
        Assert.Null(read.Preset);
    }

    [Fact]
    public void Read_NotUtf8_OrNull_Unreadable()
    {
        Assert.Equal(PresetReadStatus.Unreadable, PresetJson.Read(new byte[] { 0xFF, 0xFE, 0x00, 0x7B, 0xC3 }).Status);
        Assert.Equal(PresetReadStatus.Unreadable, PresetJson.Read(null!).Status);
    }

    [Fact]
    public void Read_OverOneMiB_Unreadable()
    {
        byte[] valid = PresetJson.Serialize(Sample());
        var padded = new byte[1024 * 1024 + 1];
        for (int i = 0; i < padded.Length; i++)
        {
            padded[i] = (byte)' ';
        }

        Array.Copy(valid, padded, valid.Length);

        Assert.Equal(PresetReadStatus.Ok, PresetJson.Read(padded.Take(1024 * 1024).ToArray()).Status);
        Assert.Equal(PresetReadStatus.Unreadable, PresetJson.Read(padded).Status);
    }

    [Fact]
    public void Read_DuplicateStage_FirstWins()
    {
        PresetRead read = PresetJson.Read(Utf8("{\"name\":\"P\",\"panels\":["
            + "{\"stage\":\"Case\",\"enabled\":true,\"settings\":{\"Mode\":\"Upper\"}},"
            + "{\"stage\":\"case\",\"enabled\":false,\"settings\":{\"Mode\":\"Lower\"}}]}"));

        PresetPanel panel = Assert.Single(read.Preset!.Panels);
        Assert.True(panel.Enabled);
        Assert.Equal("Upper", panel.Settings["Mode"]);
    }

    // Pinned: the serializer reads a JSON number or bool where the settings map holds text as that text, and the panel
    // model then judges it like any typed text; an object or an array there makes the file Unreadable.
    [Theory]
    [InlineData("3", "3")]
    [InlineData("true", "true")]
    [InlineData("{\"a\":\"b\"}", null)]
    [InlineData("[1]", null)]
    public void Read_NonStringSettingValue(string value, string? loaded)
    {
        PresetRead read = PresetJson.Read(Utf8(
            "{\"version\":1,\"name\":\"P\",\"panels\":[{\"stage\":\"Case\",\"enabled\":true,\"settings\":{\"Mode\":" + value + "}}]}"));

        if (loaded == null)
        {
            Assert.Equal(PresetReadStatus.Unreadable, read.Status);
        }
        else
        {
            Assert.Equal(PresetReadStatus.Ok, read.Status);
            Assert.Equal(loaded, Assert.Single(read.Preset!.Panels).Settings["Mode"]);
        }
    }

    [Fact]
    public void Read_PanelsNotAnArray_NeverThrows()
    {
        PresetRead read = PresetJson.Read(Utf8("{\"version\":1,\"name\":\"P\",\"panels\":{}}"));

        Assert.Equal(PresetReadStatus.Ok, read.Status);
        Assert.Empty(read.Preset!.Panels);
    }

    [Fact]
    public void Preset_HasRulesOnly()
    {
        var models = new IRulePanelModel[]
        {
            new CasePanelModel { Mode = CaseMode.Upper, Enabled = true },
            new NumberingPanelModel { Enabled = true },
        };
        string text = Text(PresetJson.Serialize(Preset.FromPanels("P", models)));
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "version", "name", "panels", "stage", "enabled", "settings" };
        allowed.UnionWith(models.SelectMany(m => m.SaveSettings().Keys));

        foreach (Match match in Regex.Matches(text, "\"([^\"]*)\"\\s*:"))
        {
            Assert.Contains(match.Groups[1].Value, allowed);
        }

        var parameters = typeof(Preset).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Concat(typeof(Preset).GetMethod(nameof(Preset.FromPanels), BindingFlags.Public | BindingFlags.Static)!.GetParameters())
            .Select(p => p.Name!)
            .ToList();
        Assert.Equal(new[] { "name", "panels", "name", "models" }, parameters);
    }

    [Fact]
    public void Preset_DuplicateStage_Throws_AndKeepsStageOrder()
    {
        var empty = new Dictionary<string, string>();
        Assert.Throws<ArgumentException>(() => new Preset("P", new[]
        {
            new PresetPanel(RuleStage.Case, true, empty), new PresetPanel(RuleStage.Case, false, empty),
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PresetPanel((RuleStage)99, true, empty));

        var preset = new Preset("P", new[] { new PresetPanel(RuleStage.Add, true, empty), new PresetPanel(RuleStage.RegEx, true, empty) });

        Assert.Equal(new[] { RuleStage.RegEx, RuleStage.Add }, preset.Panels.Select(p => p.Stage).ToArray());
    }
}
