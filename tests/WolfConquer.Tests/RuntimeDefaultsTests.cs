using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class RuntimeDefaultsTests
{
    [Fact]
    public void SyntheticRebrandedCatalogRowKeepsExplicitServerIdTwo()
    {
        var row = Assert.Single(ClientConfigParser.Parse(
            "2 WolfConquer 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220"));
        Assert.Equal(2, row.ServerId);
        Assert.Equal(ConquerRuntimeDefaults.ServerName, row.ServerName);
    }

    [Fact]
    public void OwnedProjectSiteIsTheDefaultUnlessOperatorOverridesIt() =>
        Assert.Equal(Environment.GetEnvironmentVariable("WOLFCONQUER_WEBSITE_URL") ??
            "https://github.com/vgcman16/wolfconquer", ConquerRuntimeDefaults.WebsiteUrl);

    [Fact]
    public void RuntimeDefaultsMatchWorkingStackEndpoints()
    {
        Assert.Equal("WolfConquer", ConquerRuntimeDefaults.ServerName);
        Assert.Equal("127.0.0.1", ConquerRuntimeDefaults.ServerAddress);
        Assert.Equal(2021, ConquerRuntimeDefaults.Gateway.Port);
        Assert.Equal(5818, ConquerRuntimeDefaults.WorldServer.Port);
        Assert.Equal(9960, ConquerRuntimeDefaults.AccountServer.Port);
        Assert.Equal(6844, ConquerRuntimeDefaults.ReceiveSize);
        Assert.Equal(PacketHeader.DefaultMaxFrameSize, ConquerRuntimeDefaults.SendSize);
    }
}
