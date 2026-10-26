using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("Character")]
    [InlineData("Items")]
    [InlineData("Spells")]
    [InlineData("Proficiencies")]
    [InlineData("Quests")]
    [InlineData("CombatGear")]
    [InlineData("Store")]
    public async Task UidFreePendingJournalFencesActualWritersAndAllowsFreshRecoveryAndRetry(string role)
    {
        using var input = new LoginInput();
        PrepareUidFreeJournalInput(input);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        await StageUidFreePendingJournal(input, (handler, connection, plan) =>
        {
            var files = CaptureAdmissionFiles(input);
            var pending = File.ReadAllBytes(UidFreePendingPath(input));

            RefuseUidFreeWriter(role, input, roots, handler, connection, plan);

            AssertAdmissionFiles(files);
            Assert.Equal(pending, File.ReadAllBytes(UidFreePendingPath(input)));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            return Task.CompletedTask;
        });
        var retained = File.ReadAllBytes(UidFreePendingPath(input));
        var expected = UidFreeExpectedRecovery(input, retained);
        await CompleteUidFreeRecoveryAndRetry(input, expected);
        // Real ordinary snapshot APIs become usable again after actual recovery.
        if (role is not ("Character" or "Store")) WriteDifferentUidFreeSnapshot(role, roots);
        else Assert.True(CharacterSnapshotScalarPersistence.TryPersist(input.Users,
            new CharacterSnapshotLoader(input.Users).LoadByAccountId(Uid)!, out _, out var reason), reason);
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        AssertNoUidNamespace(input);
    }

    [Fact]
    public async Task UnrelatedConfiguredDomainRemainsWritableWhileAnotherDomainRetainsUidFreeJournal()
    {
        using var blocked = new LoginInput();
        PrepareUidFreeJournalInput(blocked);
        await StageUidFreePendingJournal(blocked);
        var files = CaptureAdmissionFiles(blocked);
        var pending = File.ReadAllBytes(UidFreePendingPath(blocked));
        using var unrelated = new LoginInput();
        PrepareUidFreeJournalInput(unrelated);
        var otherRoots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(unrelated.Users));
        _ = FreshAdmissionRecovery(unrelated); // Actual explicit constructor binds this separate domain.

        WriteDifferentUidFreeSnapshot("Spells", otherRoots);

        Assert.Single(new PlayerSpellSnapshotLoader(otherRoots.Spells!).LoadByCharacterUid(Uid).Spells);
        AssertAdmissionFiles(files);
        Assert.Equal(pending, File.ReadAllBytes(UidFreePendingPath(blocked)));
        Assert.False(LegacyTradeJournal.HasPending(unrelated.Users));
        await CompleteUidFreeRecoveryAndRetry(blocked, UidFreeExpectedRecovery(blocked, pending));
    }

    [Fact]
    public async Task ReconfiguredRoutesCannotEraseTheOldPendingDomainFence()
    {
        using var input = new LoginInput();
        PrepareUidFreeJournalInput(input);
        await StageUidFreePendingJournal(input);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        var alternate = Path.Combine(AdmissionRoot(input), "AlternateSpells");
        _ = new LegacyTradeRecoveryState(roots.Users, roots.Items, roots.Quests,
            Path.Combine(AdmissionRoot(input), "PlayersMailList"), roots.CombatGear, alternate, roots.Proficiencies);
        var files = CaptureAdmissionFiles(input);
        var pending = File.ReadAllBytes(UidFreePendingPath(input));

        Assert.Throws<IOException>(() => WriteDifferentUidFreeSnapshot("Spells", roots));
        Assert.Throws<IOException>(() => new PlayerSpellSnapshotLoader(alternate).SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty));

        Assert.False(Directory.Exists(alternate));
        AssertAdmissionFiles(files);
        Assert.Equal(pending, File.ReadAllBytes(UidFreePendingPath(input)));
        await CompleteUidFreeRecoveryAndRetry(input, UidFreeExpectedRecovery(input, pending));
    }

    [Fact]
    public async Task MalformedRetainedJournalOnlyDeniesWritesAndNeverBecomesMutationPermission()
    {
        using var input = new LoginInput();
        PrepareUidFreeJournalInput(input);
        await StageUidFreePendingJournal(input);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        var path = UidFreePendingPath(input);
        var legitimate = File.ReadAllBytes(path);
        byte[] malformed = [0x7B, 0x7D];
        File.WriteAllBytes(path, malformed); // Deliberately malformed owned evidence; not an accepted record.
        var files = CaptureAdmissionFiles(input);

        Assert.Throws<IOException>(() => WriteDifferentUidFreeSnapshot("Spells", roots));
        Assert.False(FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
        AssertAdmissionFiles(files);
        Assert.Equal(malformed, File.ReadAllBytes(path));

        File.WriteAllBytes(path, legitimate);
        await CompleteUidFreeRecoveryAndRetry(input, UidFreeExpectedRecovery(input, legitimate));
    }
}
