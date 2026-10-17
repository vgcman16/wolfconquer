using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class RuntimeDefaultsTests
{
    [Fact]
    public void RuntimeDefaultsMatchWorkingStackEndpoints()
    {
        Assert.Equal("TestSrv", ConquerRuntimeDefaults.ServerName);
        Assert.Equal("127.0.0.1", ConquerRuntimeDefaults.ServerAddress);
        Assert.Equal(2021, ConquerRuntimeDefaults.Gateway.Port);
        Assert.Equal(5818, ConquerRuntimeDefaults.WorldServer.Port);
        Assert.Equal(9960, ConquerRuntimeDefaults.AccountServer.Port);
        Assert.Equal(6844, ConquerRuntimeDefaults.ReceiveSize);
        Assert.Equal(PacketHeader.DefaultMaxFrameSize, ConquerRuntimeDefaults.SendSize);
    }
}
