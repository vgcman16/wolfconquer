using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    private static Task RunAsync(Func<Fixture, Task> body, PlayerSpellSnapshotEntry[]? rows = null, bool lineage = true,
        bool missingSpellFile = false, bool combined = false, bool training = false) => WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(body, rows, lineage, missingSpellFile, combined, training);

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static LegacyNativeSpellStorage RequireCoherent(Fixture fixture)
    {
        var current = fixture.Current;
        Assert.Same(fixture.Native, current.NativeGameClient);
        Assert.Same(fixture.Player, current.NativePlayer);
        Assert.Same(fixture.Native, fixture.Player.Owner);
        var storage = Assert.IsType<LegacyNativeSpellStorage>(fixture.Native.MySpells);
        Assert.True(storage.IsOwnedBy(fixture.Native));
        var rows = storage.ClientSpells.ToArray(); // Raw observations never invoke the native map comparer.
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), rows.Select(pair => pair.Key).Order());
        foreach (var pair in rows)
        {
            var saved = Assert.Single(current.PlayerSpells.Spells, row => row.Id == pair.Key);
            var row = Assert.IsType<LegacyNativeMsgSpell>(pair.Value);
            Assert.Equal(saved.Id, row.ID);
            Assert.Equal(saved.Level, row.Level);
            Assert.Equal(saved.Experience, row.Experience);
            Assert.Equal(saved.PreviousLevel, row.PreviousLevel);
            Assert.Equal(saved.SoulLevel, row.SoulLevel);
            Assert.Equal(saved.UseSpellSoul, row.UseSpellSoul);
            var selected = saved.UseSpellSoul switch
            {
                0 => LegacyNativeMsgSpell.UseSpellSoulTyp.None,
                1 => LegacyNativeMsgSpell.UseSpellSoulTyp.One,
                2 => LegacyNativeMsgSpell.UseSpellSoulTyp.Two,
                3 => LegacyNativeMsgSpell.UseSpellSoulTyp.Three,
                4 => LegacyNativeMsgSpell.UseSpellSoulTyp.Four,
                _ => throw new InvalidOperationException("Test oracle cannot bless an unrepresentable selected soul."),
            };
            Assert.Equal(selected, row.UseSoul);
        }
        return storage;
    }
}
