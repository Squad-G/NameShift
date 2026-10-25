using System;
using System.IO;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>NS-91 AC 6: a special folder that is not a fully qualified path falls back to the temp folder.</summary>
public sealed class AppDataFoldersTests
{
    private const string FakeTemp = @"T:\fake\Temp\";

    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local", @"C:\Users\u\AppData\Local\NameShift")]
    [InlineData(@"C:\Users\u\AppData\Local\", @"C:\Users\u\AppData\Local\NameShift")]
    [InlineData(@"c:/Users/u", @"c:/Users/u\NameShift")]
    [InlineData("", @"T:\fake\Temp\NameShift")]
    [InlineData("  ", @"T:\fake\Temp\NameShift")]
    [InlineData(null, @"T:\fake\Temp\NameShift")]
    [InlineData(@"relative\x", @"T:\fake\Temp\NameShift")]
    [InlineData(@"\foo", @"T:\fake\Temp\NameShift")]
    [InlineData(@"C:foo", @"T:\fake\Temp\NameShift")]
    [InlineData(@"C:", @"T:\fake\Temp\NameShift")]
    [InlineData(@"C:\Users\|bad", @"T:\fake\Temp\NameShift")]
    [InlineData(@"C:\", @"C:\NameShift")]
    [InlineData(@"\\srv\share\p", @"\\srv\share\p\NameShift")]
    public void Resolve(string? specialFolder, string expected) =>
        Assert.Equal(expected, AppDataFolders.Resolve(specialFolder, FakeTemp));

    [Fact]
    public void Resolve_NullTemp_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AppDataFolders.Resolve(@"C:\x", null!));

    [Fact]
    public void AppFolderName_IsNameShift() => Assert.Equal("NameShift", AppDataFolders.AppFolderName);
}
