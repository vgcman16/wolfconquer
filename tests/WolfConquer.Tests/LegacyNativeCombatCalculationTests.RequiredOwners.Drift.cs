using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("missing-rune")]
    [InlineData("replace-gear")]
    [InlineData("missing-ninja")]
    [InlineData("missing-family")]
    [InlineData("missing-name")]
    public Task RequiredPhysicalOwnerDriftDuringActualStatusTraversalRejectsBeforeDirtyPrefix(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        var spell = new LegacyNativeMagicType.Magic { ID = 16420, Damage = 5 };
        var vector = s.Player.BitVector;
        var comparer = new RequiredOwnersAdmissionComparer(s.Client, monster, mode);
        vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(comparer)
        {
            [(int)Flag.Stigma] = new((int)Flag.Stigma, 30, true, 0),
        };
        vector.Add((int)Flag.Stigma);
        var vectorBefore = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var spellBefore = PhysicalAdmissionSpellValues(spell);
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);
        comparer.Armed = true;

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.True(comparer.Triggered);
        Assert.Same(sentinel, result);
        Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        vectorBefore.AssertUnchanged();
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("missing-rune")]
    [InlineData("missing-gear")]
    [InlineData("missing-ninja")]
    [InlineData("missing-family")]
    [InlineData("missing-name")]
    public Task CapturedPhysicalBindingsAvoidLateNullableSlotRereadsAfterAdmission(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        var comparer = new RequiredOwnersLateLookupComparer(s.Client, monster, mode);
        s.Client.ExtraStatus = new ConcurrentDictionary<LegacyNativeRoleStatus.StatuTyp, LegacyNativeRoleStatus>(comparer)
        {
            [LegacyNativeRoleStatus.StatuTyp.IncreasePAttack] = new(0, 0) { Stamp = DateTime.Now.AddDays(1) },
        };
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var hitpoints = monster.HitPoints;
        comparer.Armed = true;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out var result);

        Assert.True(comparer.Triggered);
        Assert.Equal(1200u, result.Damage);
        Assert.Equal(hitpoints, monster.HitPoints);
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        Assert.Empty(s.Queue);
        AssertPhysicalAdmissionFourDraws();
    });

    // This is a genuine comparer of the actual status graph, not a fake query result.
    private sealed class RequiredOwnersAdmissionComparer(
        LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole monster, string mode) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;

        public bool Equals(int left, int right) => left == right;

        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Assert.True(Monitor.IsEntered(client.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(client.Player.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(client.Player.BitVector.SyncRoot));
                Armed = false;
                Triggered = true;
                CorruptRequiredPhysicalOwner(client, monster, mode);
            }

            return value.GetHashCode();
        }
    }

    private sealed class RequiredOwnersLateLookupComparer(
        LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole monster, string mode)
        : IEqualityComparer<LegacyNativeRoleStatus.StatuTyp>
    {
        internal bool Armed;
        internal bool Triggered;

        public bool Equals(LegacyNativeRoleStatus.StatuTyp left, LegacyNativeRoleStatus.StatuTyp right) => left == right;

        public int GetHashCode(LegacyNativeRoleStatus.StatuTyp value)
        {
            if (Armed && value == LegacyNativeRoleStatus.StatuTyp.IncreasePAttack)
            {
                Assert.False(Monitor.IsEntered(client.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(client.Player.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(client.Player.BitVector.SyncRoot));
                Armed = false;
                Triggered = true;
                CorruptRequiredPhysicalOwner(client, monster, mode);
            }

            return value.GetHashCode();
        }
    }
}
