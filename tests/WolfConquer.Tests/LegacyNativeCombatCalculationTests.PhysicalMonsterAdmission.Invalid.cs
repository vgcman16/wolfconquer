using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("null-vector")]
    [InlineData("null-words")]
    [InlineData("empty-words")]
    [InlineData("short-words")]
    [InlineData("null-registry")]
    [InlineData("null-row")]
    [InlineData("key-mismatch")]
    [InlineData("row-no-word")]
    [InlineData("word-no-row")]
    [InlineData("negative-key")]
    [InlineData("aliased-row")]
    [InlineData("deadline-overflow")]
    [InlineData("invoke-overflow")]
    [InlineData("early-stigma-tail")]
    public Task WholePhysicalCallerRejectsUnavailableStatusBeforeEveryDirtyAdmissionPrefix(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        s.Player.KylinSigil = 2; s.Client.PerfectionStatus.LuckyStrike = 100;
        Assert.True(s.Player.BitVector.TryAdd((int)Flag.Cyclone, 30, true, 0));
        s.Player.SendString(s.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-physical");
        Assert.Single(s.Queue);
        var spell = PhysicalAdmissionSpell(); var spellBefore = PhysicalAdmissionSpellValues(spell);
        // Both references are the genuine shared mutable row supplied to the caller.
        var database = new Dictionary<ushort, WolfConquer.Core.Data.LegacyNativeMagicType.Magic> { [spell.ID] = spell };
        CorruptPhysicalAdmission(s.Player, corruption);
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var sentinel = PhysicalAdmissionSentinel(); var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.Same(sentinel, result); Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Same(spell, database[spell.ID]); Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        vector.AssertUnchanged(); prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("null-vector")]
    [InlineData("early-stigma-tail")]
    public Task GenuineFloorBranchStillBypassesStatusAdmissionAndDoesNotFloorTheSharedSpell(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s); monster.IsFloor = true;
        var spell = PhysicalAdmissionSpell(); var before = PhysicalAdmissionSpellValues(spell);
        CorruptPhysicalAdmission(s.Player, corruption);
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var result);
        Assert.Equal(monster.UID, result.UID); Assert.Equal(2u, result.Damage); Assert.Equal(1u, result.Hit);
        Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.None, result.Effect);
        Assert.Equal(before, PhysicalAdmissionSpellValues(spell)); vector.AssertUnchanged(); prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });
}
