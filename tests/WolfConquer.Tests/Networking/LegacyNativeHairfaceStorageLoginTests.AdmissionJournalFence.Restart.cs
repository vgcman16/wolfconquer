using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    internal static async Task RunUidFreePendingJournalRestartParentAsync(ITestOutputHelper output, bool frameConstructor)
    {
        using var input = new LoginInput();
        PrepareUidFreeJournalInput(input);
        await StageUidFreePendingJournal(input);
        var pending = File.ReadAllBytes(UidFreePendingPath(input));
        var expected = UidFreeExpectedRecovery(input, pending);
        AssertNoUidNamespace(input);
        // Existing measured child fixture starts a real fresh process and passes
        // only this guarded owned root; it cannot borrow the parent's route cache.
        var scenario = frameConstructor ? "UidFreePendingFrame" : "UidFreePendingRecovery";
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, false,
            "ExecuteOrdinaryAdmission", AdmissionRoot(input));
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("SyntheticOnly").GetBoolean());
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        AssertAdmissionFiles(expected);
        AssertNoUidNamespace(input);
        await CompleteUidFreeRecoveryAndRetry(input, expected);
    }

    internal static async Task RunUidFreePendingJournalChildAsync(string root, bool frameConstructor)
    {
        RequireOrdinaryRestartOwnedRoot(root);
        var catalog = Assert.IsType<Dictionary<string, HashSet<string>>>(typeof(LegacyOrdinaryAdmissionMutationFence)
            .GetField("UsersByParticipantDirectory", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        var producers = Assert.IsType<Dictionary<string, LegacyOrdinaryAdmissionUidOwner>>(typeof(LegacyOrdinaryAdmissionMutationFence)
            .GetField("Owners", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        Assert.Empty(catalog); Assert.Empty(producers); // Observation only, never a cache reset.
        var users = Path.Combine(root, "Users");
        var loader = new CharacterSnapshotLoader(users);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
        var files = roots.Kinds.ToDictionary(kind => roots.PathFor(kind, Uid), kind =>
            File.Exists(roots.PathFor(kind, Uid)) ? File.ReadAllBytes(roots.PathFor(kind, Uid)) : null);
        var pendingPath = Path.Combine(users, ".managed-trade-journal", "pending.json");
        var pending = File.ReadAllBytes(pendingPath);
        var expected = new Dictionary<string, byte[]?>(files);
        var record = System.Text.Json.Nodes.JsonNode.Parse(pending)!;
        Assert.Equal(7, record["Version"]!.GetValue<int>());
        foreach (var row in record["Changes"]!.AsArray())
            expected[roots.PathFor((LegacyTradeJournalFileKind)row!["Kind"]!.GetValue<int>(), row["FileId"]!.GetValue<long>())] =
                Convert.FromBase64String(row["After"]!.GetValue<string>());
        Assert.False(Directory.Exists(Path.Combine(roots.Items!, ".managed-item-uids")));
        using var metadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new NativePlayerDiagnosticLog();
        WorldServerFrameHandler? handler = null;
        LegacyTradeRecoveryState recovery;
        if (frameConstructor)
        {
            handler = new WorldServerFrameHandler(loader, null, AdmissionCatalog(),
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
        }
        else recovery = new(roots.Users, roots.Items, roots.Quests, Path.Combine(root, "PlayersMailList"),
            roots.CombatGear, roots.Spells, roots.Proficiencies);

        Assert.True(catalog.TryGetValue(roots.Spells!, out var domains) && domains.Contains(roots.Users));
        Assert.Empty(producers);
        Assert.True(LegacyTradeJournal.HasPending(users));
        Assert.Throws<IOException>(() => WriteDifferentUidFreeSnapshot("Spells", roots));
        AssertAdmissionFiles(files);
        Assert.Equal(pending, File.ReadAllBytes(pendingPath));

        Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        Assert.False(LegacyTradeJournal.HasPending(users));
        AssertAdmissionFiles(expected);
        var spells = new PlayerSpellSnapshotLoader(roots.Spells!);
        spells.SaveByCharacterUid(Uid, spells.LoadByCharacterUid(Uid)); // No pending: actual writer succeeds.
        AssertAdmissionFiles(expected);
        handler ??= new WorldServerFrameHandler(loader, null, AdmissionCatalog(),
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(new uint[] { 100, 101, 102, 103 }, accepted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(new ushort[] { 1110 }, accepted.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal(1000L, accepted.Character.Money);
            Assert.Empty(producers);
            Assert.False(Directory.Exists(Path.Combine(roots.Items!, ".managed-item-uids")));
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }
}
