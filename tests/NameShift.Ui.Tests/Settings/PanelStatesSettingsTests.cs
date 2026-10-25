using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NameShift.Core;
using NameShift.Ui.Rules;
using NameShift.Ui.Settings;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-84: the collapsed-panel states in <c>settings.json</c>, against real temp folders (never the user's
/// local application data folder).</summary>
public sealed class PanelStatesSettingsTests : IDisposable
{
    private readonly string _root;
    private readonly string _folder;

    public PanelStatesSettingsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ns-panels-" + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "NameShift");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    private AppSettingsStore Store() => new AppSettingsStore(_folder);

    private static NameShift.Ui.Presets.Preset NoRules() =>
        new NameShift.Ui.Presets.Preset(AppSettingsStore.LastRulesName, new NameShift.Ui.Presets.PresetPanel[0]);

    private AppSettings LoadRaw(string json)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(SettingsPath, new UTF8Encoding(false).GetBytes(json));
        return Store().LoadSettings();
    }

    private static Dictionary<RuleStage, bool> AllStages()
    {
        var map = new Dictionary<RuleStage, bool>();
        int i = 0;
        foreach (RuleStage stage in Enum.GetValues(typeof(RuleStage)))
        {
            map[stage] = i++ % 2 == 0;
        }

        return map;
    }

    // AC 1
    [Fact]
    public void RoundTrip_AllPanels()
    {
        Dictionary<RuleStage, bool> map = AllStages();
        Assert.Equal(11, map.Count);

        Store().Save(new AppSettings(null, 300, null, map), NoRules());

        AppSettings back = Store().LoadSettings();
        Assert.Equal(map.OrderBy(p => p.Key), back.CollapsedPanels.OrderBy(p => p.Key));
        Assert.Equal(300, back.MainSplit);
    }

    [Fact]
    public void Written_AsPlainObject_InStageOrder()
    {
        var map = new Dictionary<RuleStage, bool> { [RuleStage.Remove] = true, [RuleStage.RegEx] = false };

        Store().Save(new AppSettings(null, null, null, map), NoRules());

        string text = File.ReadAllText(SettingsPath);
        int panels = text.IndexOf("\"panels\": {", StringComparison.Ordinal);
        int regEx = text.IndexOf("\"RegEx\": \"open\"", StringComparison.Ordinal);
        int remove = text.IndexOf("\"Remove\": \"collapsed\"", StringComparison.Ordinal);
        Assert.True(panels >= 0);
        Assert.True(regEx > panels);
        Assert.True(remove > regEx);
        Assert.DoesNotContain("\"Key\"", text);
        Assert.DoesNotContain("\"Value\"", text);
        Assert.Contains("\"version\": 1", text);
    }

    [Fact]
    public void Empty_NotWritten()
    {
        Store().Save(AppSettings.Empty, NoRules());

        Assert.DoesNotContain("panels", File.ReadAllText(SettingsPath));
    }

    // AC 2
    [Fact]
    public void Missing_EmptyMap()
    {
        AppSettings s = LoadRaw("{\"version\": 1, \"mainSplit\": 300}");

        Assert.Empty(s.CollapsedPanels);
        Assert.Equal(300, s.MainSplit);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("[1,2]")]
    [InlineData("\"x\"")]
    [InlineData("null")]
    public void WrongType_Ignored(string value)
    {
        AppSettings s = LoadRaw(
            "{\"window\": {\"left\": 1, \"top\": 2, \"width\": 30, \"height\": 40}, \"mainSplit\": 300, \"panels\": " + value + "}");

        Assert.Empty(s.CollapsedPanels);
        Assert.Equal(300, s.MainSplit);
        Assert.Equal(30, s.Window!.Width);
    }

    [Fact]
    public void OneBadEntry_OthersLoad()
    {
        AppSettings s = LoadRaw(
            "{\"mainSplit\": 300, \"panels\": {\"Remove\": \"collapsed\", \"Add\": 3, \"Name\": \"maybe\", \"Bogus\": \"open\", \"5\": \"open\"}}");

        KeyValuePair<RuleStage, bool> only = Assert.Single(s.CollapsedPanels);
        Assert.Equal(RuleStage.Remove, only.Key);
        Assert.True(only.Value);
        Assert.Equal(300, s.MainSplit);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("05")]
    [InlineData("99")]
    public void NumberAsStageName_Ignored(string key)
    {
        AppSettings s = LoadRaw("{\"panels\": {\"" + key + "\": \"open\", \"Add\": \"open\"}}");

        KeyValuePair<RuleStage, bool> only = Assert.Single(s.CollapsedPanels);
        Assert.Equal(RuleStage.Add, only.Key);
    }

    [Fact]
    public void NewerVersion_Defaults()
    {
        AppSettings s = LoadRaw("{\"version\": 2, \"panels\": {\"Remove\": \"collapsed\"}}");

        Assert.Empty(s.CollapsedPanels);
        Assert.Null(s.MainSplit);
        Assert.Null(s.Window);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void RoundTrip_GermanAndTurkishCultures(string culture)
    {
        CultureInfo oldCulture = CultureInfo.CurrentCulture;
        CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            Dictionary<RuleStage, bool> map = AllStages();

            Store().Save(new AppSettings(null, 300, null, map), NoRules());

            Assert.Equal(map.OrderBy(p => p.Key), Store().LoadSettings().CollapsedPanels.OrderBy(p => p.Key));
            Assert.Contains("\"RegEx\":", File.ReadAllText(SettingsPath));
            Assert.True(LoadRaw("{\"panels\": {\"RegEx\": \"collapsed\"}}").CollapsedPanels[RuleStage.RegEx]);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    [Fact]
    public void CollapsedPanels_AreCopied()
    {
        var map = new Dictionary<RuleStage, bool> { [RuleStage.Add] = true };
        var s = new AppSettings(null, null, null, map);

        map[RuleStage.Add] = false;

        Assert.True(s.CollapsedPanels[RuleStage.Add]);
        Assert.Empty(AppSettings.Empty.CollapsedPanels);
    }

    // AC 3
    [Fact]
    public void SavedCollapsed_EnabledPanel_StaysCollapsed()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        replace.Enabled = true;
        var saved = LoadRaw("{\"panels\": {\"Replace\": \"collapsed\"}}").CollapsedPanels;

        using var model = new PanelCollapseModel(host, saved);

        Assert.True(model.IsCollapsed(replace));
        replace.Enabled = false;
        replace.Enabled = true;
        Assert.False(model.IsCollapsed(replace));
    }

    [Fact]
    public void NoSavedEntry_EnabledPanel_Open()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        replace.Enabled = true;

        using var model = new PanelCollapseModel(host, AppSettings.Empty.CollapsedPanels);

        Assert.False(model.IsCollapsed(replace));
    }

    // AC 5
    [Fact]
    public void ResetAll_DoesNotChangeStates()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        FakeRulePanelModel add = FakeRulePanelModel.Add();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, add });
        var saved = new Dictionary<RuleStage, bool> { [RuleStage.Replace] = true, [RuleStage.Add] = false };
        using var model = new PanelCollapseModel(host, saved);
        Dictionary<RuleStage, bool> before = model.States.ToDictionary(p => p.Key, p => p.Value);

        host.ResetAll();

        Assert.Equal(before.OrderBy(p => p.Key), model.States.OrderBy(p => p.Key));
    }
}
