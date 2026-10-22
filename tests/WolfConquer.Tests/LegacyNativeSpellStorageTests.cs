using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellStorageTests
{
    [Theory]
    [InlineData((ushort)1104, true)]
    [InlineData((ushort)1105, false)]
    [InlineData((ushort)1106, true)]
    [InlineData((ushort)3999, true)]
    [InlineData((ushort)4000, false)]
    [InlineData((ushort)4070, false)]
    [InlineData((ushort)4071, true)]
    public Task AddKeepsNativeExclusionsAndAdjacentAcceptedIds(ushort id, bool accepted) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Storage.Add(f.Packet, id, 9, 2, 7, -13);
        Assert.Equal(accepted, f.Storage.ClientSpells.ContainsKey(id));
        Assert.Equal(accepted ? 1 : 0, f.Queue.Count);
        if (!accepted) return;
        var stored = f.Storage.ClientSpells[id];
        Assert.Equal((ushort)9, stored.Level);
        Assert.Equal((byte)7, stored.PreviousLevel);
        Assert.Equal((byte)2, stored.SoulLevel);
        Assert.Equal(-13, stored.Experience);
        var sent = NativeSpellTestFixture.ReadSpell(f.Queue.Dequeue());
        Assert.Equal(id, sent.ID);
        Assert.Equal(18, sent.HashLevelSoul);
        Assert.Equal(-13, sent.Experience);
    });

    [Fact]
    public Task RepeatedAddReplacesEachObjectAndSendsViaItsActualStorageOwner() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var otherQueue = new Queue<byte[]>();
        var holder = NativeSpellTestFixture.CreateOwner(otherQueue);
        holder.MySpells = f.Storage;
        f.Storage.Add(f.Packet, 1000, 8, 3, 6, 123);
        var first = f.Storage.ClientSpells[1000];
        holder.MySpells.Add(f.Packet, 1000, 8, 3, 6, 123);
        var second = f.Storage.ClientSpells[1000];
        Assert.NotSame(first, second);
        Assert.Equal(2, f.Queue.Count);
        Assert.Empty(otherQueue);
        Assert.Equal(f.Queue.Dequeue(), f.Queue.Dequeue());
        f.Storage.Add(f.Packet, 1000, Experience: 999, ClearExp: true);
        Assert.NotSame(second, f.Storage.ClientSpells[1000]);
        Assert.Equal(0, f.Storage.ClientSpells[1000].Experience);
        Assert.Equal(0, NativeSpellTestFixture.ReadSpell(Assert.Single(f.Queue)).Experience);
    });

    [Fact]
    public Task AddPublishesBeforeNullStreamFaultButExcludedIdsDoNotTouchIt() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Storage.Add(null!, 1105);
        f.Storage.Add(null!, 4000);
        Assert.Empty(f.Storage.ClientSpells);
        Assert.Throws<NullReferenceException>(() => f.Storage.Add(null!, 1000, 8));
        Assert.Equal((ushort)8, f.Storage.ClientSpells[1000].Level);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task RemoveDeletesBeforeActionWriterFaultAndMissingIdsDoNotReadStream() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Storage.ClientSpells[1000] = new LegacyNativeMsgSpell { ID = 1000 };
        Assert.Throws<NullReferenceException>(() => f.Storage.Remove(1000, null!));
        Assert.Empty(f.Storage.ClientSpells);
        f.Storage.Remove(1000, null!);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task RemoveSendsTheCurrentActualOwnerUidAndRemovedDictionaryKey() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Storage.ClientSpells[1001] = new LegacyNativeMsgSpell { ID = 2000 };
        f.Owner.Player.UID = 73;
        f.Storage.Remove(1001, f.Packet);
        Assert.Empty(f.Storage.ClientSpells);
        Assert.Equal(Convert.FromHexString("1300FE07084918E9074800606D680070007800"), Assert.Single(f.Queue));
    });

    [Fact]
    public Task CheckSpellUsesDictionaryKeyAndCurrentObjectLevel() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var value = new LegacyNativeMsgSpell { ID = 2000, Level = 8 };
        f.Storage.ClientSpells[1000] = value;
        Assert.True(f.Storage.CheckSpell(1000, 8));
        Assert.False(f.Storage.CheckSpell(1000, 9));
        Assert.False(f.Storage.CheckSpell(2000, 0));
        value.Level = 9;
        Assert.True(f.Storage.CheckSpell(1000, 9));
        Assert.Same(value, f.Storage.ClientSpells[1000]);
    });

    [Fact]
    public Task RebornRetainsSoulButTruncatesPriorLevelAndResetsOtherSpellState() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var old = new LegacyNativeMsgSpell { ID = 1000, Level = 300, Experience = 123, UseSpellSoul = 2, SoulLevel = 3 };
        f.Storage.ClientSpells[1000] = old;
        Assert.True(f.Storage.RebornSpell(f.Packet, 1000));
        var fresh = f.Storage.ClientSpells[1000];
        Assert.NotSame(old, fresh);
        Assert.Equal((ushort)0, fresh.Level);
        Assert.Equal((byte)44, fresh.PreviousLevel);
        Assert.Equal((byte)3, fresh.SoulLevel);
        Assert.Equal(274, fresh.HashLevelSoul);
        Assert.Equal(0, fresh.Experience);
        Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.None, fresh.UseSoul);
        Assert.False(f.Storage.RebornSpell(null!, 2000));
        Assert.Single(f.Queue);
    });

    [Fact]
    public Task RebornPublicationIsRetainedIfItsSubsequentPacketWriteFails() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var old = new LegacyNativeMsgSpell { ID = 1000, Level = 300, SoulLevel = 2 };
        f.Storage.ClientSpells[1000] = old;
        Assert.Throws<NullReferenceException>(() => f.Storage.RebornSpell(null!, 1000));
        Assert.NotSame(old, f.Storage.ClientSpells[1000]);
        Assert.Equal((byte)44, f.Storage.ClientSpells[1000].PreviousLevel);
        Assert.Equal((ushort)0, f.Storage.ClientSpells[1000].Level);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task RebornOfAnExcludedPreseededIdRemovesItAndStillReturnsTrue() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Storage.ClientSpells[1105] = new LegacyNativeMsgSpell { ID = 1105, Level = 9 };
        Assert.True(f.Storage.RebornSpell(null!, 1105));
        Assert.Empty(f.Storage.ClientSpells);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task ClearRebornsStaticEntriesAndRemovesOthersWithoutDependingOnEnumerationOrder() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var retained = new LegacyNativeMsgSpell { ID = 1000, Level = 9, SoulLevel = 2 };
        f.Storage.ClientSpells[1000] = retained;
        f.Storage.ClientSpells[1001] = new LegacyNativeMsgSpell { ID = 1001, Level = 7 };
        f.Storage.ClearSpells(new List<ushort> { 1000 }, f.Packet);
        Assert.Single(f.Storage.ClientSpells);
        Assert.NotSame(retained, f.Storage.ClientSpells[1000]);
        Assert.Equal((byte)9, f.Storage.ClientSpells[1000].PreviousLevel);
        Assert.Equal((ushort)0, f.Storage.ClientSpells[1000].Level);
        Assert.Equal(new ushort[] { 2046, 2101 }, f.Queue.Select(x => BitConverter.ToUInt16(x, 2)).OrderBy(x => x));
    });

    [Fact]
    public Task UpdateCopiesOnlyNativeAddInputsIntoANewStoredObject() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var input = new LegacyNativeMsgSpell { ID = 1000, Level = 8, SoulLevel = 3, PreviousLevel = 7,
            Experience = -9, UseSpellSoul = 2, Type = 12, Soul = 11, IsSpellWithColdTime = true };
        f.Storage.Update(f.Packet, input);
        var stored = f.Storage.ClientSpells[1000];
        Assert.NotSame(input, stored);
        Assert.Equal((ushort)8, stored.Level);
        Assert.Equal((byte)3, stored.SoulLevel);
        Assert.Equal((byte)7, stored.PreviousLevel);
        Assert.Equal(-9, stored.Experience);
        Assert.Equal((ushort)0, stored.Type);
        Assert.Equal((byte)0, stored.Soul);
        Assert.False(stored.IsSpellWithColdTime);
        Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.None, stored.UseSoul);
        Assert.Single(f.Queue);
    });

    [Fact]
    public Task SendAllKeepsPhysicalObjectsAndDuplicateValueAliases() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var value = new LegacyNativeMsgSpell { ID = 2000, Level = 8 };
        f.Storage.ClientSpells[1000] = value;
        f.Storage.ClientSpells[1001] = value;
        f.Storage.SendAll(f.Packet);
        Assert.Equal(2, f.Queue.Count);
        Assert.Equal(f.Queue.Dequeue(), f.Queue.Dequeue());
        Assert.Same(value, f.Storage.ClientSpells[1000]);
        Assert.Same(value, f.Storage.ClientSpells[1001]);
        Assert.Equal((ushort)8, value.Level);
    });
}
