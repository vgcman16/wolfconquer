using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeOwnedHitpointsTests
{
    private static Task Run(Func<Fixture, Task> exercise) =>
        WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(exercise, lineage: false);

    private static LegacyOrdinarySpellBinding Binding(Fixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.Same(f.Store, binding.Store); Assert.Same(f.Connection, binding.Connection);
        Assert.Same(f.Native, binding.Client); Assert.Same(f.Player, binding.Player);
        return binding;
    }

    private static long SavedHp(Fixture f) =>
        Assert.IsType<CharacterSnapshot>(new CharacterSnapshotLoader(f.Users)
            .TryLoadByAccountIdForAdmission(Fixture.Uid).Snapshot).MinHitPoints;

    private static void AssertHp(Fixture f, int expected)
    {
        Assert.Equal((long)expected, f.Current.Character.MinHitPoints);
        Assert.Equal(expected, f.Player.HitPoints);
        Assert.Equal(expected > 0, f.Player.Alive);
        Assert.Equal(expected < 1, f.Player.NotLife);
        Assert.Equal((long)expected, SavedHp(f));
        Assert.False(Binding(f).IsRetired);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(Binding(f)));
    }

    // Prepare inputs with the same actual pure scalar projector as the real
    // writer. It grants no authority or save result; the actual v8 journal below
    // still owns replacement. Literal HP/owner assertions remain independent.
    private static byte[] HpImage(Fixture f, byte[] source, long hp) =>
        LegacyBoundHitpointsScalarWrite.ProjectAfterImage(source,
            f.Current.Character with { MinHitPoints = hp });

    private static LegacyTradeJournalChange[] HpChanges(Fixture f, byte[] before, byte[] after)
    {
        var spells = File.ReadAllBytes(f.SpellPath);
        return [new(LegacyTradeJournalFileKind.Character, f.Current.AccountId, before, after),
            new(LegacyTradeJournalFileKind.Spells, f.Current.Character.Uid, spells, spells)];
    }
}
