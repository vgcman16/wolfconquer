using System.Reflection;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    internal static async Task RunOrdinaryRestartParentAsync(ITestOutputHelper output)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        SeedRetiredOrdinaryWork(input, uids, allocated: true, out var after);
        uids.Dispose();
        InstallRetiredAfterFacts(input, after); // Explicit historical already-after fixture, never a new admission grant.
        var files = CaptureAdmissionFiles(input);
        var uidState = uids.StateBytes();
        Assert.Single(JsonNode.Parse(uidState)!["ProducerIntents"]!.AsArray());
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        uids.Dispose(); // Child cannot borrow an in-process registry lease or owner cache.

        var result = LegacyNativeCombatGearLoadChildFixture.Run("CommittedOrdinaryIntent", output, false,
            "ExecuteOrdinaryAdmission", AdmissionRoot(input));

        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("SyntheticOnly").GetBoolean());
        AssertAdmissionFiles(files);
        Assert.Equal(uidState, uids.StateBytes());
        uids.Open(bootstrap: false); // Actual fresh producer reconciles exact four/postimages and archives.
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(new uint[] { 2000001, 2000002, 2000003, 2000004 }, admitted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            AssertAdmissionFiles(files);
            Assert.Empty(JsonNode.Parse(uids.StateBytes())!["ProducerIntents"]!.AsArray());
            Assert.Equal(1UL, JsonNode.Parse(uids.StateBytes())!["ArchiveCount"]!.GetValue<ulong>());
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    internal static void RequireOrdinaryRestartOwnedRoot(string root)
    {
        var absolute = Path.GetFullPath(root);
        var name = Path.GetFileName(absolute);
        const string prefix = "wolf-hairface-login-";
        Assert.True(Path.IsPathFullyQualified(root));
        Assert.Equal(absolute, root);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(absolute),
            ignoreCase: OperatingSystem.IsWindows());
        Assert.StartsWith(prefix, name, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(absolute);
    }

    internal static async Task RunDefaultOrdinaryRestartChildAsync(string root)
    {
        RequireOrdinaryRestartOwnedRoot(root);
        var cache = Assert.IsType<Dictionary<string, LegacyOrdinaryAdmissionUidOwner>>(
            typeof(LegacyOrdinaryAdmissionMutationFence).GetField("Owners", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
        Assert.Empty(cache); // Read-only observation; no global reset or owner substitution.
        var users = Path.Combine(root, "Users");
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(users));
        var files = roots.Kinds.ToDictionary(kind => roots.PathFor(kind, Uid), kind =>
            File.Exists(roots.PathFor(kind, Uid)) ? File.ReadAllBytes(roots.PathFor(kind, Uid)) : null);
        var uidPath = Path.Combine(roots.Items!, ".managed-item-uids", "state.json");
        var uidState = File.ReadAllBytes(uidPath);
        Assert.Single(JsonNode.Parse(uidState)!["ProducerIntents"]!.AsArray());
        using var metadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new NativePlayerDiagnosticLog();
        // Same public/default constructor shape used by the real Host. It has
        // no producer and must never infer one from files or the UID range.
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users), null, AdmissionCatalog(),
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var before = ObservePrefixPlayer(birth.Connection.NativeGameClient!.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(birth.Connection.NativeGameClient.Player));
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Null(birth.Connection.NativeGameClient.RawEquipmentObservation);
            Assert.Contains("ordinaryAdmissionReconciliationOwnerMissing", log.Text);
            Assert.DoesNotContain("ordinary admission committed key=", log.Text);
            AssertAdmissionFiles(files);
            Assert.Equal(uidState, File.ReadAllBytes(uidPath));
            Assert.Empty(cache);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }
}
