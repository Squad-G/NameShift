using Xunit;

namespace NameShift.Ui.Tests;

public sealed class SpacingTests
{
    [Fact]
    public void Values()
    {
        Assert.Equal(4, Spacing.Small);
        Assert.Equal(8, Spacing.Medium);
        Assert.Equal(12, Spacing.Large);
    }
}
