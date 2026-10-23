using System.Net.Sockets;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyNativeLoginSocketIdentityTests
{
    [Fact]
    public void FullRemoteIpRetainsNoneForAbsentConnectionAndOriginalOverrideDefault()
    {
        var socket=new LegacyNativeSecuritySocket(null!,null!);
        Assert.Null(socket.Connection);Assert.False(socket.OverrideTiming);Assert.Equal("NONE",socket.RemoteIp);
        Assert.NotNull(typeof(LegacyNativeSecuritySocket).GetMethod("GetMACAddress"));
        // GetMACAddress is deliberately not executed: it enumerates real host NICs.
    }

    [Fact]
    public void FullRemoteIpCatchesRealUnconnectedAndDisposedSocketEndpointFaults()
    {
        var owner=new LegacyNativeSecuritySocket(null!,null!);
        using var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        owner.Connection=socket;Assert.Equal("NONE",owner.RemoteIp);
        socket.Dispose();Assert.Equal("NONE",owner.RemoteIp);
    }
}
