using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("unowned")]
    [InlineData("foreign")]
    public Task WholePhysicalCallerRejectsInvalidEquipmentBeforeOutputRngOrPublication(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Player.KylinSigil = 2;
        s.Client.PerfectionStatus.LuckyStrike = 100;
        s.Player.SendString(s.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-equipment");
        InvalidateMinimumDamageEquipment(s.Client, corruption);
        var equipment = s.Client.Equipment;
        var spell = PhysicalAdmissionSpell();
        var spellBefore = PhysicalAdmissionSpellValues(spell);
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.Same(equipment, s.Client.Equipment);
        Assert.Same(sentinel, result);
        Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        vector.AssertUnchanged();
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("replace-owned")]
    [InlineData("change-weapons")]
    [InlineData("change-level")]
    public Task EquipmentDriftDuringActualStatusAdmissionRejectsBeforeDirtyCalculationPrefix(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Player.KylinSigil = 2;
        s.Client.PerfectionStatus.LuckyStrike = 100;
        var equipmentBefore = s.Client.Equipment;
        var vector = s.Player.BitVector;
        var comparer = new MinimumDamageEquipmentComparer(s.Client, mode);
        vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(comparer)
        {
            [(int)Flag.Stigma] = new((int)Flag.Stigma, 30, true, 0),
        };
        vector.Add((int)Flag.Stigma);
        var vectorBefore = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var spell = PhysicalAdmissionSpell();
        var spellBefore = PhysicalAdmissionSpellValues(spell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);
        comparer.Armed = true;

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.True(comparer.Triggered);
        if (mode is "change-weapons" or "change-level")
        {
            Assert.Same(equipmentBefore, s.Client.Equipment);
        }
        else
        {
            Assert.NotSame(equipmentBefore, s.Client.Equipment);
        }
        Assert.Same(comparer.Replacement, s.Client.Equipment);
        Assert.Same(sentinel, result);
        Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        vectorBefore.AssertUnchanged();
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("unowned")]
    [InlineData("foreign")]
    public Task GenuineFloorMonsterStillBypassesUnneededEquipmentOwner(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        monster.IsFloor = true;
        InvalidateMinimumDamageEquipment(s.Client, corruption);
        var equipment = s.Client.Equipment;
        var spell = PhysicalAdmissionSpell();
        var spellBefore = PhysicalAdmissionSpellValues(spell);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var result);

        Assert.Same(equipment, s.Client.Equipment);
        Assert.Equal(monster.UID, result.UID);
        Assert.Equal(2u, result.Damage);
        Assert.Equal(1u, result.Hit);
        Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.None, result.Effect);
        Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Fact]
    public Task ActualOwnedEquipmentPreservesWholeMinimumDamageFormulaAndFourDrawOrder()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Client.Status.MinAttack = s.Client.Status.MaxAttack = 1;
        s.Client.Equipment!.LeftWeapon = 420009;
        s.Client.Equipment.RightWeapon = 410008;
        var equipment = s.Client.Equipment;
        var hitpoints = monster.HitPoints;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var result);

        // 1 + level100/10 + left9 + right8 = 28; same-level120% = 33.
        Assert.Equal(33u, result.Damage);
        Assert.Same(equipment, s.Client.Equipment);
        Assert.Equal(hitpoints, monster.HitPoints);
        Assert.Empty(s.Queue);
        AssertPhysicalAdmissionFourDraws();
    });

    private static void InvalidateMinimumDamageEquipment(LegacyNativeGameClientRuntime client, string corruption)
    {
        switch (corruption)
        {
            case "missing": client.Equipment = null; break;
            case "unowned": client.Equipment = new LegacyNativeEquipItemStorage(null); break;
            case "foreign":
                var foreign = new LegacyNativeGameClientRuntime();
                foreign.Player.UID = client.Player.UID;
                client.Equipment = new LegacyNativeEquipItemStorage(foreign);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("change-weapons")]
    public Task WholePhysicalCallerUsesCapturedMinimumWhenActualLaterLookupChangesEquipment(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Client.Status.MinAttack = s.Client.Status.MaxAttack = 1;
        var originalEquipment = s.Client.Equipment!;
        originalEquipment.LeftWeapon = 420009;
        originalEquipment.RightWeapon = 410008;
        var comparer = new MinimumDamageLateLookupComparer(s.Client, mode);
        s.Client.ExtraStatus = new ConcurrentDictionary<LegacyNativeRoleStatus.StatuTyp, LegacyNativeRoleStatus>(comparer)
        {
            [LegacyNativeRoleStatus.StatuTyp.IncreasePAttack] = new(0, 0) { Stamp = DateTime.Now.AddDays(1) },
        };
        var hitpoints = monster.HitPoints;
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        comparer.Armed = true;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, null, out var result);

        Assert.True(comparer.Triggered);
        Assert.Equal(33u, result.Damage);
        Assert.Equal(hitpoints, monster.HitPoints);
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        if (mode == "missing")
        {
            Assert.Null(s.Client.Equipment);
        }
        else
        {
            Assert.Same(originalEquipment, s.Client.Equipment);
            Assert.Equal(0u, originalEquipment.LeftWeapon);
            Assert.Equal(0u, originalEquipment.RightWeapon);
        }

        Assert.Empty(s.Queue);
        AssertPhysicalAdmissionFourDraws();
    });

    // This later, genuine ExtraStatus lookup is after admission and the first
    // damage roll; it demonstrates that the minimum no longer reads live owners.
    private sealed class MinimumDamageLateLookupComparer(LegacyNativeGameClientRuntime client, string mode)
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
                if (mode == "missing")
                {
                    client.Equipment = null;
                }
                else
                {
                    client.Equipment!.LeftWeapon = 0;
                    client.Equipment.RightWeapon = 0;
                }
            }

            return value.GetHashCode();
        }
    }

    // A real dictionary comparer mutates the required owner while the existing
    // status admission traverses that dictionary. It does not fake a query result.
    private sealed class MinimumDamageEquipmentComparer(LegacyNativeGameClientRuntime client, string mode) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;
        internal LegacyNativeEquipItemStorage? Replacement;

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
                if (mode == "replace-owned")
                {
                    client.Equipment = new LegacyNativeEquipItemStorage(client);
                }
                else if (mode == "change-weapons")
                {
                    client.Equipment!.LeftWeapon++;
                }
                else if (mode == "change-level")
                {
                    Set(client.Player, "_level", (ushort)(client.Player.Level + 1));
                }
                else
                {
                    InvalidateMinimumDamageEquipment(client, mode);
                }

                Replacement = client.Equipment;
            }

            return value.GetHashCode();
        }
    }
}
