using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

/// <summary>NS-36 AC 1 to 3: each panel model saves its settings as a flat map of text and loads them back.</summary>
public sealed class PanelSettingsTests
{
    // Explicit samples for a property whose setter refuses the sample built by type: (model type, property) -> value.
    private static readonly Dictionary<(Type, string), object> SampleOverrides = new Dictionary<(Type, string), object>();

    /// <summary>Each panel with every setting at a non-default value.</summary>
    private static readonly Dictionary<string, Func<IRulePanelModel>> Configured = new Dictionary<string, Func<IRulePanelModel>>
    {
        ["Add"] = () => new AddPanelModel
        {
            Prefix = "p-", Insert = "ins", InsertPositionText = "3", InsertFromEnd = true, Suffix = "-s", WordSpace = true,
        },
        ["AutoDate"] = () => new AutoDatePanelModel
        {
            Source = DateSource.Created, Format = "yyyyMMdd", Placement = DatePlacement.Suffix, Separator = " - ",
        },
        ["Case"] = () => new CasePanelModel { Mode = CaseMode.Title, ExceptionsText = "of, the" },
        ["Extension"] = () => new ExtensionPanelModel { Mode = ExtensionMode.Fixed, Text = "jpg" },
        ["FolderName"] = () => new FolderNamePanelModel
        {
            Placement = FolderNamePlacement.Suffix, LevelsText = "2", Separator = " ",
        },
        ["MoveCopy"] = () => new MoveCopyPanelModel
        {
            Mode = MoveCopyMode.Copy, Source = MoveCopySource.End, Target = MoveCopyTarget.Position, CountText = "4",
            PositionText = "2", Separator = "-",
        },
        ["Name"] = () => new NamePanelModel { Mode = NameMode.Fixed, Text = "photo" },
        ["Numbering"] = () => new NumberingPanelModel
        {
            // Numerals first: its setter resets UpperCase and rewrites StartText.
            Numerals = NumeralSystem.Roman, UpperCase = false, StartText = "iv", Mode = NumberingMode.Insert,
            StepText = "2", PadText = "3", Separator = "-", InsertPositionText = "5",
        },
        ["Regex"] = () => new RegexPanelModel
        {
            Pattern = "(\\d+)", Replacement = "#$1", IgnoreCase = true, IncludeExtension = true,
        },
        ["Remove"] = () => new RemovePanelModel
        {
            FirstText = "1", LastText = "2", FromText = "3", ToText = "4", Characters = "xy", Text = "draft",
            WholeWord = true, MatchCase = true, CropBefore = "[", CropAfter = "]", KeepMarker = true, Digits = true,
            Symbols = true, Accents = true, HighCharacters = true, Trim = true, LeadingDots = true, DoubleSpaces = true,
        },
        ["Replace"] = () => new ReplacePanelModel
        {
            Find = "a", With = "b", MatchCase = true, IncludeExtension = true, KeepCase = true,
        },
    };

    public static IEnumerable<object[]> PanelsAndCultures() =>
        Configured.Keys.SelectMany(name => new[] { new object[] { name, "" }, new object[] { name, "tr-TR" } });

