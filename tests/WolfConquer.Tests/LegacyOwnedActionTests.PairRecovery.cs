using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public Task GenuinePairedMovementRecoversBothCharacterImagesAtEveryReplacementPrefixWithoutTransientReplay(bool jump, int prefix) =>
        Run(async f =>
        {
            var capture = await CapturePair(f, jump, prefix);
            AssertPairPhysicalPrefix(capture, prefix);
            await RestartPairAndAssert(f, capture);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task GenuinePairedMovementRecoversTwoFlushedStagesWithoutPublishingTheInterruptedMovement(bool jump) => Run(async f =>
    {
        var capture = await CapturePair(f, jump, 2, flushed: true);
        foreach (string path in capture.Targets)
        {
            Assert.Equal(capture.BeforeTargets[path], File.ReadAllBytes(path));
            Assert.Equal(capture.AfterTargets[path], File.ReadAllBytes(PairStage(capture, path)));
        }
        await RestartPairAndAssert(f, capture);
    });

    private sealed record PairCapture(Dictionary<string, byte[]> Before, Dictionary<string, byte[]> After,
        Dictionary<string, byte[]> BeforeTargets, Dictionary<string, byte[]> AfterTargets, string PendingPath, string TransactionId)
    {
        internal string[] Targets => BeforeTargets.Keys.ToArray();
    }

    private static async Task<PairCapture> CapturePair(ActionFixture f, bool jump, int boundary, bool flushed = false)
    {
        await f.StanceAsync(250); await f.StanceAsync(190, Peer); await f.PairAsync(); ClearOutput(f);
        Assert.Equal(Peer, f.Current.InteractionPartnerUid); Assert.Equal(Uid, f.Get(Peer).InteractionPartnerUid);
        AssertCoherent(f, 250); AssertCoherent(f, 190, Peer);
        var before = f.Data.Files();
        var after = ExpectedCoordinates(f, before,
            (Uid, 430, 380, jump ? 432 : 431, jump ? 381 : 380),
            (Peer, 430, 380, jump ? 432 : 431, jump ? 381 : 380));
        Assert.Equal((long)Uid, f.Current.AccountId); Assert.Equal((long)Peer, f.Get(Peer).AccountId);
        var declaredTargets = new[] { (FileId: f.Current.AccountId, Path: f.CharacterPath(Uid)),
            (FileId: f.Get(Peer).AccountId, Path: f.CharacterPath(Peer)) }.OrderBy(row => row.FileId).ToArray();
        var targets = declaredTargets.Select(row => row.Path).ToArray();
        var beforeTargets = targets.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var afterTargets = targets.ToDictionary(path => path,
            path => after[Path.GetRelativePath(f.Data.Root, path)], StringComparer.OrdinalIgnoreCase);
        var sessions = new[] { f.Current, f.Get(Peer) };
        var natives = new[] { f.Native, f.ConnectionFor(Peer).NativeGameClient! };
        var bindings = natives.Select(native => native.OrdinarySpellBinding!).ToArray();
        var players = natives.Select(native => native.Player).ToArray();
        var images = bindings.Select(binding => binding.Items.Image).ToArray();
        var spells = natives.Select(native => native.MySpells!.ClientSpells).ToArray();
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.PairMovementJournalInterruption = fault;
            try { if (jump) await f.JumpAsync(); else await f.WalkAsync(); }
            finally { f.Handler.PairMovementJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            Assert.Equal(flushed ? new[] { 0, boundary } : Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent); Assert.Empty(f.PeerSent); Assert.Empty(f.Order);
        for (int index = 0; index < sessions.Length; index++)
        {
            Assert.False(f.Store.TryGet(f.ConnectionFor(index == 0 ? Uid : Peer), out _));
            Assert.True(bindings[index].IsRetired);
            Assert.Same(natives[index], sessions[index].NativeGameClient);
            Assert.Equal((430L, 380L), (sessions[index].Character.X, sessions[index].Character.Y));
            Assert.Equal(index == 0 ? 250U : 190U, LegacyActionState.RequireCurrent(sessions[index].Character));
            Assert.Same(players[index], natives[index].Player);
            Assert.Same(images[index], bindings[index].Items.Image);
            Assert.Same(spells[index], natives[index].MySpells!.ClientSpells);
            Assert.Equal(index == 0 ? 250U : 190U, (uint)players[index].Action);
            Assert.Equal(1, players[index].HitPoints);
            Assert.Equal((ushort)430, players[index].X); Assert.Equal((ushort)380, players[index].Y);
        }
        foreach (var row in before)
            if (!targets.Contains(Path.Combine(f.Data.Root, row.Key), StringComparer.OrdinalIgnoreCase))
                Assert.Equal(row.Value, File.ReadAllBytes(Path.Combine(f.Data.Root, row.Key)));
        string pending = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(21, record["Version"]!.GetValue<int>());
        var operation = record["PairMovement"]!.AsObject();
        Assert.Equal(new[] { "ActorAccountId", "ActorUid", "DirectionOrFacing", "DynamicId", "FromX", "FromY", "Kind",
            "MapId", "PartnerAccountId", "PartnerUid", "ToX", "ToY" }, operation.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal(jump ? 2 : 1, operation["Kind"]!.GetValue<int>());
        Assert.Equal((long)Uid, operation["ActorAccountId"]!.GetValue<long>());
        Assert.Equal(Uid, operation["ActorUid"]!.GetValue<uint>());
        Assert.Equal((long)Peer, operation["PartnerAccountId"]!.GetValue<long>());
        Assert.Equal(Peer, operation["PartnerUid"]!.GetValue<uint>());
        Assert.Equal(1002U, operation["MapId"]!.GetValue<uint>()); Assert.Equal(0U, operation["DynamicId"]!.GetValue<uint>());
        Assert.Equal((ushort)430, operation["FromX"]!.GetValue<ushort>());
        Assert.Equal((ushort)380, operation["FromY"]!.GetValue<ushort>());
        Assert.Equal(jump ? (ushort)432 : (ushort)431, operation["ToX"]!.GetValue<ushort>());
        Assert.Equal(jump ? (ushort)381 : (ushort)380, operation["ToY"]!.GetValue<ushort>());
        Assert.Equal((ushort)6, operation["DirectionOrFacing"]!.GetValue<ushort>());
        var changes = record["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!;
            Assert.Equal((int)LegacyTradeJournalFileKind.Character, change["Kind"]!.GetValue<int>());
            Assert.Equal(declaredTargets[index].FileId, change["FileId"]!.GetValue<long>());
            Assert.Equal(beforeTargets[targets[index]], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(afterTargets[targets[index]], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        return new(before, after, beforeTargets, afterTargets, pending, record["TransactionId"]!.GetValue<string>());
    }

    private static void AssertPairPhysicalPrefix(PairCapture capture, int prefix)
    {
        for (int index = 0; index < capture.Targets.Length; index++)
        {
            string path = capture.Targets[index];
            Assert.Equal(index < prefix ? capture.AfterTargets[path] : capture.BeforeTargets[path], File.ReadAllBytes(path));
        }
    }

    private static async Task RestartPairAndAssert(ActionFixture f, PairCapture capture)
    {
        await f.CloseAsync(); int outputs = f.Order.Count;
        var ledger = File.ReadAllBytes(f.Data.State);
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var first, out var reason), reason);
        await using (var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(first))
        {
            Assert.True(runtime.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            AssertPairRecovered(f, capture, ledger); Assert.Equal(outputs, f.Order.Count);
        }
        var once = f.Data.Files();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var second, out reason), reason);
        await using (var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(second))
        {
            Assert.True(runtime.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            AssertPairRecovered(f, capture, ledger); f.Data.AssertFiles(once); Assert.Equal(outputs, f.Order.Count);
        }
    }

    private static void AssertPairRecovered(ActionFixture f, PairCapture capture, byte[] ledger)
    {
        // BEFORE any new Frame/login: complete X/Y-only images also prove
        // transient stance values and pair links were never persisted/replayed.
        f.Data.AssertFiles(capture.After); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.All(capture.Targets, path => Assert.False(File.Exists(PairStage(capture, path))));
        Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
            out var observation, out var reason), reason);
        uint highwater = JsonNode.Parse(ledger)!["HighWater"]!.GetValue<uint>();
        Assert.True(observation.Maximum <= highwater);
    }

    private static string PairStage(PairCapture capture, string target) => target + ".managed-trade-" + capture.TransactionId + ".tmp";
}
