using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOrdinarySpellCallerTests
{
    private static Task Run(Func<Fixture, Task> body, bool lineage = true) =>
        WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(body, lineage: lineage);
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
    private static void AssertNative(Fixture f)
    {
        var current = f.Current; var storage = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells);
        Assert.Same(f.Native, current.NativeGameClient); Assert.Same(f.Player, current.NativePlayer);
        Assert.Equal(current.PlayerSpells.Spells.Select(s => s.Id).Order(), storage.ClientSpells.ToArray().Select(p => p.Key).Order());
        foreach (var pair in storage.ClientSpells.ToArray())
        {
            var row = Assert.Single(current.PlayerSpells.Spells, s => s.Id == pair.Key);
            Assert.Equal(row.Level, pair.Value.Level); Assert.Equal(row.Experience, pair.Value.Experience);
            Assert.Equal(row.PreviousLevel, pair.Value.PreviousLevel); Assert.Equal(row.SoulLevel, pair.Value.SoulLevel);
            Assert.Equal(row.UseSpellSoul, pair.Value.UseSpellSoul);
        }
    }
    private static WorldPacketHandlerContext Context(Fixture f, WorldSession session) =>
        new(f.Connection, f.Log, Session: session, Sessions: f.Store, WorldData: LegacyItemWorldDataFixture.Create(), PlayerSpellsRoot: f.Spells)
        { Trades = f.Trades, TradeOperations = f.Operations, TradeRecovery = f.Recovery };
    private static async Task<bool> Performer(Fixture f, WorldSession session) =>
        await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(Context(f, session), session, 7776, 7, CancellationToken.None);
    private static async Task<bool> FullAccount(Fixture f, WorldSession session)
    {
        using var operation = await f.Operations.EnterSharedAsync(CancellationToken.None);
        return await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(Context(f, session), session,
            465123, 1, string.Empty, CancellationToken.None);
    }
    private static void PreparePerformer(Fixture f)
    {
        var current = f.Current; const string subprof = "0#0#1#6#9#9#";
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["SubProfInfo"] = subprof };
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, current.AccountId, fields,
            detailedIoFailures: true, out var reason), reason);
        var attrs = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["SubProfInfo"] = subprof };
        f.Store.Set(f.Connection, current with { Character = current.Character with { Attributes = attrs } });
        AssertNative(f);
    }
}
