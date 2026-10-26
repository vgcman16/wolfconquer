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
    private sealed record CombinedAdmissionOwners(CharacterSnapshotLoader Loader, AccountHandoffService Handoff,
        LegacyMailboxPackageProducer Mailbox, LegacyOrdinaryAdmissionUidOwner Ordinary,
        LegacyOrdinaryAdmissionRoots OrdinaryRoots);

    private sealed partial class PackageJoin
    {
        internal async Task<CombinedAdmissionOwners> PrepareCombinedAdmissionOwnersAsync(bool wrongOrdinaryRoute = false)
        {
            await DisposeAsync();
            IsClosed = false;
            var spells = Path.Combine(fixture.Root, "CombinedSpells");
            var profs = Path.Combine(fixture.Root, "CombinedProficiencies");
            var quests = Path.Combine(fixture.Root, "CombinedQuests");
            foreach (var root in new[] { spells, profs, quests }) Directory.CreateDirectory(root);
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, spells, profs,
                fixture.PackageGearRoot, quests);
            Recovery = new(fixture.Users, fixture.Items, quests, fixture.PackageMailboxRoot,
                fixture.PackageGearRoot, spells, profs);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
            Assert.True(Recovery.IsReadyForOrdinaryAdmission(roots));
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var opened, out reason), reason);
            Registry = Assert.IsType<LegacyItemUidRegistry>(opened);
            Domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(fixture.PackageUidState))!["Domain"]!.GetValue<string>(), "N");
            var ordinaryRoots = roots;
            if (wrongOrdinaryRoute)
            {
                var other = Path.Combine(fixture.Root, "OtherConfiguredProficiencies");
                Directory.CreateDirectory(other);
                ordinaryRoots = roots with { Proficiencies = other };
            }
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(Registry, Domain, ordinaryRoots,
                fixture.PackageMailboxRoot, out var ordinary, out reason), reason);
            Assert.True(LegacyMailboxPackageProducer.TryCreate(Registry, Domain, Recovery, fixture.Users, fixture.Items,
                fixture.PackageMailboxRoot, fixture.PackageGearRoot, out var mailbox, out reason), reason);
            return new(loader, new AccountHandoffService(new InMemoryAccountHandoffTicketStore()), mailbox!, ordinary!, ordinaryRoots);
        }

        internal async Task<LegacyNativeGameClientRuntime> StartCombinedNativeAdmissionAsync(CombinedAdmissionOwners owners)
        {
            Handler = new WorldServerFrameHandler(owners.Loader, owners.Handoff, worldData,
                owners.Mailbox, owners.Ordinary, fixture.Log, ConquerProtocolProfile.VisibleDx8D13);
            var native = new LegacyNativeGameClientRuntime(); // Actual constructor owner before transport, no queue injection.
            Assert.Same(native, native.Player.Owner);
            Assert.Equal(0U, native.Player.UID);
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
