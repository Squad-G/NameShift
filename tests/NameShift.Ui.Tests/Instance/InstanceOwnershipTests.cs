using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 11: only the window that owns the instance saves the settings at close.</summary>
public sealed class InstanceOwnershipTests
{
    [Fact]
    public void Owner_Saves()
    {
        var owner = new InstanceOwnership(ownsInstance: true);

        Assert.True(owner.OwnsInstance);
        Assert.True(owner.ShouldSaveSettings);
    }

    [Fact]
    public void Fallback_DoesNotSave()
    {
        var fallback = new InstanceOwnership(ownsInstance: false);

        Assert.False(fallback.OwnsInstance);
        Assert.False(fallback.ShouldSaveSettings);
    }
}
