using System;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 9: the mutex and pipe names carry the user's SID (and the pipe the session id).</summary>
public sealed class InstanceNamesTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";

    [Fact]
    public void Mutex_LocalNamespace_WithSid()
    {
        Assert.Equal(@"Local\NameShift.S-1-5-21-1-2-3-1001", InstanceNames.Mutex(Sid));
    }

    [Fact]
    public void Pipe_SidAndSession()
    {
        Assert.Equal("NameShift.S-1-5-21-1-2-3-1001.2", InstanceNames.Pipe(Sid, 2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("S-1-")]
    [InlineData("S-2-5-21")]
    [InlineData("s-1-5-21")]
    [InlineData("X-1-5-21")]
    [InlineData("S-1-5-")]
    [InlineData("S-1-5--21")]
    [InlineData("S-1-5-21-a")]
    [InlineData("S-1-5-21 ")]
    [InlineData(@"S-1-5-21\x")]
    [InlineData("S-1-5-21.2")]
    public void NotASid_Throws(string sid)
    {
        Assert.ThrowsAny<ArgumentException>(() => InstanceNames.Mutex(sid));
        Assert.ThrowsAny<ArgumentException>(() => InstanceNames.Pipe(sid, 1));
    }

    [Fact]
    public void NullSid_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => InstanceNames.Mutex(null!));
        Assert.ThrowsAny<ArgumentException>(() => InstanceNames.Pipe(null!, 1));
    }

    [Fact]
    public void TooLongSid_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => InstanceNames.Mutex("S-1-" + new string('1', 300)));
    }

    [Fact]
    public void NegativeSession_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InstanceNames.Pipe(Sid, -1));
    }

    [Fact]
    public void ShortSid_Accepted()
    {
        Assert.Equal("NameShift.S-1-5-18.0", InstanceNames.Pipe("S-1-5-18", 0));
    }
}
