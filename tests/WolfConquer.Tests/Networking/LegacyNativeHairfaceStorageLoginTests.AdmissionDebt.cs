using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ActualRetainedUnsafeBeforeWorkDeniesReplayAndOrdinaryWritersWithoutDiscardingEvidence()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        var original = CaptureAdmissionFiles(input);
        var retained = SeedRetiredOrdinaryWork(input, uids, allocated: true, out _);
        var state = uids.StateBytes();
        uids.Dispose();
        uids.Open(bootstrap: false);
        Assert.False(uids.Owner.IsReconciliationComplete);
        Assert.True(uids.Owner.IsPending(Uid));
        Assert.False(uids.Owner.IsPending(Uid + 1));
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var before = ObservePrefixPlayer(birth.Connection.NativeGameClient!.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(birth.Connection.NativeGameClient.Player));
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Contains("unsafeRetainedWorkRequiresReconciliation", log.Text);
            Assert.False(AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery").IsBlocked);
            var raw = new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!;
            Assert.False(CharacterSnapshotScalarPersistence.TryPersist(input.Users, raw with { Money = 999 }, out _, out _));
            Assert.Throws<IOException>(() => new PlayerSpellSnapshotLoader(Path.Combine(AdmissionRoot(input), "PlayersSpells"))
                .SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty));
            Assert.Throws<IOException>(() => new PlayerProficiencySnapshotLoader(Path.Combine(AdmissionRoot(input), "PlayersProfs"))
                .SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty));
            var loader = new CharacterSnapshotLoader(input.Users);
            // Complete actual saved candidate for a NEGATIVE Store call. It is
            // never seeded/accepted as a live owner in place of the real login.
            var session = new WorldSession(Uid, "owned", raw, loader.LoadPlayerItemsByCharacterUid(Uid),
                loader.LoadPlayerSpellsByCharacterUid(Uid), loader.LoadPlayerProficienciesByCharacterUid(Uid),
                CombatGear: loader.LoadCombatGearByCharacterUid(Uid), UsersRoot: input.Users)
                { NativeGameClient = birth.Connection.NativeGameClient };
            Assert.Throws<InvalidOperationException>(() => AdmissionOwner<WorldSessionStore>(handler, "_sessions").Set(birth.Connection, session));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        }, admissionUids: uids.Owner);
        AssertAdmissionFiles(original);
        Assert.Equal(state, uids.StateBytes());
        Assert.True(uids.Registry.TryReadProducerIntent(uids.Domain, retained.Request.OperationId, out var same, out var reason), reason);
        Assert.Equal(retained.CopyPayload(), same!.CopyPayload());
        Assert.Equal(retained.Allocation, same.Allocation);
    }

    [Theory]
    [InlineData("Character")]
    [InlineData("Quests")]
    [InlineData("Proficiencies")]
    public async Task FreshOwnerCannotTurnMixedOrMissingOldWorkIntoWriterPermission(string role)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        File.WriteAllBytes(roots.PathFor(LegacyTradeJournalFileKind.Proficiencies, Uid), new byte[4]);
        using var uids = new AdmissionUidFixture(input);
        SeedRetiredOrdinaryWork(input, uids, allocated: true, out _);
        uids.Dispose();
        if (role == "Character") File.AppendAllText(input.FilePath, ";foreign pinned input\r\n", Encoding.UTF8);
        else if (role == "Quests") File.WriteAllBytes(roots.PathFor(LegacyTradeJournalFileKind.Quests, Uid), new byte[4]);
        else File.Delete(roots.PathFor(LegacyTradeJournalFileKind.Proficiencies, Uid));
        var files = CaptureAdmissionFiles(input);
        var state = uids.StateBytes();
        Assert.True(LegacyItemUidRegistry.TryOpen(uids.OptionsForReopen, out var registry, out var reason), reason);
        using (registry)
        {
            Assert.False(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry!, uids.Domain, roots,
                Path.Combine(AdmissionRoot(input), "PlayersMailList"), out var owner, out reason));
            Assert.Null(owner);
            Assert.Contains("ordinaryAdmissionRecoveryRequired", reason);
            Assert.Throws<IOException>(() => new PlayerSpellSnapshotLoader(roots.Spells!).SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty));
            AssertAdmissionFiles(files);
            Assert.Equal(state, uids.StateBytes());
        }
    }
}
