using WolfConquer.Core.World;
using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Fact]
    public Task UpdateStatusUsesAllSevenPhysicalIdsActualMetadataAndAdditiveRepeatedEffects() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var types = new[] { Sort.Apothecary, Sort.ChiMaster, Sort.MartialArtist, Sort.Performer, Sort.Sage, Sort.Warlock, Sort.Wrangler };
        uint[][] damages = [[2], [3], [5], [7, 11], [13], [17], [19]];
        for (int index = 0; index < types.Length; index++)
        {
            scope.State.Row((Sort)(100 + index), types[index]);
            scope.State.Damage(types[index], 2, damages[index]);
        }
        var status = scope.Client.Status;
        status.Detoxication = 11; status.Immunity = 13; status.CriticalStrike = 17;
        status.MagicAttack = 19; status.MaxAttack = 23; status.MinAttack = 29;
        status.Penetration = 31; status.SkillCStrike = 37; status.MaxHitpoints = 41; status.Defence = 99;
        scope.Runtime.UpdateStatus(scope.Client);
        Assert.Equal(new uint[] { 13, 313, 517, 26, 34, 40, 1331, 1737, 60 },
            new[] { status.Detoxication, status.Immunity, status.CriticalStrike, status.MagicAttack,
                status.MaxAttack, status.MinAttack, status.Penetration, status.SkillCStrike, status.MaxHitpoints });
        damages[3][1] = 23; // Mutate the genuine retained metadata array, not a result provider.
        scope.Runtime.UpdateStatus(scope.Client);
        Assert.Equal(new uint[] { 15, 613, 1017, 33, 57, 63, 2631, 3437, 79 },
            new[] { status.Detoxication, status.Immunity, status.CriticalStrike, status.MagicAttack,
                status.MaxAttack, status.MinAttack, status.Penetration, status.SkillCStrike, status.MaxHitpoints });
        Assert.Equal(99u, status.Defence);
        Assert.Same(status, scope.Client.Status);
        scope.Unwritten();
    });

    [Fact]
    public Task UpdateStatusKeepsUnsignedMultiplyAndAdditionWrap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        foreach (Sort type in new[] { Sort.MartialArtist, Sort.Performer, Sort.Wrangler })
        {
            scope.State.Row(type, type);
            uint[] damage = type == Sort.Performer ? [uint.MaxValue, uint.MaxValue] : [uint.MaxValue];
            scope.State.Damage(type, 2, damage);
        }
        var status = scope.Client.Status;
        status.CriticalStrike = 10; status.MaxHitpoints = 17;
        status.MagicAttack = 5; status.MaxAttack = 11; status.MinAttack = 13;
        scope.Runtime.UpdateStatus(scope.Client);
        Assert.Equal(4294967206u, status.CriticalStrike);
        Assert.Equal(16u, status.MaxHitpoints);
        Assert.Equal(4u, status.MagicAttack);
        Assert.Equal(10u, status.MaxAttack);
        Assert.Equal(12u, status.MinAttack);
        scope.Unwritten();
    });

    [Fact]
    public Task UnknownIdsSkipMetadataAndClientStatusAccessEntirely() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        foreach (byte value in new byte[] { 0, 7, 8, 255 }) scope.State.Row((Sort)value, (Sort)value);
        LegacyNativePoolSubClassInfo.SubClassInfo = null!;
        scope.Runtime.UpdateStatus(null!);
        scope.Client.Status = null!;
        scope.Runtime.UpdateStatus(scope.Client);
        scope.Unwritten();
    });

    [Fact]
    public void GetDamageReturnsActualMutableArrayAndRetainsDictionaryFaults()
    {
        using var state = new State();
        Assert.Throws<KeyNotFoundException>(() => state.Metadata.GetDamage(Sort.Apothecary, 2));
        state.Metadata[Sort.Apothecary] = new();
        Assert.Throws<KeyNotFoundException>(() => state.Metadata.GetDamage(Sort.Apothecary, 2));
        uint[] array = state.Damage(Sort.Apothecary, 2, 7, 11);
        Assert.Same(array, state.Metadata.GetDamage(Sort.Apothecary, 2));
        array[0] = 17;
        Assert.Equal(17u, state.Metadata.GetDamage(Sort.Apothecary, 2)[0]);
        state.Metadata[Sort.Apothecary][2] = null!;
        Assert.Throws<NullReferenceException>(() => state.Metadata.GetDamage(Sort.Apothecary, 2));
        state.Metadata[Sort.Apothecary] = null!;
        Assert.Throws<NullReferenceException>(() => state.Metadata.GetDamage(Sort.Apothecary, 2));
    }

    [Fact]
    public Task PerformerArrayFaultsExposeExactMagicThenMaxThenMinMutationOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.State.Row(Sort.Performer, Sort.Performer);
        scope.State.Damage(Sort.Performer, 2, []);
        var status = scope.Client.Status;
        status.MagicAttack = 19; status.MaxAttack = 23; status.MinAttack = 29;
        Assert.Throws<IndexOutOfRangeException>(() => scope.Runtime.UpdateStatus(scope.Client));
        Assert.Equal(19u, status.MagicAttack);
        scope.State.Metadata[Sort.Performer][2].Damage = [7];
        Assert.Throws<IndexOutOfRangeException>(() => scope.Runtime.UpdateStatus(scope.Client));
        Assert.Equal(26u, status.MagicAttack);
        Assert.Equal(23u, status.MaxAttack);
        Assert.Equal(29u, status.MinAttack);
        scope.Unwritten();
    });

    [Fact]
    public Task EarlierEnumeratedStatusMutationSurvivesLaterMissingMetadataFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.State.Row(Sort.MartialArtist, Sort.MartialArtist);
        scope.State.Row(Sort.Warlock, Sort.Warlock);
        var rows = scope.Runtime.src.Values.ToArray();
        rows[0].ID = Sort.Apothecary;
        rows[1].ID = Sort.ChiMaster;
        scope.State.Damage(Sort.Apothecary, 2, 7);
        scope.Client.Status.Detoxication = 11;
        Assert.Throws<KeyNotFoundException>(() => scope.Runtime.UpdateStatus(scope.Client));
        Assert.Equal(18u, scope.Client.Status.Detoxication);
        Assert.Equal(0u, scope.Client.Status.Immunity);
        scope.Unwritten();
    });

    [Fact]
    public Task NullStoredClassAndStatusRetainNativeFaultsWithoutQueueOrRecovery() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Runtime.src[Sort.MartialArtist] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Runtime.UpdateStatus(scope.Client));
        scope.Runtime.src.Clear();
        scope.State.Row(Sort.Apothecary, Sort.Apothecary);
        scope.Client.Status = null!;
        Assert.Throws<NullReferenceException>(() => scope.Runtime.UpdateStatus(scope.Client));
        scope.Unwritten();
    });
}
