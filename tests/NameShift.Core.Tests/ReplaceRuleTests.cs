using Xunit;

namespace NameShift.Core.Tests;

public sealed class ReplaceRuleTests
{
    [Fact]
    public void ReplaceKeepsTheExtension()
    {
        var pipeline = new RenamePipeline([new ReplaceRule("IMG", "Photo")]);

        var results = pipeline.Run(Items.Files("IMG_001.jpg"));

        Assert.Equal("Photo_001.jpg", results[0].NewName);
    }

    [Fact]
    public void ReplaceIgnoresCaseUnlessAsked()
    {
        var context = new ItemContext(Items.File("Abc.txt"), 0, 1);
        var name = new NameParts("Abc", "txt");

        Assert.Equal("Xbc", new ReplaceRule("a", "X").Apply(name, context).BaseName);
        Assert.Equal("Abc", new ReplaceRule("a", "X", matchCase: true).Apply(name, context).BaseName);
    }
}
