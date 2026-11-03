using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerDhPeerWireTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("one")]
    [InlineData("p-minus-one")]
    [InlineData("p")]
    [InlineData("nonhex")]
    public Task InvalidLegacyDhPeerGetsActualEofWithoutDispatchAndListenerStillAcceptsFreshValidRngHandshake(string kind) =>
        NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            await fixture.AssertInvalidDhPeerClosesBeforeTransportDispatchAsync(kind);
            // Same real listener, fresh server/client CSPRNG exponents and the established encrypted/sealed transport.
            var valid = await fixture.ConnectGameAsync(false);
            var dispatch = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, dispatch.Opcode);
            var owner = Assert.IsType<LegacyNativeGameClientRuntime>(dispatch.Connection.NativeGameClient);
            Assert.Same(owner, owner.Player.Owner); Assert.Equal(0U, owner.Player.UID);
            Assert.Null(dispatch.Before); Assert.Null(dispatch.After);
            await fixture.CloseClientTransportAsync(clientIndex: 1); // Invalid EOF drain0 and genuine valid drain1 remain aligned.
            Assert.True(dispatch.Connection.HasObservedTransportClose);
            GC.KeepAlive(valid);
        });
}
