using NameShift.Core;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class RenamePipelineTests
{
    [Fact]
    public void ReplaceKeepsTheExtension()
    {
        var pipeline = new RenamePipeline([new ReplaceRule("IMG", "Photo")]);

        Assert.Equal("Photo_001.jpg", pipeline.NewName("IMG_001.jpg", 0));
    }

    [Fact]
    public void ReplaceIgnoresCaseUnlessAsked()
    {
        Assert.Equal("Xbc", new ReplaceRule("a", "X").Apply("Abc", 0));
        Assert.Equal("Abc", new ReplaceRule("a", "X", matchCase: true).Apply("Abc", 0));
    }
}
