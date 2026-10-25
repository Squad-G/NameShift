using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class RulePanelModelsTests
{
    private static readonly Type[] WindowOrder =
    {
        typeof(ReplacePanelModel), typeof(CasePanelModel), typeof(RemovePanelModel), typeof(AddPanelModel),
        typeof(NumberingPanelModel), typeof(NamePanelModel), typeof(ExtensionPanelModel), typeof(RegexPanelModel),
        typeof(MoveCopyPanelModel), typeof(AutoDatePanelModel), typeof(FolderNamePanelModel),
    };

    [Fact]
    public void CreateAll_HasEveryPanelModelOnce()
    {
        Type[] created = RulePanelModels.CreateAll().Select(m => m.GetType()).ToArray();
        Type[] expected = typeof(RulePanelModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(RulePanelModel).IsAssignableFrom(t))
            .ToArray();

        Assert.Equal(created.Length, created.Distinct().Count());
        Assert.Equal(expected.OrderBy(t => t.FullName, StringComparer.Ordinal), created.OrderBy(t => t.FullName, StringComparer.Ordinal));
    }

    [Fact]
    public void CreateAll_DistinctStages()
    {
        IReadOnlyList<IRulePanelModel> models = RulePanelModels.CreateAll();

        Assert.Equal(11, models.Select(m => m.Stage).Distinct().Count());
    }

    [Fact]
    public void CreateAll_WindowOrder()
    {
        Assert.Equal(WindowOrder, RulePanelModels.CreateAll().Select(m => m.GetType()).ToArray());
    }

    [Fact]
    public void CreateAll_FreshDefaults()
    {
        IReadOnlyList<IRulePanelModel> first = RulePanelModels.CreateAll();
        IReadOnlyList<IRulePanelModel> second = RulePanelModels.CreateAll();

        Assert.Empty(first.Intersect(second));
        Assert.All(first.Concat(second), model =>
        {
            Assert.True(model.IsDefault, model.GetType().Name);
            Assert.False(model.Enabled, model.GetType().Name);
        });
    }
}
