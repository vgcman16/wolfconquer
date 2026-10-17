using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class RootMagicEntryFixture
    {
        public void ChangeRootGhostRawInput(uint uid, Func<WorldSession, WorldSession> change)
        {
            var current = Assert.IsType<WorldSession>(RootItemEffectSlot(uid));
            Assert.Equal((long)uid, current.Character.Uid);
            var changed = change(current);
            Assert.Equal(current.Character.Uid, changed.Character.Uid);
            sessions.Set(connections[uid], changed);
        }

        public void AddRootGhostViewer(uint uid,
            Func<RootMagicEntryFixture, uint, byte[], ValueTask> onSend)
        {
            Assert.False(connections.ContainsKey(uid));
            var source = Peer;
            Assert.Equal((long)TargetPlayerUid, source.Character.Uid);
            var viewer = source with { Character = source.Character with
                { Uid = uid, Name = "GhostPhaseViewer" } };
            connections.Add(uid, new ConquerTcpConnection(null,
                new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    Order.Enqueue((uid, bytes));
                    await onSend(this, uid, bytes);
                }));
            sessions.Set(connections[uid], viewer);
        }

        public WorldSession? RetireRootGhostSubject(uint uid, bool rebind)
        {
            Assert.Contains(uid, new[] { PlayerUid, TargetPlayerUid, RootGhostExtraViewerUid });
            var current = Assert.IsType<WorldSession>(RootItemEffectSlot(uid));
            Assert.Equal((long)uid, current.Character.Uid);
            Assert.True(sessions.TryRemove(connections[uid], out _));
            if (!rebind) return null;
            var replacement = current with { Character = current.Character with
                { Uid = (long)uid + 9_000, Name = "GhostPhaseReplacement" } };
            sessions.Set(connections[uid], replacement);
            return replacement; // Declared connection/UID input only; no Ghost/HP/clock outcome is assigned.
        }
    }

    private const uint RootGhostExtraViewerUid = TargetPlayerUid + 1;

    private static async Task PrepareRootGhostPhaseAsync(RootMagicEntryFixture fixture)
    {
        await fixture.QueryRootItemEffectsAsync();
        Assert.Equal(5_000L, fixture.Owner.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        var killedAt = fixture.Now;
        await fixture.KillRootItemEffectOwnerAsync(); // Real peer learned1001/raw9000; never a saved Dead/HP outcome.
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        Assert.Equal(killedAt.AddSeconds(1), DateTimeOffset.Parse(
            fixture.Owner.Character.Attributes[LegacyPlayerDeathState.GhostStampAttribute],
            System.Globalization.CultureInfo.InvariantCulture));
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(1));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10)); // Native strict now > GhostStamp.
    }

    private static string? RootGhostPhaseKind(byte[] bytes)
    {
        var updates = ParseMsgUpdates([bytes]);
        if (updates.Count == 1 && updates[0].Uid == PlayerUid && updates[0].Updates.Count == 1)
        {
            var entry = updates[0].Updates[0];
            if (entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is { Count: 18 } && (entry.Values[0] & 1_024UL) != 0) return "Status";
            if (entry.Type == LegacyMsgUpdateDataType.Mesh) return "Mesh";
        }
        return LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _) ? "Map" : null;
    }

    private static (uint Recipient, byte[] Bytes)[] RootGhostPhaseEvents(
        RootMagicEntryFixture fixture, int start)
    {
        var events = fixture.Order.ToArray().Skip(start).ToArray();
        Assert.All(events, entry => Assert.NotNull(RootGhostPhaseKind(entry.Bytes)));
        return events; // No packet filter hides an unexpected frame.
    }
}

