using NameShift.Ui.Settings;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-37 AC 6: the saved folder-tree splitter, clamped at start.</summary>
public sealed class SplitterRuleTests
{
    [Theory]
    [InlineData(300, 1000, 160, 800, 300)]   // a saved value that fits
    [InlineData(null, 1000, 160, 800, 260)]  // none saved: the default
    [InlineData(-5, 1000, 160, 800, 260)]    // negative: ignored
    [InlineData(1200, 1000, 160, 800, 260)]  // larger than the container: ignored
    [InlineData(1000, 1000, 160, 800, 800)]  // the container's width itself fits, then the panel's limit applies
    [InlineData(100, 1000, 160, 800, 160)]   // below the tree's minimum: raised
    [InlineData(900, 1000, 160, 800, 800)]   // leaves the right side too little: lowered
    [InlineData(0, 1000, 160, 800, 160)]
    [InlineData(300, 200, 160, 100, 160)]    // a container too small for both minimums: the tree's minimum wins
    [InlineData(null, 200, 160, 100, 160)]
    public void Clamp(int? saved, int container, int min, int max, int expected)
    {
        Assert.Equal(expected, SplitterRule.Clamp(saved, 260, container, min, max));
    }
}
