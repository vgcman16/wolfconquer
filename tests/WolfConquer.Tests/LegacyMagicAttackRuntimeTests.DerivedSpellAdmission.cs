using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task MissingDerivedVenomRowRefusesQueryBeforeAnyAppendOrStatePublication()
    {
        var (session, data) = MissingDerivedVenomInput(false);
        var sessions = new WorldSessionStore(); var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent); sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, session, WorldData: data, Sessions: sessions);
        var beforeSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(session.PlayerSpells);
        var result = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, session,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.AllowAnimation,
                new LegacyEquipmentQueryPublicationScope(session.Character.Uid)), CancellationToken.None);
        Assert.False(result.Completed); Assert.False(result.Admitted);
        Assert.Equal(LegacyEquipmentQueryPublicationLoss.RequiredDerivedSpellUnavailable, result.Loss);
        Assert.Empty(sent); Assert.True(sessions.TryGet(connection, out var current)); Assert.Same(session, current);
        Assert.Same(session.PhysicalStatusOverlay, current!.PhysicalStatusOverlay);
        Assert.Same(session.FineRainEquipmentAdmission, current.FineRainEquipmentAdmission);
        Assert.Equal(beforeSpells, PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
    }

    [Fact]
    public async Task FineRainDeniedQueryDoesNotRequireAnUnusedDerivedVenomRow()
    {
        var (session, data) = MissingDerivedVenomInput(true);
        var sessions = new WorldSessionStore(); var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent); sessions.Set(connection, session);
        using var log = new StringWriter();
        var clock = new DerivedAdmissionClock();
        var context = new WorldPacketHandlerContext(connection, log, session, WorldData: data, Sessions: sessions,
            LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        var result = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, session,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.AllowAnimation,
                new LegacyEquipmentQueryPublicationScope(session.Character.Uid)), CancellationToken.None);
        Assert.True(result.Completed); Assert.False(result.Admitted);
        Assert.Equal(LegacyEquipmentQueryPublicationLoss.None, result.Loss);
        Assert.Single(sent);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes(), sent[0]);
        Assert.True(sessions.TryGet(connection, out var current)); Assert.NotNull(current);
        Assert.Equal(session.Character, current.Character);
        Assert.Same(session.PlayerSpells, current.PlayerSpells); Assert.Same(session.PlayerItems, current.PlayerItems);
        Assert.Same(session.PhysicalStatusOverlay, current.PhysicalStatusOverlay);
        Assert.Same(session.FineRainEquipmentAdmission, current.FineRainEquipmentAdmission);
        Assert.False(current.PlayerSpells.ContainsSpell(20_000));
    }

    private static (WorldSession, WorldDataCatalog) MissingDerivedVenomInput(bool denied)
    {
        // This is a negative query component fixture, not a bound native owner
        // or a successful gameplay grant. The item row exists; only the needed
        // derived spell-level row is absent from the declared catalog.
        var item = new PlayerItemSnapshotItem { Uid = 1_991_001, ItemId = 130_003, Position = 3,
            Durability = 10, MaximumDurability = 10, MythSoul = 11 };
        var items = PlayerItemSnapshot.Empty with { EquipmentItems = new[] { item } };
        var source = CreateSession(playerItems: items, attributes: new Dictionary<string, string>
            { ["StatusFlags"] = BuildStatusFlags(denied ? [212] : []) });
        source = source with
        {
            PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(source.Character.Uid, 17, 19),
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(source.Character.Uid, 10_000),
        };
        var data = CreateMagicCatalogWithItems(CreateMonsterFamily(), [],
            [OwnedEquipmentBehaviorFixture.Item(130_003)], []);
        return (source, data);
    }

    private sealed class DerivedAdmissionClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    }
}
