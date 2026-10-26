using System.Net;
using System.Text.Json.Nodes;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed partial class PackageJoin
    {
        internal async Task<LegacyNativeGameClientRuntime> RestartNativeDebtRecoveryAsync()
        {
            await DisposeAsync();
            IsClosed = false;
            Recovery = new(fixture.Users, fixture.Items, null, fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var opened, out reason), reason);
            Registry = Assert.IsType<LegacyItemUidRegistry>(opened);
            Domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(fixture.PackageUidState))!["Domain"]!.GetValue<string>(), "N");
            Assert.True(LegacyMailboxPackageProducer.TryCreate(Registry, Domain, Recovery, fixture.Users, fixture.Items,
                fixture.PackageMailboxRoot, fixture.PackageGearRoot, out var producer, out reason), reason);
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
                playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            Handler = new WorldServerFrameHandler(loader, handoff, worldData, producer!, fixture.Log,
                ConquerProtocolProfile.VisibleDx8D13);
            var native = new LegacyNativeGameClientRuntime(); // Real constructor birth before transport.
            Assert.Same(native, native.Player.Owner);
            Assert.Equal(0U, native.Player.UID);
            Assert.False(native.FullLoading);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                var bytes = frame.ToArray();
                fixture.Sent.Add(bytes);
                SendObserver?.Invoke(bytes);
                return ValueTask.CompletedTask;
            }, nativeGameClient: native);
            await FrameAsync(new LegacyMsgLoginGamePacket(checked((uint)fixture.Account)).ToFrameBytes());
            return native;
        }
    }
}
