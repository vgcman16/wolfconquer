using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryAdmissionFenceDispatchTests
{
    [Fact]
    public void SharedDirectoryKeepsEachRealOwnersFileKindAndOriginalRegistrationOrder()
    {
        using var first = new OwnerFixture();
        using var second = new OwnerFixture(sharedGear: first.Roots.Proficiencies);
        var shared = first.Roots.Proficiencies!;
        Assert.Equal([(first.Owner, LegacyTradeJournalFileKind.Proficiencies),
            (second.Owner, LegacyTradeJournalFileKind.CombatGear)], Rows(Bucket(shared)));
        second.Registry.Dispose();
        var files = first.Data.Files();
        first.SaveSameProficiencies(); // The foreign .ini owner cannot turn a .bin into its participant.
        Assert.Throws<IOException>(() => new CombatGearSnapshotLoader(shared)
            .SaveByCharacterUid(Uid, CombatGearSnapshot.Empty));
        first.Data.AssertFiles(files);
        Assert.Equal((first.Owner, second.Owner), SessionOwners(second.Roots.Users, first.Roots.Users));
    }

    [Theory]
    [InlineData("0002399700.BIN", true)]
    [InlineData("2399700.bin", true)]
    [InlineData("0.bin", true)]
    [InlineData("4294967295.bin", true)]
    [InlineData("+2399700.bin", false)]
    [InlineData("-1.bin", false)]
    [InlineData("4294967296.bin", false)]
    [InlineData("2399700.ini", false)]
    [InlineData("2399700.bin.extra", false)]
    public void CaseAndLeafDispatchRetainsExistingUnsignedExtensionRules(string leaf, bool refused)
    {
        using var fixture = new OwnerFixture();
        fixture.Registry.Dispose();
        var files = fixture.Data.Files();
        var root = fixture.Roots.Proficiencies!.ToUpperInvariant();
        Assert.Same(Bucket(fixture.Roots.Proficiencies!), Bucket(root));
        Action action = () => LegacyOrdinaryAdmissionMutationFence.RequireFileMutation(Path.Combine(root, leaf));
        if (refused) Assert.Throws<IOException>(action); else action();
        fixture.Data.AssertFiles(files);
    }

    [Fact]
    public void SessionLookupChecksBothGenuineRootsAndDoesNotCacheEndedOwnerOrInvalidAccounts()
    {
        using var first = new OwnerFixture();
        using var second = new OwnerFixture();
        var previous = first.Session(); var next = second.Session();
        Assert.Equal((first.Owner, second.Owner), SessionOwners(second.Roots.Users, first.Roots.Users));
        Assert.True(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(previous, next, null));
        var files = second.Data.Files();
        first.Registry.Dispose();
        Assert.False(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(previous, next, null));
        Assert.False(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(next, previous, null));
        Assert.True(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(previous with { AccountId = 0 }, next, null));
        Assert.True(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(previous with { AccountId = (long)uint.MaxValue + 1 }, next, null));
        Assert.True(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(null, next, null));
        Assert.Equal((second.Owner, (LegacyOrdinaryAdmissionUidOwner?)null), SessionOwners(null, second.Roots.Users));
        second.Data.AssertFiles(files);
    }

    [Fact]
    public void GenuineRegistrationRefusesWithinOwnerAliasedKindsBeforeChangingTheIndex()
    {
        using var fixture = new OwnerFixture();
        var before = Bucket(fixture.Roots.Spells!);
        var roots = fixture.Roots with { Proficiencies = fixture.Roots.Spells };
        fixture.Registry.Dispose();
        Assert.True(LegacyItemUidRegistry.TryOpen(fixture.Data.RegistryOptions(), fixture.Data.ExpectedDomain,
            out var registry, out var reason), reason);
        using (registry)
        {
            Assert.False(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry!, fixture.Data.ExpectedDomain,
                roots, fixture.Data.Mailbox, out var rejected, out reason));
            Assert.Null(rejected);
            Assert.Same(before, Bucket(fixture.Roots.Spells!));
            Assert.Throws<IOException>(() => new PlayerSpellSnapshotLoader(fixture.Roots.Spells!)
                .SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty));
        }
    }
}
