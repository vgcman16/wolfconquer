using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    public Task PrivateSpellRemovalCannotTreatAComparerFailureAsValidAbsence(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        var comparer = new ThirdRemovalHashComparer();
        InstallStatefulSpellMap(scope, comparer);
        var before = new LoadingTransactionWitness(scope);
        comparer.Armed = true;

        var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, scope.Packet));

        before.RequireUnchanged();
        Assert.True(comparer.WrongHashUsed);
        Assert.Equal(3, comparer.TargetReads); // original lookup, private clone, attempted removal
        Assert.IsType<InvalidOperationException>(error);
    });

    [Theory]
    [InlineData("loading")]
    [InlineData("alternate")]
    public Task FailedPrivateSpellInsertionCannotPublishAnUnstoredSkillNotice(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        scope.Spell(16320, 77);
        var comparer = new ThirdInsertionAliasComparer(alias: true);
        InstallStatefulSpellMap(scope, comparer);
        var before = new LoadingTransactionWitness(scope);
        comparer.Armed = true;

        var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));

        before.RequireUnchanged();
        Assert.True(comparer.AliasUsed);
        Assert.Equal(3, comparer.TargetReads); // original membership, candidate membership, TryAdd
        Assert.IsType<InvalidOperationException>(error);
    });

    [Fact]
    public Task StableCollidingSpellComparerRemainsAValidOwnedRefresh() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        var existing = scope.Spell(16320, 77);
        var comparer = new ThirdInsertionAliasComparer(alias: false);
        InstallStatefulSpellMap(scope, comparer);
        var before = new LegacyNativeSpellMutationWitness(scope.Spells);
        comparer.Armed = true;

        scope.Ninja.Alternate();

        Assert.False(comparer.AliasUsed);
        Assert.True(comparer.TargetReads >= 3);
        Assert.Same(comparer, scope.Spells.ClientSpells.Comparer);
        before.RequireEscapedMapUnchanged();
        Assert.Same(existing, scope.Spells.ClientSpells[16320]);
        Assert.Equal((ushort)77, existing.Level);
        Assert.Equal((ushort)9, scope.Spells.ClientSpells[16310].Level);
        scope.Sent(SpellFrame(16310, 9), RemoveFrame(17, 16330));
    });

    private static void InstallStatefulSpellMap(Scope scope, IEqualityComparer<ushort> comparer)
    {
        var map = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        foreach (var pair in scope.Spells.ClientSpells) Assert.True(map.TryAdd(pair.Key, pair.Value));
        scope.Spells.ClientSpells = map;
    }

    private sealed class ThirdRemovalHashComparer : IEqualityComparer<ushort>
    {
        internal bool Armed;
        internal bool WrongHashUsed;
        internal int TargetReads;
        public bool Equals(ushort first, ushort second) => first == second;
        public int GetHashCode(ushort value)
        {
            if (Armed && value == 1000 && ++TargetReads == 3)
            {
                WrongHashUsed = true;
                return 1001;
            }
            return value.GetHashCode();
        }
    }

    private sealed class ThirdInsertionAliasComparer(bool alias) : IEqualityComparer<ushort>
    {
        internal bool Armed;
        internal bool AliasUsed;
        internal int TargetReads;
        public int GetHashCode(ushort value)
        {
            if (Armed && value == 16310) TargetReads++;
            return 0; // Real collisions are valid; only the conditional alias is corrupt.
        }
        public bool Equals(ushort first, ushort second)
        {
            if (alias && Armed && TargetReads == 3 &&
                (first == 16310 && second == 16320 || first == 16320 && second == 16310))
            {
                AliasUsed = true;
                return true;
            }
            return first == second;
        }
    }
}
