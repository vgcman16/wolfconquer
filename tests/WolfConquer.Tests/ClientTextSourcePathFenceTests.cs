using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientTextSourcePathFenceTests
{
    [Theory]
    [InlineData(@"C:\fixtures\Destroyer-Online\ini\StrRes.ini")]
    [InlineData(@"C:\fixtures\Eudemons\ini\StrRes.ini")]
    [InlineData(@"C:\fixtures\Demon Online\ini\StrRes.ini")]
    public void BlocksKnownWrongGameSourceRoots(string path)
    {
        Assert.True(ClientTextSourcePathFence.IsBlockedWrongGameSourcePath(path));
        Assert.False(ClientTextSourcePathFence.IsAllowedSourcePath(path));
    }

    [Fact]
    public void AllowsConquerSourceRoots()
    {
        var path = @"C:\fixtures\Conquer Online\src\WolfConquer.Core\Client\SyntheticCatalog.cs";

        Assert.False(ClientTextSourcePathFence.IsBlockedWrongGameSourcePath(path));
        Assert.True(ClientTextSourcePathFence.IsAllowedSourcePath(path));
    }
}