    [Theory]
    [MemberData(nameof(PanelsAndCultures))]
    public void RoundTrip_EachPanel(string panel, string culture)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture);
            IRulePanelModel source = Configured[panel]();
            IRulePanelModel fresh = NewModel(source.GetType());
            foreach (PropertyInfo property in SettingProperties(source.GetType()))
            {
                // The setup puts every setting at a non-default value.
                Assert.False(
                    Equals(property.GetValue(fresh), property.GetValue(source)),
                    $"{panel}.{property.Name} is at its default in the setup.");
            }

            IReadOnlyDictionary<string, string> saved = source.SaveSettings();
            IRulePanelModel target = NewModel(source.GetType());
            target.LoadSettings(saved);

            Assert.False(saved.ContainsKey(nameof(IRulePanelModel.Enabled)));
            Assert.False(target.Enabled);
            foreach (PropertyInfo property in SettingProperties(source.GetType()))
            {
                Assert.True(
                    Equals(property.GetValue(source), property.GetValue(target)),
                    $"{panel}.{property.Name}: {property.GetValue(source)} saved, {property.GetValue(target)} loaded.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Save_Format()
    {
        var replace = new ReplacePanelModel { Find = " a ", MatchCase = true };
        var casePanel = new CasePanelModel { Mode = CaseMode.Lower };

        IReadOnlyDictionary<string, string> saved = replace.SaveSettings();

        Assert.Equal(" a ", saved["Find"]);
        Assert.Equal("", saved["With"]);
        Assert.Equal("true", saved["MatchCase"]);
        Assert.Equal("false", saved["KeepCase"]);
        Assert.Equal("Lower", casePanel.SaveSettings()["Mode"]);
    }

    // Hexadecimal "10" also reads as a decimal start, so applying StartText before Numerals would get it rewritten.
    public static IEnumerable<object[]> KeyOrders() =>
        from numerals in new[] { NumeralSystem.Roman, NumeralSystem.Hexadecimal }
        from reversed in new[] { false, true }
        select new object[] { numerals, numerals == NumeralSystem.Roman ? "iv" : "10", reversed };

    [Theory]
    [MemberData(nameof(KeyOrders))]
    public void Numbering_RomanUpperOffStart_AnyKeyOrder(NumeralSystem numerals, string start, bool reversed)
    {
        var source = new NumberingPanelModel { Numerals = numerals, UpperCase = false, StartText = start };
        List<KeyValuePair<string, string>> pairs = source.SaveSettings().ToList();
        if (reversed)
        {
            pairs.Reverse();
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            map.Add(pair.Key, pair.Value);
        }

        var target = new NumberingPanelModel();
        target.LoadSettings(map);

        Assert.Equal(numerals, target.Numerals);
        Assert.False(target.UpperCase);
        Assert.Equal(start, target.StartText);
        Assert.Equal(source.Start, target.Start);
    }

    [Fact]
    public void Numbering_RomanWithoutStartText_GetsThePanelsOwnStart()
    {
        var target = new NumberingPanelModel();

        target.LoadSettings(Map(("Numerals", "Roman")));

        Assert.Equal("I", target.StartText);
        Assert.True(target.UpperCase);
    }

    [Fact]
    public void Load_RaisesChangedOnce_DoesNotTouchEnabled()
    {
        var model = new CasePanelModel { Enabled = true };
        int changes = 0;
        model.Changed += (_, _) => changes++;

        model.LoadSettings(Map(("Mode", "Upper"), ("ExceptionsText", "of")));

        Assert.Equal(1, changes);
        Assert.True(model.Enabled);
        Assert.Equal(CaseMode.Upper, model.Mode);
    }

    [Fact]
    public void Load_UnknownAndMissingKeys()
    {
        var model = new CasePanelModel { Mode = CaseMode.Upper, ExceptionsText = "of" };

        model.LoadSettings(Map(("Mode", "Lower"), ("Bogus", "1")));

        Assert.Equal(CaseMode.Lower, model.Mode);
        Assert.Equal("", model.ExceptionsText);
    }

    [Theory]
    [InlineData("Case", "Mode", "sideways")]
    [InlineData("Case", "Mode", "3")]
    [InlineData("Case", "Mode", "1, 2")]
    [InlineData("Numbering", "Mode", "sideways")]
    [InlineData("Numbering", "Mode", "3")]
    [InlineData("Numbering", "Mode", "1, 2")]
    [InlineData("Numbering", "UpperCase", "maybe")]
    public void Load_BadEnumOrBool_FallsBackForThatSetting(string panel, string key, string value)
    {
        IRulePanelModel source = Configured[panel]();
        var map = source.SaveSettings().ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        map[key] = value;
        IRulePanelModel target = NewModel(source.GetType());
        IRulePanelModel defaults = NewModel(source.GetType());
        if (panel == "Numbering")
        {
            // The default case is the one the panel picks for its system.
            ((NumberingPanelModel)defaults).Numerals = NumeralSystem.Roman;
        }

        target.LoadSettings(map);

        foreach (PropertyInfo property in SettingProperties(source.GetType()))
        {
            object? expected = property.Name == key ? property.GetValue(defaults) : property.GetValue(source);
            Assert.True(
                Equals(expected, property.GetValue(target)),
                $"{panel}.{property.Name}: expected {expected}, got {property.GetValue(target)}.");
        }
    }

    [Fact]
    public void Load_EnumAndBoolIgnoreCase()
    {
        var model = new ReplacePanelModel();

        model.LoadSettings(Map(("MatchCase", "TRUE")));
        var casePanel = new CasePanelModel();
        casePanel.LoadSettings(Map(("Mode", "upper")));

        Assert.True(model.MatchCase);
        Assert.Equal(CaseMode.Upper, casePanel.Mode);
    }

    [Fact]
    public void Load_TextAsSaved()
    {
        var model = new NumberingPanelModel();

        model.LoadSettings(Map(("StartText", "abc"), ("StepText", "-"), ("PadText", "x")));

        Assert.Equal("abc", model.StartText);
        Assert.Equal("-", model.StepText);
        Assert.Equal("x", model.PadText);
        Assert.NotEqual("", model.StartError);
        Assert.NotEqual("", model.StepError);
        Assert.NotEqual("", model.PadError);
    }

    [Fact]
    public void Load_NullValue_IsEmpty()
    {
        var model = new ReplacePanelModel { Find = "a" };
        var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["Find"] = null!, ["With"] = "b" };

        model.LoadSettings(map);

        Assert.Equal("", model.Find);
        Assert.Equal("b", model.With);
    }

    [Fact]
    public void Load_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CasePanelModel().LoadSettings(null!));
    }

    [Fact]
    public void EverySetting_IsSaved()
    {
        foreach (Type type in PanelTypes())
        {
            IRulePanelModel model = Sampled(type);

            var expected = SettingProperties(type).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var saved = model.SaveSettings().Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.True(
                expected.SequenceEqual(saved),
                $"{type.Name} saves [{string.Join(", ", saved)}], its settings are [{string.Join(", ", expected)}].");
        }
    }

    [Fact]
    public void EverySetting_RoundTrips()
    {
        foreach (Type type in PanelTypes())
        {
            IRulePanelModel source = Sampled(type);
            IRulePanelModel target = NewModel(type);

            target.LoadSettings(source.SaveSettings());

            foreach (PropertyInfo property in SettingProperties(type))
            {
                Assert.True(
                    Equals(property.GetValue(source), property.GetValue(target)),
                    $"{type.Name}.{property.Name}: {property.GetValue(source)} saved, {property.GetValue(target)} loaded.");
            }
        }
    }

    [Fact]
    public void Configured_CoversEveryPanel()
    {
        Assert.Equal(
            PanelTypes().Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal),
            Configured.Values.Select(f => f().GetType().Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> Map(params (string Key, string Value)[] pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    /// <summary>Every non-abstract panel model in NameShift.Ui; asserts the count so a new panel is seen.</summary>
    private static List<Type> PanelTypes()
    {
        var types = typeof(IRulePanelModel).Assembly.GetTypes()
            .Where(t => typeof(IRulePanelModel).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(11, types.Count);
        return types;
    }

    /// <summary>The public instance properties with a public setter, except Enabled.</summary>
    private static IEnumerable<PropertyInfo> SettingProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetSetMethod() != null && p.Name != nameof(IRulePanelModel.Enabled))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    private static IRulePanelModel NewModel(Type type) => (IRulePanelModel)Activator.CreateInstance(type)!;

    /// <summary>A new model with every setting set to a sample built by type (or from the override table). Each setting
    /// must end up different from a new model's, so a setter that rewrites another setting cannot hide a missing
    /// line.</summary>
    private static IRulePanelModel Sampled(Type type)
    {
        IRulePanelModel model = NewModel(type);
        foreach (PropertyInfo property in SettingProperties(type))
        {
            object sample = SampleOverrides.TryGetValue((type, property.Name), out object? value)
                ? value
                : SampleFor(type, property, property.GetValue(model));
            try
            {
                property.SetValue(model, sample);
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail($"{type.Name}.{property.Name} refused the sample {sample}: add a sample. {e.InnerException?.Message}");
            }
        }

        IRulePanelModel fresh = NewModel(type);
        foreach (PropertyInfo property in SettingProperties(type))
        {
            Assert.False(
                Equals(property.GetValue(fresh), property.GetValue(model)),
                $"{type.Name}.{property.Name} is back at its default after sampling: add a sample.");
        }

        return model;
    }

    private static object SampleFor(Type type, PropertyInfo property, object? current)
    {
        Type t = property.PropertyType;
        if (t == typeof(bool))
        {
            return !(bool)current!;
        }

        if (t == typeof(string))
        {
            return "sample";
        }

        if (t.IsEnum)
        {
            Array values = Enum.GetValues(t);
            int index = Array.IndexOf(values, current);
            return values.GetValue((index + 1) % values.Length)!;
        }

        if (t == typeof(int))
        {
            return (int)current! + 1;
        }

        if (t == typeof(int?))
        {
            return ((int?)current ?? 0) + 1;
        }

        Assert.Fail($"{type.Name}.{property.Name} has the type {t.Name}: add a sample.");
        return null!;
    }
}
