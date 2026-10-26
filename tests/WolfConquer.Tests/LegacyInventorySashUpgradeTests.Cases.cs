using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(150L, 0UL, 60L, 1UL)]
    [InlineData(90L, 299UL, 0L, 300UL)]
    public Task RegisteredSashUpgradeCommitsExactNinetyCpOneSlotAndThreeLegacyFrames(
        long cp, ulong count, long expectedCp, ulong expectedCount) => RunAsync(async fixture =>
        {
            fixture.Seed(cp, count);
            var before = fixture.Capture();
            var observed = 0;
            fixture.SendObserver = _ =>
            {
                Assert.Equal(expectedCp, fixture.Current.Character.ConquerPoints);
                Assert.Equal(expectedCount.ToString(CultureInfo.InvariantCulture), fixture.Current.Character.Attributes["InventorySashCount"]);
                Assert.Equal(ProjectSashFile(before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath)], expectedCp, expectedCount),
                    File.ReadAllBytes(fixture.CharacterPath));
                observed++;
            };

            await fixture.InvokeAsync();

            Assert.Equal(3, observed);
            AssertSashCommit(fixture, before, expectedCp, expectedCount);
        });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredSashSharingFailureKeepsExactOwnerFilesAndOutputsThenSameOwnerRetries() => RunAsync(async fixture =>
    {
        fixture.Seed();
        var before = fixture.Capture();
        fixture.HoldCharacterFile();

        await fixture.InvokeAsync();

        fixture.ReleaseFiles(); // Actual handler terminal first, never release on a timeout observation.
        fixture.AssertUnchanged(before);
        await fixture.InvokeAsync();
        AssertSashCommit(fixture, before, 60, 1);
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-uid")]
    [InlineData("stale-cp")]
    [InlineData("stale-count")]
    public Task RegisteredSashRequiredCharacterBaselineRefusesUnchangedAndOnlyFaultRepairAllowsRetry(string fault) => RunAsync(async fixture =>
    {
        fixture.Seed();
        var original = File.ReadAllBytes(fixture.CharacterPath);
        switch (fault)
        {
            case "missing": File.Delete(fixture.CharacterPath); break;
            case "foreign-uid": WriteSashField(fixture, "UID", "1293601"); break;
            case "stale-cp": WriteSashField(fixture, "ConquerPoints", "151"); break;
            case "stale-count": WriteSashField(fixture, "InventorySashCount", "7"); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        var before = fixture.Capture();

        await fixture.InvokeAsync();

        fixture.AssertUnchanged(before);
        File.WriteAllBytes(fixture.CharacterPath, original); // Repair only the exact injected leaf fault.
        var repaired = fixture.Capture();
        await fixture.InvokeAsync();
        AssertSashCommit(fixture, repaired, 60, 1);
    });

    [Fact]
    public Task RegisteredSashMalformedPresentCountCannotBeReinterpretedAsFreeZero() => RunAsync(async fixture =>
    {
        fixture.Seed();
        WriteSashField(fixture, "InventorySashCount", "not-a-count");
        var attributes = new Dictionary<string, string>(fixture.Current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["InventorySashCount"] = "not-a-count" };
        var malformed = fixture.Current with { Character = fixture.Current.Character with { Attributes = attributes } };
        fixture.Sessions.Set(fixture.Connection, malformed);
        fixture.SetContext(fixture.Context with { Session = malformed });
        var before = fixture.Capture();

        await fixture.InvokeAsync();

        fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData("store")]
    [InlineData("operations")]
    [InlineData("unready-recovery")]
    [InlineData("unregistered")]
    public Task RegisteredSashMissingGenuineOperationOrCurrentOwnerCannotSpend(string missing) => RunAsync(async fixture =>
    {
        fixture.Seed();
        var before = fixture.Capture();
        switch (missing)
        {
            case "store": fixture.SetContext(fixture.Context with { Sessions = null }); break;
            case "operations": fixture.SetContext(fixture.Context with { TradeOperations = null }); break;
            case "unready-recovery":
                fixture.SetContext(fixture.Context with { TradeRecovery = new LegacyTradeRecoveryState(fixture.Users, fixture.Items) });
                break;
            case "unregistered": Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _)); break;
            default: throw new ArgumentOutOfRangeException(nameof(missing));
        }

        await fixture.InvokeAsync();

        if (missing == "unregistered")
        {
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
            fixture.AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
        }
        else fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData("ended")]
    [InlineData("in-trade")]
    [InlineData("pending-claim")]
    public Task RegisteredSashEndedOrTradingOwnerCannotConsumeWallet(string state) => RunAsync(async fixture =>
    {
        fixture.Seed();
        if (state == "ended")
        {
            lock (fixture.Trades.SyncRoot) fixture.Trades.MarkConnectionEnded(fixture.Connection);
        }
        else if (state == "in-trade")
        {
            var current = fixture.Current with { LegacyTradeActive = true };
            fixture.Sessions.Set(fixture.Connection, current);
            fixture.SetContext(fixture.Context with { Session = current });
        }
        else
        {
            // Real isolated pending/orphan escrow after ready startup, not an
            // invented active counterparty or a restart-recovery simulation.
            var original = fixture.Current;
            var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["ConquerPoints"] = "125" };
            var pending = LegacyTradeEscrowClaims.WithClaim(original with
            { Character = original.Character with { ConquerPoints = 125, Attributes = attributes } },
                new LegacyTradeEscrowClaim(Guid.Parse("8db71775-80a5-495e-b5a0-aedb940408af"), 0, 25));
            Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, pending, out var reason), reason);
            fixture.Sessions.Set(fixture.Connection, pending);
            fixture.SetContext(fixture.Context with { Session = pending });
        }
        var before = fixture.Capture();

        await fixture.InvokeAsync();

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredSashStaleContextCannotOverwriteFreshCurrentWalletOrSlotCount() => RunAsync(async fixture =>
    {
        fixture.Seed();
        var originalContext = fixture.Context;
        var fresh = WithSashWallet(fixture.Current, 190, 4);
        File.WriteAllBytes(fixture.CharacterPath, ProjectSashFile(File.ReadAllBytes(fixture.CharacterPath), 190, 4));
        fixture.Sessions.Set(fixture.Connection, fresh);
        Assert.Same(originalContext.Session, fixture.Context.Session);
        Assert.NotSame(fixture.Context.Session, fixture.Current);
        var before = fixture.Capture();

        await fixture.InvokeAsync();

        AssertSashCommit(fixture, before, 100, 5);
    });

    [Theory]
    [InlineData(89L, 0UL)]
    [InlineData(150L, 300UL)]
    public Task RegisteredSashUnaffordableOrCappedRetainsNativeFailureTalkWithoutDebit(long cp, ulong count) => RunAsync(async fixture =>
    {
        fixture.Seed(cp, count);
        var before = fixture.Capture();

        await fixture.InvokeAsync();

        Assert.Same(before.Owner, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(before.Files);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("You need 90 CPs in your inventory to open 1 slot in your sash.").ToFrameBytes(),
            Assert.Single(fixture.Sent.Skip(before.FrameCount)));
    });

    [Fact]
    public Task RegisteredSashUpgradeQueuesBehindActualSharedOperationBeforeSelectingOwner() => RunAsync(async fixture =>
    {
        fixture.Seed();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Releases.Add(release);
        var reader = Task.Run(async () =>
        {
            using var lease = await fixture.Operations.EnterSharedAsync();
            ready.TrySetResult();
            await release.Task;
        });
        fixture.Calls.Add(reader);
        await ready.Task.WaitAsync(Bound);
        var before = fixture.Capture();
        var action = fixture.InvokeAsync();
        var gate = typeof(WorldOperationBarrier).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Operations)!;
        var queue = typeof(WorldOperationBarrier).GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Operations)!;
        var count = queue.GetType().GetProperty("Count")!;
        using var budget = new CancellationTokenSource(Bound);
        while (true)
        {
            lock (gate) { if ((int)count.GetValue(queue)! > 0) break; }
            if (action.IsCompleted) { await action; Assert.Fail("Upgrade bypassed the real preceding Shared operation."); }
            await Task.Delay(10, budget.Token);
        }
        fixture.AssertUnchanged(before);
        Assert.False(action.IsCompleted);
        release.TrySetResult();
        await fixture.ObserveAsync(reader);
        await fixture.ObserveAsync(action);
        AssertSashCommit(fixture, before, 60, 1);
    });

    private static void AssertSashCommit(Fixture fixture, Snapshot before, long cp, ulong count)
    {
        var expected = WithSashWallet(before.Owner, cp, count);
        Assert.Equal(expected.Character with { Attributes = fixture.Current.Character.Attributes }, fixture.Current.Character);
        Assert.Equal(expected.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            fixture.Current.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(before.Owner with { Character = fixture.Current.Character }, fixture.Current);
        Assert.Same(before.Owner.PlayerItems, fixture.Current.PlayerItems);
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var path = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        files[path] = ProjectSashFile(before.Files[path], cp, count);
        fixture.AssertFiles(files);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
        Assert.Equal(cp, saved.ConquerPoints);
        Assert.Equal(count.ToString(CultureInfo.InvariantCulture), saved.Attributes["InventorySashCount"]);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(3, frames.Length);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)cp))]).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySashMax, 300)]).ToFrameBytes(), frames[1]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySash, count)]).ToFrameBytes(), frames[2]);
    }

    private static WorldSession WithSashWallet(WorldSession before, long cp, ulong count)
    {
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["ConquerPoints"] = cp.ToString(CultureInfo.InvariantCulture), ["InventorySashCount"] = count.ToString(CultureInfo.InvariantCulture) };
        return before with { Character = before.Character with { ConquerPoints = cp, Attributes = attributes } };
    }

    private static byte[] ProjectSashFile(byte[] bytes, long cp, ulong count) => ProjectSashFields(bytes,
        new Dictionary<string, string> { ["ConquerPoints"] = cp.ToString(CultureInfo.InvariantCulture),
            ["InventorySashCount"] = count.ToString(CultureInfo.InvariantCulture) });

    private static byte[] ProjectSashFields(byte[] bytes, IReadOnlyDictionary<string, string> values)
    {
        var lines = Encoding.UTF8.GetString(bytes).Split('\n');
        for (var index = 0; index < lines.Length; index++)
            foreach (var (key, value) in values)
                if (lines[index].StartsWith(key + "=", StringComparison.Ordinal))
                    lines[index] = key + "=" + value + (lines[index].EndsWith('\r') ? "\r" : "");
        return new UTF8Encoding(false).GetBytes(string.Join('\n', lines));
    }
    private static void WriteSashField(Fixture fixture, string key, string value) => File.WriteAllBytes(fixture.CharacterPath,
        ProjectSashFields(File.ReadAllBytes(fixture.CharacterPath), new Dictionary<string, string> { [key] = value }));
}
