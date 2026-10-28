using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOrdinaryPaidSpellCallerTests
{
    private static Task Run(NativePaidSpellCase kind, bool funded, Func<Fixture, Task> body) =>
        WorldServerFrameHandlerTests.WithNativePaidSpellCallerAsync(kind, funded, body);
    private static ushort Learned(NativePaidSpellCase kind) => kind switch
    { NativePaidSpellCase.Mount => 7002, NativePaidSpellCase.Taoist => 12370, NativePaidSpellCase.Trojan => 11990, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    private static long Cost(NativePaidSpellCase kind) => kind == NativePaidSpellCase.Mount ? 100000 : 2150;
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
    private static WorldPacketHandlerContext Context(Fixture f, WorldSession current) =>
        new(f.Connection, f.Log, Session: current, Sessions: f.Store, WorldData: f.PaidCallerWorldData,
            PlayerSpellsRoot: f.Spells, PlayerItemsRoot: Path.Combine(f.Root, "PlayersItems"), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        { Trades = f.Trades, TradeOperations = f.Operations, TradeRecovery = f.Recovery };
    private static async Task<bool> Invoke(Fixture f, NativePaidSpellCase kind, WorldSession current)
    {
        using var operation = await f.Operations.EnterSharedAsync(CancellationToken.None);
        return kind == NativePaidSpellCase.Mount
            ? await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(Context(f, current), current, 5600, 4, CancellationToken.None)
            : await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(Context(f, current), current,
                kind == NativePaidSpellCase.Taoist ? 10000U : 10022U, kind == NativePaidSpellCase.Taoist ? (byte)91 : (byte)17, CancellationToken.None);
    }
    private static void NativeMatches(Fixture f)
    {
        var storage = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells); var current = f.Current;
        Assert.Same(f.Native, current.NativeGameClient); Assert.True(storage.IsOwnedBy(f.Native));
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), storage.ClientSpells.ToArray().Select(pair => pair.Key).Order());
        foreach (var pair in storage.ClientSpells.ToArray())
        {
            var row = Assert.Single(current.PlayerSpells.Spells, row => row.Id == pair.Key);
            Assert.Equal(row.Level, pair.Value.Level); Assert.Equal(row.Experience, pair.Value.Experience);
            Assert.Equal(row.PreviousLevel, pair.Value.PreviousLevel); Assert.Equal(row.SoulLevel, pair.Value.SoulLevel);
            Assert.Equal(row.UseSpellSoul, pair.Value.UseSpellSoul);
        }
    }
    private static CharacterSnapshot SavedCharacter(Fixture f) =>
        CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
    private static void RequireOpaque(Fixture f)
    {
        byte[] bytes = File.ReadAllBytes(f.SpellPath); var rows = PlayerSpellSnapshotLoader.Parse(bytes).Spells;
        int index = Enumerable.Range(0, rows.Count).Single(i => rows[i].Id == 6001);
        Assert.Equal(0xA5, bytes[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11]);
        Assert.Contains("[OwnedOpaque]\nKeep=paid-handler-fixture\n",
            CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _), StringComparison.Ordinal);
    }
}
