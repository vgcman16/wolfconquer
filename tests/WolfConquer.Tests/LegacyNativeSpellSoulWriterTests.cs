using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellSoulWriterTests
{
    [Fact]
    public Task SoulChangeDoesNotPerformARedundantFallibleMapWriteAfterTheRowStore() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var comparer = new SecondLookupFault();
        fixture.Storage.ClientSpells = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        var stored = new LegacyNativeMsgSpell { ID = 1000, SoulLevel = 1 };
        Assert.True(fixture.Storage.ClientSpells.TryAdd(1000, stored));
        fixture.Packet.SpellCreate(new() { ID = 1000, Level = 7, UseSoul = LegacyNativeMsgSpell.UseSpellSoulTyp.One });
        comparer.Armed = true;

        Assert.Null(Record.Exception(() => LegacyNativeMsgSpell.HandlerSpell(fixture.Owner, fixture.Packet)));

        Assert.Equal(1, comparer.Calls);
        Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.One, stored.UseSoul);
        var response = NativeSpellTestFixture.ReadSpell(Assert.Single(fixture.Queue));
        Assert.Equal((ushort)3, response.Type);
        Assert.Equal((ushort)7, response.Level);
        Assert.Equal(4, fixture.Packet.Position);
    });

    private sealed class SecondLookupFault : IEqualityComparer<ushort>
    {
        internal bool Armed;
        internal int Calls;
        public bool Equals(ushort left, ushort right) => left == right;
        public int GetHashCode(ushort value)
        {
            if (Armed && ++Calls == 2) throw new InvalidOperationException("Redundant spell-map write probe.");
            return value.GetHashCode();
        }
    }
}
