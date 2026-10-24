using System;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class OpenSequenceTests
{
    private static readonly OpenRequest Folder = OpenRequest.ForFolder(@"D:\Pics");
    private static readonly OpenRequest File = OpenRequest.ForFile(@"D:\Pics", "a.jpg");
    private static readonly OpenRequest Message = OpenRequest.ForMessage("not found");

    private readonly OpenSequence _opens = new OpenSequence();

    [Fact]
    public void Current_Folder_Opens()
    {
        int ticket = _opens.Begin();

        Assert.Equal(OpenAction.Open, _opens.Complete(ticket, Folder, isBusy: false, folderShown: false));
        Assert.Equal(OpenAction.Open, _opens.Complete(ticket, File, isBusy: false, folderShown: true));
    }

    [Fact]
    public void NoFolder_NothingShown_FallsBackToStartFolder()
    {
        int ticket = _opens.Begin();

        Assert.Equal(OpenAction.OpenStartFolder, _opens.Complete(ticket, Message, isBusy: false, folderShown: false));
        Assert.Equal(OpenAction.OpenStartFolder, _opens.Complete(ticket, OpenRequest.Nothing, isBusy: false, folderShown: false));
    }

    [Fact]
    public void NoFolder_FolderShown_Stays()
    {
        int ticket = _opens.Begin();

        Assert.Equal(OpenAction.Stay, _opens.Complete(ticket, Message, isBusy: false, folderShown: true));
        Assert.Equal(OpenAction.Stay, _opens.Complete(ticket, OpenRequest.Nothing, isBusy: false, folderShown: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Busy_Discards_EvenAtStart(bool folderShown)
    {
        int ticket = _opens.Begin();

        Assert.Equal(OpenAction.Discard, _opens.Complete(ticket, Folder, isBusy: true, folderShown));
        Assert.Equal(OpenAction.Discard, _opens.Complete(ticket, Message, isBusy: true, folderShown));
    }

    [Fact]
    public void NavigationMeanwhile_Discards()
    {
        int ticket = _opens.Begin();

        _opens.Invalidate(); // a tree click, a typed path, or another open applied

        Assert.Equal(OpenAction.Discard, _opens.Complete(ticket, Folder, isBusy: false, folderShown: true));
        Assert.Equal(OpenAction.Discard, _opens.Complete(ticket, Message, isBusy: false, folderShown: false));
    }

    [Fact]
    public void NewerOpen_Wins_WhicheverFinishesFirst()
    {
        int slow = _opens.Begin();
        int fast = _opens.Begin();

        Assert.Equal(OpenAction.Open, _opens.Complete(fast, Folder, isBusy: false, folderShown: true));
        Assert.Equal(OpenAction.Discard, _opens.Complete(slow, Folder, isBusy: false, folderShown: true));
    }

    [Fact]
    public void Complete_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _opens.Complete(_opens.Begin(), null!, isBusy: false, folderShown: false));
    }
}
