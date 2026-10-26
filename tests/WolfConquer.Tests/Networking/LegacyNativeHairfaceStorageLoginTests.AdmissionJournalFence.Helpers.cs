using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static void PrepareUidFreeJournalInput(LoginInput input)
    {
        PrepareAdmissionInput(input);
        File.WriteAllText(input.FilePath, File.ReadAllText(input.FilePath)
            .Replace("Class=7051\r\n", "Class=1051\r\n", StringComparison.Ordinal));
        Assert.Equal(1051L, new CharacterSnapshotLoader(input.Users).LoadByAccountId(Uid)!.Class);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        // Complete enhanced saved equipment is pre-existing fixture data, not a
        // grant. It is deliberately outside the neutral starter-repair pattern.
        var equipment = new[] { (Id: 132009u, Pos: (ushort)3), (Id: 420301u, Pos: (ushort)4),
            (Id: 420301u, Pos: (ushort)5), (Id: 150003u, Pos: (ushort)6) }
            .Select((row, index) => new PlayerItemSnapshotItem { Uid = 100u + (uint)index,
                ItemId = row.Id, Position = row.Pos, Durability = 321, MaximumDurability = 321,
                StackSize = 1, Plus = 1, Color = 5 }).ToArray();
        new PlayerItemSnapshotLoader(roots.Items!).SaveByCharacterUid(Uid, new([], [], [], equipment, []));
        new PlayerSpellSnapshotLoader(roots.Spells!).SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(roots.Proficiencies!).SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(roots.Quests!).SaveByCharacterUid(Uid, PlayerQuestSnapshot.Empty);
        new CombatGearSnapshotLoader(roots.CombatGear!).SaveByCharacterUid(Uid, CombatGearSnapshot.Empty);
    }

    private static async Task StageUidFreePendingJournal(LoginInput input,
        Func<WorldServerFrameHandler, ConquerTcpConnection, LegacyOrdinaryAdmissionPlan, Task>? whilePending = null)
    {
        var before = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            using var operation = await AdmissionOwner<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync(fixture.Token);
            var plan = PrepareRealAdmission(handler, birth.Connection, input, owner: null, recovery: recovery);
            Assert.False(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCommitOrdinaryAdmission(birth.Connection,
                plan, out var accepted, out reason, LegacyOrdinaryAdmissionInterruption.AfterPending));
            Assert.Null(accepted);
            Assert.True(recovery.IsBlocked);
            Assert.True(LegacyTradeJournal.HasPending(input.Users));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.NotNull(plan.Candidate);
            Assert.Equal(new uint[] { 100, 101, 102, 103 }, plan.Candidate!.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(new ushort[] { 1110 }, plan.Candidate.PlayerSpells.Spells.Select(row => row.Id));
            AssertAdmissionFiles(before);
            AssertNoUidNamespace(input);
            if (whilePending is not null) await whilePending(handler, birth.Connection, plan);
        }, worldData: AdmissionCatalog());
    }

    private static void AssertNoUidNamespace(LoginInput input) =>
        Assert.False(Directory.Exists(Path.Combine(AdmissionRoot(input), "PlayersItems", ".managed-item-uids")));

    private static string UidFreePendingPath(LoginInput input) => Path.Combine(input.Users, ".managed-trade-journal", "pending.json");

    private static void RefuseUidFreeWriter(string role, LoginInput input, LegacyOrdinaryAdmissionRoots roots,
        WorldServerFrameHandler handler, ConquerTcpConnection connection, LegacyOrdinaryAdmissionPlan plan)
    {
        if (role == "Character")
            Assert.False(CharacterSnapshotScalarPersistence.TryPersist(input.Users,
                plan.Baseline.Character with { Money = 999 }, out _, out _));
        else if (role == "Store")
            Assert.Throws<InvalidOperationException>(() => AdmissionOwner<WorldSessionStore>(handler, "_sessions")
                .Set(connection, plan.Baseline));
        else Assert.Throws<IOException>(() => WriteDifferentUidFreeSnapshot(role, roots));
    }

    private static void WriteDifferentUidFreeSnapshot(string role, LegacyOrdinaryAdmissionRoots roots)
    {
        // Synthetic structurally valid persistence values; these calls do not
        // claim a gameplay learn/reward entitlement or publish native state.
        switch (role)
        {
            case "Items": new PlayerItemSnapshotLoader(roots.Items!).SaveByCharacterUid(Uid, PlayerItemSnapshot.Empty); break;
            case "Spells": new PlayerSpellSnapshotLoader(roots.Spells!).SaveByCharacterUid(Uid,
                new([new(65000, 1, 23, 0, 0, 0)])); break;
            case "Proficiencies": new PlayerProficiencySnapshotLoader(roots.Proficiencies!).SaveByCharacterUid(Uid,
                new([new(420, 1, 0, 23, 100)])); break;
            case "Quests": new PlayerQuestSnapshotLoader(roots.Quests!).SaveByCharacterUid(Uid,
                new([new(900001, PlayerQuestStatus.Accepted, 1, [2u])])); break;
            case "CombatGear": new CombatGearSnapshotLoader(roots.CombatGear!).SaveByCharacterUid(Uid,
                CombatGearSnapshot.Empty with { PirateExp = 1 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(role));
        }
    }

    private static Dictionary<string, byte[]?> UidFreeExpectedRecovery(LoginInput input, byte[] pending)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        var expected = CaptureAdmissionFiles(input);
        var record = JsonNode.Parse(pending)!;
        Assert.Equal(7, record["Version"]!.GetValue<int>());
        foreach (var row in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)Uid, row!["FileId"]!.GetValue<long>());
            var kind = (LegacyTradeJournalFileKind)row["Kind"]!.GetValue<int>();
            expected[roots.PathFor(kind, Uid)] = Convert.FromBase64String(row["After"]!.GetValue<string>());
        }
        return expected;
    }

    private static async Task CompleteUidFreeRecoveryAndRetry(LoginInput input, Dictionary<string, byte[]?> expected)
    {
        Assert.True(FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason), reason);
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        AssertAdmissionFiles(expected);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(new uint[] { 100, 101, 102, 103 }, accepted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(new ushort[] { 1110 }, accepted.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal(1000L, accepted.Character.Money);
            Assert.True(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCompareAndSet(birth.Connection, accepted, accepted));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            AssertNoUidNamespace(input);
        }, worldData: AdmissionCatalog());
    }
}
