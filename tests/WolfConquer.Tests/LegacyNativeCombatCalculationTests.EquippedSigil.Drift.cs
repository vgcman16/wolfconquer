using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("item-level")]
    [InlineData("metadata-power")]
    [InlineData("metadata-damage")]
    [InlineData("null-table")]
    [InlineData("replace-items")]
    [InlineData("alternate")]
    [InlineData("class")]
    public Task EquippedSigilAliasDriftDuringActualStatusAdmissionRejectsBeforeDirtyPrefix(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        Set(s.Player, "_class", 5000u);
        var item = InstallPhysicalWhirlSigil(s);
        var metadata = LegacyNativeNinjaFile.gouyu_type[700];
        var vector = s.Player.BitVector;
        var comparer = new EquippedSigilStatusComparer(s.Client, item, metadata, mode);
        vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(comparer)
        {
            [(int)Flag.Stigma] = new((int)Flag.Stigma, 30, true, 0),
        };
        vector.Add((int)Flag.Stigma);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var spell = new LegacyNativeMagicType.Magic { ID = 16420, Damage = 5 };
        var beforeSpell = PhysicalAdmissionSpellValues(spell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var beforeResult = PhysicalAdmissionResultValues(result);
        comparer.Armed = true;

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.True(comparer.Triggered);
        Assert.Same(sentinel, result);
        Assert.Equal(beforeResult, PhysicalAdmissionResultValues(result));
        Assert.Equal(beforeSpell, PhysicalAdmissionSpellValues(spell));
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("null-items")]
    [InlineData("null-table")]
    [InlineData("metadata-damage")]
    [InlineData("item-level")]
    [InlineData("null-user")]
    public Task PhysicalDamageUsesCapturedSigilScalarsWhenLaterActualLookupMutatesAliases(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s, 2);
        Set(s.Player, "_class", 5000u);
        s.Client.Status.CriticalStrike = 15000;
        var item = InstallPhysicalWhirlSigil(s);
        var metadata = LegacyNativeNinjaFile.gouyu_type[700];
        var comparer = new EquippedSigilLateComparer(s.Client, item, metadata, mode);
        s.Client.ExtraStatus = new ConcurrentDictionary<LegacyNativeRoleStatus.StatuTyp, LegacyNativeRoleStatus>(comparer)
        {
            [LegacyNativeRoleStatus.StatuTyp.IncreasePAttack] = new(0, 0) { Stamp = DateTime.Now.AddDays(1) },
        };
        var hitpoints = monster.HitPoints;
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        comparer.Armed = true;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 16420, Damage = 100 }, out var result);

        Assert.True(comparer.Triggered);
        Assert.Equal(2880u, result.Damage);
        Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike, result.Effect);
        Assert.Equal(hitpoints, monster.HitPoints);
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        Assert.Empty(s.Queue);
    });

    private static void MutateEquippedSigilAliases(LegacyNativeGameClientRuntime client,
        LegacyNativeNinja.Item item, LegacyNativeNinjaFile.NinjaSprintItem metadata, string mode)
    {
        switch (mode)
        {
            case "item-level": item.Level++; break;
            case "metadata-power": metadata.Power++; break;
            case "metadata-damage": metadata.Damage = 99; break;
            case "null-table": LegacyNativeNinjaFile.gouyu_type = null!; break;
            case "replace-items": client.MyNinja!.Items = new(); break;
            case "null-items": client.MyNinja!.Items = null!; break;
            case "alternate": client.Equipment!.Alternante = !client.Equipment.Alternante; break;
            case "class": Set(client.Player, "_class", 2000u); break;
            case "null-user": client.MyNinja!.user = null!; break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private sealed class EquippedSigilStatusComparer(LegacyNativeGameClientRuntime client,
        LegacyNativeNinja.Item item, LegacyNativeNinjaFile.NinjaSprintItem metadata, string mode) : IEqualityComparer<int>
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
                Armed = false; Triggered = true;
                MutateEquippedSigilAliases(client, item, metadata, mode);
            }
            return value.GetHashCode();
        }
    }

    private sealed class EquippedSigilLateComparer(LegacyNativeGameClientRuntime client,
        LegacyNativeNinja.Item item, LegacyNativeNinjaFile.NinjaSprintItem metadata, string mode)
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
                Armed = false; Triggered = true;
                MutateEquippedSigilAliases(client, item, metadata, mode);
            }
            return value.GetHashCode();
        }
    }
}
