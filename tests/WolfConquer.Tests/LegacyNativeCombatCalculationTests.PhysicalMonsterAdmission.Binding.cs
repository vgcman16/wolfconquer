using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("throw")]
    [InlineData("replace-player")]
    [InlineData("retire-player")]
    [InlineData("replace-vector")]
    [InlineData("change-uid")]
    [InlineData("dirty-word")]
    [InlineData("replace-words")]
    [InlineData("replace-registry")]
    [InlineData("change-prior-row")]
    public Task ComparerDependentStatusDriftIsRejectedBeforePhysicalResultRngSpellOrProcEffects(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var player = s.Player; player.KylinSigil = 2;
        s.Client.PerfectionStatus.LuckyStrike = 100;
        var comparer = InstallPhysicalAdmissionComparer(s.Client, mode);
        var spell = PhysicalAdmissionSpell(); var before = PhysicalAdmissionSpellValues(spell);
        var sentinel = PhysicalAdmissionSentinel(); var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        comparer.Armed = true;

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(player, monster, spell, out result));

        Assert.True(comparer.Triggered); Assert.Same(sentinel, result);
        Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(before, PhysicalAdmissionSpellValues(spell)); prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Fact]
    public Task RetiredPhysicalPlayerIsRejectedEvenWhenItsOwnStatusGraphIsCoherent()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var player = s.Player;
        Assert.True(player.BitVector.TryAdd((int)Flag.Superman, 30, true, 0));
        var vector = new PhysicalAdmissionVectorSnapshot(player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var spell = PhysicalAdmissionSpell(); var before = PhysicalAdmissionSpellValues(spell);
        var replacement = new LegacyNativePlayerRuntime(s.Client) { UID = player.UID };
        s.Client.Player = replacement;
        var sentinel = PhysicalAdmissionSentinel(); var result = sentinel;
        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(player, monster, spell, out result));
        Assert.Same(sentinel, result); Assert.Same(replacement, s.Client.Player); Assert.Same(s.Client, player.Owner);
        Assert.Equal(before, PhysicalAdmissionSpellValues(spell)); vector.AssertUnchanged(); prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Fact]
    public Task ObservingRealComparerUnderAllThreeGatesDoesNotSuppressTheGenuineFullCalculation()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var comparer = InstallPhysicalAdmissionComparer(s.Client, "observe");
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player);
        comparer.Armed = true;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = 500, Damage = 100 }, out var result);
        Assert.True(comparer.Triggered); Assert.Equal(1560u, result.Damage); Assert.Empty(s.Queue);
        vector.AssertUnchanged(); AssertPhysicalAdmissionFourDraws();
    });

    private static PhysicalAdmissionComparer InstallPhysicalAdmissionComparer(LegacyNativeGameClientRuntime client, string mode)
    {
        var vector = client.Player.BitVector;
        var comparer = new PhysicalAdmissionComparer(client, client.Player, vector, mode);
        vector.ArrayFlags = new(comparer)
        {
            [(int)Flag.Stigma] = new((int)Flag.Stigma, 30, true, 0),
            [25] = new(25, 30, false, 0),
        };
        vector.Add((int)Flag.Stigma); vector.Add(25);
        return comparer;
    }

    // This is a genuine collection comparer failure/drift boundary, not an
    // injected membership result or accepting gameplay callback.
    private sealed class PhysicalAdmissionComparer(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector, string mode) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;
        private int calls;
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Assert.True(Monitor.IsEntered(client.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(player.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(vector.SyncRoot));
                calls++;
                if (mode == "change-prior-row" && calls != 2) return value.GetHashCode();
                Armed = false; Triggered = true;
                switch (mode)
                {
                    case "throw": throw new InvalidOperationException("Owned comparer failure");
                    case "replace-player": client.Player = new LegacyNativePlayerRuntime(client) { UID = player.UID }; break;
                    case "retire-player": client.Player = null!; break;
                    case "replace-vector": player.BitVector = new LegacyNativeStatusFlagsBigVector32(576); break;
                    case "change-uid": player.UID++; break;
                    case "dirty-word": vector.Add(500); break;
                    case "replace-words": vector.bits = vector.bits.ToArray(); break;
                    case "replace-registry": vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(vector.ArrayFlags.ToArray()); break;
                    case "change-prior-row":
                        // The other row has already passed IsCurrent's field check.
                        vector.ArrayFlags[value == (int)Flag.Stigma ? 25 : (int)Flag.Stigma].Secounds++;
                        break;
                    case "observe": break;
                    default: throw new InvalidOperationException(mode);
                }
            }
            return value.GetHashCode();
        }
    }
}
