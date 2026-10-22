using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using HW = WolfConquer.Core.World.LegacyNativeHundredWeapons;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Fact]
    public Task LockedAddStoresOnlyPhysicalSubtypeKeyAndOverloadCreatesFiveZeroAttributes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var prior = scope.Item(Weapon.Sword, 1, Weapon.Blade);
        Assert.True(scope.Owner.AddWeapon(Weapon.Blade, 9));
        var added = scope.Owner.Objects[Weapon.Blade];
        Assert.Same(prior, scope.Owner.Objects[Weapon.Sword]); Assert.NotSame(prior, added);
        Assert.Equal((byte)9, added.Level); Assert.Equal(0u, added.Progress); Assert.Equal((byte)0, added.AppearancePosition);
        Assert.Equal(5, added.Attributes.Count); Assert.All(added.Attributes.Values, value => Assert.Equal(0, value));
        Assert.Equal(new uint[] { 0 }, added.BitVector.bits);
        var replacement = new LegacyNativeHundredWeaponInfo { WeaponSubtype = Weapon.Blade, Level = 2 };
        Assert.True(scope.Owner.AddWeapon(replacement)); Assert.Same(replacement, scope.Owner.Objects[Weapon.Blade]);
        Assert.Empty(scope.Metadata); Assert.Empty(scope.Queue); // Locked admission does not consult missing DBInfo.
    });

    [Fact]
    public Task InvalidAddStillRemovesRanksWhileNullItemFailsBeforeThatPostlude() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Rank(LegacyNativeHWRank.Type.Main);
        Assert.Throws<NullReferenceException>(() => scope.Owner.AddWeapon(null!));
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList);
        Assert.False(scope.Owner.AddWeapon(Weapon.Blade, 0));
        Assert.Empty(scope.Owner.Objects); Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task AddLearnsRealSpellThenSendsInfoThenPublishesSubtypeAndMainRanks() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = scope.Ready(Weapon.Blade); scope.Owner.Objects.Clear(); scope.Owner.Unlocked = true;
        Assert.True(scope.Owner.AddWeapon(item, true));
        Assert.Same(item, scope.Owner.Objects[Weapon.Blade]); Assert.Equal((ushort)5, scope.Spells.ClientSpells[12000].Level);
        Assert.Equal(2, scope.Queue.Count); Assert.Equal(SpellFrame(12000, 5), scope.Queue.ElementAt(0));
        Assert.Equal(Hex("2B003009080210111A21089A03100018002004280030904E38A09C0140B0EA0148C0B80250D0860360A01F"), scope.Queue.ElementAt(1));
        Assert.Equal(4000u, LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList[17].TotalPoints);
        Assert.Equal(4000u, LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList[17].TotalPoints);
        Assert.False(scope.Owner.ValidateSpells()); Assert.Equal(2, scope.Queue.Count);
    });

    [Fact]
    public Task LateRankFaultRetainsStoredWeaponLearntSpellAndBothPriorFrames() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = scope.Ready(Weapon.Blade); scope.Owner.Objects.Clear(); scope.Owner.Unlocked = true;
        LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.AddWeapon(item, true));
        Assert.Same(item, scope.Owner.Objects[Weapon.Blade]); Assert.True(scope.Spells.ClientSpells.ContainsKey(12000));
        Assert.Equal(new ushort[] { 2101, 2352 }, scope.Queue.Select(Opcode));
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList);
        Assert.Null(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main]);
    });

    [Fact]
    public Task ValidateSpellsRemovesMappedArchiveWhenLockedButKeepsPhysicalKeyAliasesAndSparseRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Meta(Weapon.Blade, 1, 12000);
        scope.Magic(12000, 0); scope.Magic(12000, 1);
        scope.Spells.ClientSpells.TryAdd(12000, new LegacyNativeMsgSpell { ID = 12000, Level = 0 });
        scope.Spells.ClientSpells.TryAdd(44, new LegacyNativeMsgSpell { ID = 12000, Level = 0 });
        scope.Spells.ClientSpells.TryAdd(12001, new LegacyNativeMsgSpell { ID = 12001, Level = 5 });
        scope.Magic(12001, 0);
        Assert.False(scope.Owner.ValidateSpells());
        Assert.False(scope.Spells.ClientSpells.ContainsKey(12000)); Assert.True(scope.Spells.ClientSpells.ContainsKey(44));
        Assert.True(scope.Spells.ClientSpells.ContainsKey(12001));
        Assert.Equal((ushort)2046, Opcode(Assert.Single(scope.Queue))); Assert.Equal("", scope.Log);
    });

    [Fact]
    public Task ValidateSpellsRetainsCountGateGapDiagnosticAndMissingMetadataFaultPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spells.ClientSpells.TryAdd(12001, new LegacyNativeMsgSpell { ID = 12001, Level = 3 });
        foreach (ushort index in new ushort[] { 0, 1, 2, 4 }) scope.Magic(12001, index);
        Assert.False(scope.Owner.ValidateSpells());
        Assert.Equal("ERORR [SPELL LOAD] Id: 12001 Level 3" + Environment.NewLine, scope.Log);
        var item = scope.Item(Weapon.Blade, 4); scope.Owner.Unlocked = true;
        Assert.Throws<NullReferenceException>(() => scope.Owner.ValidateSpells());
        Assert.Same(item, scope.Owner.Objects[Weapon.Blade]); Assert.True(scope.Spells.ClientSpells.ContainsKey(12001)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ValidationSkipsNullNonArchiveAndUnmappedMagicUntilAGenuineMetadataMatchExists() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spells.ClientSpells.TryAdd(12000, new LegacyNativeMsgSpell { ID = 12000, Level = 0 });
        var magic = scope.Magic(12000, 0, 0, false); scope.Magic(12000, 1);
        LegacyNativePoolMagic.Magic[12000][0] = null!;
        Assert.False(scope.Owner.ValidateSpells()); Assert.True(scope.Spells.ClientSpells.ContainsKey(12000));
        LegacyNativePoolMagic.Magic[12000][0] = magic;
        Assert.False(scope.Owner.ValidateSpells()); Assert.True(scope.Spells.ClientSpells.ContainsKey(12000));
        magic.isTrojanArchiveSkill = true;
        Assert.False(scope.Owner.ValidateSpells()); Assert.True(scope.Spells.ClientSpells.ContainsKey(12000)); Assert.Empty(scope.Queue);
        scope.Meta(Weapon.Blade, 1, 12000);
        Assert.False(scope.Owner.ValidateSpells()); Assert.Empty(scope.Spells.ClientSpells);
        Assert.Equal((ushort)2046, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task RemoveWeaponReallyAddsMissingSpellBeforeDeletionAndDoesNotRemoveExistingSpell() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Ready(Weapon.Blade); scope.Rank(LegacyNativeHWRank.Type.Dominator); scope.Rank(LegacyNativeHWRank.Type.Main);
        Assert.True(scope.Owner.RemoveWeapon(Weapon.Blade, true));
        Assert.Empty(scope.Owner.Objects); Assert.Equal(SpellFrame(12000, 5), scope.Queue.ElementAt(0));
        Assert.Equal(Hex("0800300908021011"), scope.Queue.ElementAt(1)); Assert.Equal(2, scope.Queue.Count);
        Assert.True(scope.Spells.ClientSpells.ContainsKey(12000));
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList);
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList);
        Assert.True(scope.Owner.RemoveWeapon(Weapon.Blade, true)); Assert.Equal(2, scope.Queue.Count);
        scope.Item(Weapon.Blade, 4); Assert.True(scope.Owner.RemoveWeapon(Weapon.Blade));
        Assert.True(scope.Spells.ClientSpells.ContainsKey(12000)); Assert.Equal(2, scope.Queue.Count);
    });

    [Fact]
    public Task RemoveInfoFaultLeavesDeletionAndSpellSendButPreventsLaterRankRemoval() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Ready(Weapon.Blade); scope.Rank(LegacyNativeHWRank.Type.Dominator); scope.Rank(LegacyNativeHWRank.Type.Main);
        scope.Client.HundredWeapons = null!;
        Assert.Throws<NullReferenceException>(() => scope.Owner.RemoveWeapon(Weapon.Blade, true));
        Assert.Empty(scope.Owner.Objects); Assert.Equal(SpellFrame(12000, 5), Assert.Single(scope.Queue));
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList);
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList);
    });
}
