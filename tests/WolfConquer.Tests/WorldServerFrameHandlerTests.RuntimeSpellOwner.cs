using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task RuntimeContextRetainsTheActualFrameTradeAndRecoveryOwnersForDerivedSpells()
    {
        using var log = new StringWriter();
        var fixture = await CreateRuntimeSpellOwnerFixtureAsync(log);
        var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection, fixture.Session));
        var trades = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Handler);
        var recovery = typeof(WorldServerFrameHandler).GetField("_tradeRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Handler);
        Assert.Same(trades, context.Trades); Assert.Same(recovery, context.TradeRecovery);
        Assert.Same(fixture.Store, context.Sessions); Assert.Same(fixture.Session, context.Session);
        Assert.Equal(fixture.SpellsRoot, context.PlayerSpellsRoot);
        Assert.True(context.TradeRecovery!.IsReadyForCharacterSpells(fixture.Session.UsersRoot, fixture.SpellsRoot));
        int first = fixture.Sent.Count;

        var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, fixture.Session,
            new(LegacyEquipmentRebuildReason.InitialWorldEntry,
                new LegacyEquipmentQueryPublicationScope(fixture.Session.Character.Uid)), CancellationToken.None);

        Assert.True(query.Completed);
        var spells = fixture.Sent.Skip(first).Select(bytes =>
            LegacySpellPacket.TryParse(ReadFrame(bytes), out var row) ? (LegacySpellPacket?)row : null)
            .Where(row => row is not null).Select(row => row!.Value).ToArray();
        Assert.Equal((ushort)10, Assert.Single(spells, row => row.Id == 21930).Level);
        Assert.Equal((ushort)6, Assert.Single(spells, row => row.Id == 20000).Level);
        Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
        var persisted = new PlayerSpellSnapshotLoader(fixture.SpellsRoot).LoadByCharacterUid(fixture.Session.Character.Uid);
        Assert.Equal(current!.PlayerSpells.Spells, persisted.Spells);
        Assert.DoesNotContain("held native-derived spell membership", log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeDerivedSpellRefusalExplainsMissingOrBlockedActualRecoveryWithoutSpellOutput(bool blocked)
    {
        using var log = new StringWriter();
        var fixture = await CreateRuntimeSpellOwnerFixtureAsync(log);
        var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection, fixture.Session));
        if (blocked) context.TradeRecovery!.Block("owned-runtime-derived-refusal-control");
        else context = context with { TradeRecovery = null };
        var path = Path.Combine(fixture.SpellsRoot, fixture.Session.Character.Uid + ".bin");
        var before = File.ReadAllBytes(path);
        int first = fixture.Sent.Count;

        var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, fixture.Session,
            new(LegacyEquipmentRebuildReason.InitialWorldEntry,
                new LegacyEquipmentQueryPublicationScope(fixture.Session.Character.Uid)), CancellationToken.None);

        Assert.False(query.Completed);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.DoesNotContain(fixture.Sent.Skip(first), bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var row) &&
            row.Id is 21930 or 20000);
        Assert.Contains("held native-derived spell membership", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("reason=spellMutationOwnerUnavailable", log.ToString(), StringComparison.Ordinal);
    }

    private async Task<RuntimeSpellOwnerFixture> CreateRuntimeSpellOwnerFixtureAsync(StringWriter log)
    {
        var users = Path.Combine(_usersRoot, "Users");
        var items = Path.Combine(_usersRoot, "PlayersItems");
        var spells = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(users); Directory.CreateDirectory(items); Directory.CreateDirectory(spells);
        WriteSnapshot(users, 1293600, uid: 1293600, classCode: 7051, minHitPoints: 61229);
        File.WriteAllBytes(Path.Combine(items, "1293600.bin"), PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 500109, position: 4)], []));
        EarnNativeStartupMythSoulInput(items);
        File.WriteAllBytes(Path.Combine(spells, "1293600.bin"), PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(
            [PlayerSpellSnapshotLoaderTests.Entry(21930, level: 10), PlayerSpellSnapshotLoaderTests.Entry(20000, level: 6)]));
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "runtime-owned-fixture", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users, items, spells), handoff,
            worldData: CreateNativeStartupQueryInputCatalog(), runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log);
        var sent = new List<byte[]>(); var connection = CreateSendingConnection(sent);
        await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
        Assert.True(store.TryGet(connection, out var session), log.ToString());
        return new(handler, store, connection, Assert.IsType<WorldSession>(session), spells, sent);
    }

    private sealed record RuntimeSpellOwnerFixture(WorldServerFrameHandler Handler, WorldSessionStore Store,
        ConquerTcpConnection Connection, WorldSession Session, string SpellsRoot, List<byte[]> Sent);
}
