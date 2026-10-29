using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Independent managed handler owners share disposable saves. This models a restart
// boundary, not an actual killed process, hardware power loss, SQL, or D13 UI proof.
public sealed partial class LegacyTradeRestartRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-restart-" + Guid.NewGuid().ToString("N"));
    private readonly List<Player> players = [];

    [Fact]
    public async Task CapturedTradeLogKeepsConcurrentLineWritesAndSnapshotsConsistent()
    {
        using var log = new CapturedTradeLog();
        const int count = 512;
        string padding = new('x', 128);
        var synchronous = Task.Run(() =>
        {
            for (int i = 0; i < count; i++) log.WriteLine($"sync-{i}:{padding}");
        });
        var asynchronous = Task.Run(async () =>
        {
            for (int i = 0; i < count; i++) await log.WriteLineAsync($"async-{i}:{padding}");
        });
        var writing = Task.WhenAll(synchronous, asynchronous);
        do
        {
            string snapshot = log.ToString();
            Assert.True(snapshot.Length == 0 || snapshot.EndsWith(Environment.NewLine, StringComparison.Ordinal));
            await Task.Yield();
        }
        while (!writing.IsCompleted);
        await writing;
        string[] lines = log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(count * 2, lines.Length);
        Assert.Equal(count * 2, lines.Distinct(StringComparer.Ordinal).Count());
        for (int i = 0; i < count; i++)
        {
            Assert.Contains($"sync-{i}:{padding}", lines);
            Assert.Contains($"async-{i}:{padding}", lines);
        }
    }

    public LegacyTradeRestartRecoveryTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
    }

    [Fact]
    public async Task NewOwnerRefundsBothUncompletedClaimsBeforeFirstAdmissionExactlyOnceWithoutMovingItems()
    {
        const uint leftUid = 1_296_001;
        const uint rightUid = 1_296_002;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var leftItems = File.ReadAllBytes(ItemPath(leftUid));
        var rightItems = File.ReadAllBytes(ItemPath(rightUid));
        var first = NewOwner();
        var left = await Login(first, leftUid);
        var right = await Login(first, rightUid);
        await Open(first, left, right);
        await Trade(first, left, new(6) { ItemUID = 10 });
        await Trade(first, right, new(6) { ItemUID = 20 });
        await Trade(first, left, new(7) { Money = 300 });
        await Trade(first, left, new(12) { CPS = 5 });
        await Trade(first, right, new(7) { Money = 700 });
        await Trade(first, right, new(12) { CPS = 10 });
        AssertWallet(leftUid, 700, 45);
        AssertWallet(rightUid, 1_300, 50);
        var leftClaim = AssertClaim(leftUid, 300, 5);
        Assert.Equal(leftClaim, AssertClaim(rightUid, 700, 10));

        // The previous owner is abandoned without Close or cancellation. The new
        // owner must recover every persisted claim, not just the logging-in UID.
        var second = NewOwner();
        var rightAfterRestart = await Login(second, rightUid);
        AssertWallet(leftUid, 1_000, 50);
        AssertWallet(rightUid, 2_000, 60);
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        Assert.Equal(2_000, Current(second, rightAfterRestart).Character.Money);
        Assert.False(Current(second, rightAfterRestart).LegacyTradeActive);
        var leftAfterRestart = await Login(second, leftUid);
        Assert.Equal(1_000, Current(second, leftAfterRestart).Character.Money);
        Assert.Equal(leftItems, File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(rightItems, File.ReadAllBytes(ItemPath(rightUid)));

        var third = NewOwner();
        await Login(third, leftUid);
        await Login(third, rightUid);
        AssertWallet(leftUid, 1_000, 50);
        AssertWallet(rightUid, 2_000, 60);
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        Assert.Equal(leftItems, File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(rightItems, File.ReadAllBytes(ItemPath(rightUid)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedExchangeOrNormalRefundClearsClaimsAndLaterOwnersCannotCreditAgain(bool complete)
    {
        const uint leftUid = 1_296_011;
        const uint rightUid = 1_296_012;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var first = NewOwner();
        var left = await Login(first, leftUid);
        var right = await Login(first, rightUid);
        await Open(first, left, right);
        await Trade(first, left, new(6) { ItemUID = 10 });
        await Trade(first, right, new(6) { ItemUID = 20 });
        await Trade(first, left, new(7) { Money = 300 });
        await Trade(first, right, new(12) { CPS = 10 });
        AssertClaim(leftUid, 300, 0);
        AssertClaim(rightUid, 0, 10);
        if (complete)
        {
            await Trade(first, left, new(9));
            await Trade(first, right, new(9));
        }
        else await Trade(first, left, new(2));

        var expectedLeft = complete ? (700L, 60L) : (1_000L, 50L);
        var expectedRight = complete ? (2_300L, 50L) : (2_000L, 60L);
        AssertWallet(leftUid, expectedLeft.Item1, expectedLeft.Item2);
        AssertWallet(rightUid, expectedRight.Item1, expectedRight.Item2);
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        var leftItemBytes = File.ReadAllBytes(ItemPath(leftUid));
        var rightItemBytes = File.ReadAllBytes(ItemPath(rightUid));

        for (var restart = 0; restart < 2; restart++)
        {
            var next = NewOwner();
            var nextLeft = await Login(next, leftUid);
            var nextRight = await Login(next, rightUid);
            AssertWallet(leftUid, expectedLeft.Item1, expectedLeft.Item2);
            AssertWallet(rightUid, expectedRight.Item1, expectedRight.Item2);
            Assert.Equal(complete ? 20U : 10U, Assert.Single(Current(next, nextLeft).PlayerItems.InventoryItems).Uid);
            Assert.Equal(complete ? 10U : 20U, Assert.Single(Current(next, nextRight).PlayerItems.InventoryItems).Uid);
            AssertClearedClaim(leftUid);
            AssertClearedClaim(rightUid);
            Assert.Equal(leftItemBytes, File.ReadAllBytes(ItemPath(leftUid)));
            Assert.Equal(rightItemBytes, File.ReadAllBytes(ItemPath(rightUid)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task StartupFinishesInterruptedCompletedExchangeBeforeConsideringAnyOrphanRefund(int replacements)
    {
        const uint leftUid = 1_296_051;
        const uint rightUid = 1_296_052;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var first = NewOwner();
        var left = await Login(first, leftUid);
        var right = await Login(first, rightUid);
        await Open(first, left, right);
        await Trade(first, left, new(6) { ItemUID = 10 });
        await Trade(first, right, new(6) { ItemUID = 20 });
        await Trade(first, left, new(7) { Money = 300 });
        await Trade(first, right, new(12) { CPS = 10 });
        var before = SaveBytes();
        await Trade(first, left, new(9));
        await Trade(first, right, new(9));
        AssertWallet(leftUid, 700, 60);
        AssertWallet(rightUid, 2_300, 50);
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        var after = SaveBytes();

        // Replay the actual before/after bytes produced by the real completion
        // path, then stop its durable journal at each replacement boundary.
        foreach (var (path, bytes) in before) File.WriteAllBytes(path, bytes);
        var changes = new[] { leftUid, rightUid }.SelectMany(uid => new[]
        {
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, uid,
                before[UserPath(uid)], after[UserPath(uid)]),
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Items, uid,
                before[ItemPath(uid)], after[ItemPath(uid)]),
        }).ToArray();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes,
            LegacyJournalInterruption.StopAfter(replacements), out _));
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));

        var restarted = NewOwner();
        var nextLeft = await Login(restarted, leftUid);
        var nextRight = await Login(restarted, rightUid);

        AssertWallet(leftUid, 700, 60);
        AssertWallet(rightUid, 2_300, 50);
        Assert.Equal(20U, Assert.Single(Current(restarted, nextLeft).PlayerItems.InventoryItems).Uid);
        Assert.Equal(10U, Assert.Single(Current(restarted, nextRight).PlayerItems.InventoryItems).Uid);
        Assert.Equal(after[ItemPath(leftUid)], File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(after[ItemPath(rightUid)], File.ReadAllBytes(ItemPath(rightUid)));
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        var again = NewOwner();
        await Login(again, rightUid);
        await Login(again, leftUid);
        AssertWallet(leftUid, 700, 60);
        AssertWallet(rightUid, 2_300, 50);
        Assert.Equal(after[ItemPath(leftUid)], File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(after[ItemPath(rightUid)], File.ReadAllBytes(ItemPath(rightUid)));
    }

    [Fact]
    public async Task FailedFirstWalletWriteCannotDebitOrLeaveARecoverableClaim()
    {
        const uint leftUid = 1_296_021;
        const uint rightUid = 1_296_022;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        await Open(owner, left, right);
        var original = File.ReadAllBytes(UserPath(leftUid));
        using (File.Open(UserPath(leftUid), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Trade(owner, left, new(7) { Money = 300 });
            Assert.Equal(1_000, Current(owner, left).Character.Money);
        }
        Assert.Equal(original, File.ReadAllBytes(UserPath(leftUid)));
        AssertClearedClaim(leftUid);
        var restarted = NewOwner();
        await Login(restarted, leftUid);
        AssertWallet(leftUid, 1_000, 50);
    }

    [Theory]
    [InlineData(7U)]
    [InlineData(12U)]
    public async Task ZeroCurrencyOfferWritesNoClaimOrWalletAndRequiresEarlierConfirmationAgain(uint action)
    {
        const uint leftUid = 1_296_061;
        const uint rightUid = 1_296_062;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        await Open(owner, left, right);
        await Trade(owner, left, new(6) { ItemUID = 10 });
        await Trade(owner, left, new(9));
        var beforeZero = SaveBytes();
        var beforeLeft = Current(owner, left);
        var beforeRight = Current(owner, right);
        var leftFrameCount = left.Sent.Count;

        await Trade(owner, right, new(action));

        AssertSavesUnchanged(beforeZero);
        Assert.Same(beforeLeft, Current(owner, left));
        Assert.Same(beforeRight, Current(owner, right));
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Contains(left.Sent.Skip(leftFrameCount), bytes =>
            LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Action == (action == 7 ? 8UL : 11UL) && packet.Money == 0 && packet.CPS == 0);

        // Right's confirmation must not consume the earlier left confirmation:
        // even an accepted zero offer resets the pair's confirmation state.
        await Trade(owner, right, new(9));
        Assert.True(Current(owner, left).LegacyTradeActive);
        Assert.True(Current(owner, right).LegacyTradeActive);
        Assert.Equal(10U, Assert.Single(Current(owner, left).PlayerItems.InventoryItems).Uid);
        Assert.Equal(20U, Assert.Single(Current(owner, right).PlayerItems.InventoryItems).Uid);
        AssertSavesUnchanged(beforeZero);

        await Trade(owner, left, new(9));
        Assert.False(Current(owner, left).LegacyTradeActive);
        Assert.False(Current(owner, right).LegacyTradeActive);
        Assert.Empty(Current(owner, left).PlayerItems.InventoryItems);
        Assert.Equal(new uint[] { 10, 20 }, Current(owner, right).PlayerItems.InventoryItems.Select(item => item.Uid).Order().ToArray());
        AssertWallet(leftUid, 1_000, 50);
        AssertWallet(rightUid, 2_000, 60);
        AssertClearedClaim(leftUid);
        AssertClearedClaim(rightUid);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PendingJournalDuringLiveSettlementFencesOldOwnerUntilAValidFreshRecovery(
        bool confirm, bool validJournal)
    {
        const uint leftUid = 1_296_071;
        const uint rightUid = 1_296_072;
        Seed(leftUid, 1_000, 50, 10);
        Seed(rightUid, 2_000, 60, 20);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        var locationBefore = (Current(owner, left).Character.X, Current(owner, left).Character.Y);
        await Move(owner, left);
        Assert.NotEqual(locationBefore, (Current(owner, left).Character.X, Current(owner, left).Character.Y));
        Assert.Contains(left.Sent, bytes => MsgMovementPacket.TryParse(ReadFrame(bytes), out _));
        await Open(owner, left, right);
        await Trade(owner, left, new(6) { ItemUID = 10 });
        await Trade(owner, left, new(7) { Money = 300 });
        await Trade(owner, right, new(12) { CPS = 10 });
        if (confirm) await Trade(owner, left, new(9));
        var originalLeft = Current(owner, left);
        var originalRight = Current(owner, right);
        var beforeSettlement = SaveBytes();
        if (validJournal)
        {
            // Explicit fault-injection fixture: publish a valid no-op journal
            // through the real journal API after this owner's startup. This tests
            // the pending-record admission fence, not an actual process crash.
            var changes = new[] { leftUid, rightUid }.SelectMany(uid => new[]
            {
                new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, uid,
                    beforeSettlement[UserPath(uid)], beforeSettlement[UserPath(uid)]),
                new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Items, uid,
                    beforeSettlement[ItemPath(uid)], beforeSettlement[ItemPath(uid)]),
            }).ToArray();
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(0), out _));
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
            File.WriteAllText(PendingPath, "invalid-live-fault-injection-fixture");
        }
        var journalBefore = File.ReadAllBytes(PendingPath);

        await Trade(owner, confirm ? right : left, new(confirm ? 9U : 2U));

        Assert.Same(originalLeft, Current(owner, left));
        Assert.Same(originalRight, Current(owner, right));
        Assert.True(Current(owner, left).LegacyTradeActive);
        Assert.True(Current(owner, right).LegacyTradeActive);
        AssertWallet(leftUid, 700, 50);
        AssertWallet(rightUid, 2_000, 50);
        AssertClaim(leftUid, 300, 0);
        AssertClaim(rightUid, 0, 10);
        AssertSavesUnchanged(beforeSettlement);
        Assert.Equal(journalBefore, File.ReadAllBytes(PendingPath));

        var leftFrames = left.Sent.Count;
        var rightFrames = right.Sent.Count;
        await Move(owner, left);
        var blockedLogin = CreateConnection(leftUid);
        await SendLogin(owner, blockedLogin);
        Assert.Same(originalLeft, Current(owner, left));
        Assert.Same(originalRight, Current(owner, right));
        Assert.Equal(leftFrames, left.Sent.Count);
        Assert.Equal(rightFrames, right.Sent.Count);
        Assert.False(owner.Sessions.TryGet(blockedLogin.Connection, out _));
        Assert.DoesNotContain(blockedLogin.Sent, IsAnswerOk);
        Assert.Contains("trade recovery", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        AssertSavesUnchanged(beforeSettlement);
        Assert.Equal(journalBefore, File.ReadAllBytes(PendingPath));

        // Exercise the actual queued lifetime-cancellation owner, not just the
        // frame-dispatch gate. A fenced old owner must retain unresolved claims.
        right.Lifetime.Cancel();
        await WaitForRetainedDisconnectCleanup(owner);
        Assert.Same(originalLeft, Current(owner, left));
        Assert.Same(originalRight, Current(owner, right));
        Assert.True(Current(owner, left).LegacyTradeActive);
        Assert.True(Current(owner, right).LegacyTradeActive);
        AssertWallet(leftUid, 700, 50);
        AssertWallet(rightUid, 2_000, 50);
        AssertClaim(leftUid, 300, 0);
        AssertClaim(rightUid, 0, 10);
        Assert.Equal(leftFrames, left.Sent.Count);
        Assert.Equal(rightFrames, right.Sent.Count);
        AssertSavesUnchanged(beforeSettlement);
        Assert.Equal(journalBefore, File.ReadAllBytes(PendingPath));

        var fresh = NewOwner();
        if (validJournal)
        {
            var recoveredRight = await Login(fresh, rightUid);
            var recoveredLeft = await Login(fresh, leftUid);
            AssertWallet(leftUid, 1_000, 50);
            AssertWallet(rightUid, 2_000, 60);
            AssertClearedClaim(leftUid);
            AssertClearedClaim(rightUid);
            Assert.False(Current(fresh, recoveredLeft).LegacyTradeActive);
            Assert.False(Current(fresh, recoveredRight).LegacyTradeActive);
            Assert.Equal(beforeSettlement[ItemPath(leftUid)], File.ReadAllBytes(ItemPath(leftUid)));
            Assert.Equal(beforeSettlement[ItemPath(rightUid)], File.ReadAllBytes(ItemPath(rightUid)));
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        }
        else
        {
            var rejected = CreateConnection(rightUid);
            await SendLogin(fresh, rejected);
            Assert.False(fresh.Sessions.TryGet(rejected.Connection, out _));
            Assert.DoesNotContain(rejected.Sent, IsAnswerOk);
            Assert.Contains("trade recovery", fresh.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            AssertSavesUnchanged(beforeSettlement);
            Assert.Equal(journalBefore, File.ReadAllBytes(PendingPath));
        }

        // Even if a fresh owner cleared the record, the old in-memory sessions
        // still contain debited claims and must never resume ordinary writes.
        var afterFreshOwner = SaveBytes();
        await Move(owner, left);
        await Trade(owner, left, new(2));
        Assert.Same(originalLeft, Current(owner, left));
        Assert.Same(originalRight, Current(owner, right));
        Assert.Equal(leftFrames, left.Sent.Count);
        Assert.Equal(rightFrames, right.Sent.Count);
        AssertSavesUnchanged(afterFreshOwner);
    }

    [Theory]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054")]
    [InlineData("ManagedTradeEscrowId=bad\nManagedTradeEscrowMoney=1\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=-1\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=9223372036854775808\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=0\nManagedTradeEscrowCP=4294967296")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=9223372036854775807\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=0\nManagedTradeEscrowCP=4294967295")]
    [InlineData("ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nmanagedtradeescrowid=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=1\nManagedTradeEscrowCP=0")]
    public async Task InvalidOrOverflowingClaimAnywhereFencesAdmissionBeforeAnySaveMutation(string badClaim)
    {
        const uint validUid = 1_296_031;
        const uint invalidUid = 1_296_032;
        const uint loginUid = 1_296_033;
        Seed(validUid, 700, 50, 10);
        Seed(invalidUid, 2_000, 60, 20);
        Seed(loginUid, 3_000, 70, 30);
        AppendClaim(validUid, "ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=300\nManagedTradeEscrowCP=0");
        AppendClaim(invalidUid, badClaim);
        var before = SaveBytes();
        var owner = NewOwner();
        var attempted = CreateConnection(loginUid);

        await SendLogin(owner, attempted);

        Assert.False(owner.Sessions.TryGet(attempted.Connection, out _));
        Assert.DoesNotContain(attempted.Sent, IsAnswerOk);
        Assert.Contains("trade recovery", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        AssertSavesUnchanged(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptOrConflictingJournalFencesAdmissionBeforeOrphanClaimRefunds(bool conflicting)
    {
        const uint leftUid = 1_296_041;
        const uint rightUid = 1_296_042;
        Seed(leftUid, 700, 50, 10);
        Seed(rightUid, 2_000, 50, 20);
        AppendClaim(leftUid, "ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=300\nManagedTradeEscrowCP=0");
        AppendClaim(rightUid, "ManagedTradeEscrowId=2d680b20ef8d434d8c2a31c7b867b054\nManagedTradeEscrowMoney=0\nManagedTradeEscrowCP=10");
        if (conflicting)
        {
            var changes = new[] { leftUid, rightUid }.SelectMany(uid => new[]
            {
                new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, uid,
                    File.ReadAllBytes(UserPath(uid)), File.ReadAllBytes(UserPath(uid)).Concat(new byte[] { 10 }).ToArray()),
                new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Items, uid,
                    File.ReadAllBytes(ItemPath(uid)), File.ReadAllBytes(ItemPath(uid)).Concat(new byte[] { 0 }).ToArray()),
            }).ToArray();
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(0), out _));
            File.AppendAllText(UserPath(rightUid), "UnrelatedOwnerWrite=1\n");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
            File.WriteAllText(PendingPath, "not-a-trade-journal");
        }
        var before = SaveBytes();
        var journalBefore = File.ReadAllBytes(PendingPath);
        var owner = NewOwner();
        var attempted = CreateConnection(leftUid);

        await SendLogin(owner, attempted);

        Assert.False(owner.Sessions.TryGet(attempted.Connection, out _));
        Assert.DoesNotContain(attempted.Sent, IsAnswerOk);
        Assert.Contains("trade recovery", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        AssertSavesUnchanged(before);
        Assert.Equal(journalBefore, File.ReadAllBytes(PendingPath));
    }

    public void Dispose()
    {
        foreach (var player in players) player.Lifetime.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string PendingPath => Path.Combine(UsersRoot, ".managed-trade-journal", "pending.json");
    private string UserPath(uint uid) => Path.Combine(UsersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    private string ItemPath(uint uid) => Path.Combine(ItemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private Owner NewOwner()
    {
        var log = new CapturedTradeLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot, ItemsRoot),
            ConquerProtocolProfile.VisibleDx8D13, log);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return new Owner(handler, Assert.IsType<WorldSessionStore>(field.GetValue(handler)), log);
    }

    private void Seed(uint uid, long money, long cps, uint itemUid)
    {
        File.WriteAllText(UserPath(uid), $$"""
            [Character]
            UID={{uid}}
            Name=RestartTrader{{uid}}
            Body=1008
            Face=154
            Haire=27
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=380
            PMap=1002
            PMapX=430
            PMapY=380
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money={{money}}
            ConquerPoints={{cps}}
            BoundConquerPoints=333
            BankCps=444
            PkPoints=0
            Online=1

            """);
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, new PlayerItemSnapshot(
            [new PlayerItemSnapshotItem { Uid = itemUid, ItemId = 1_088_001, StackSize = 1 }], [], [], [], [])
        {
            WarehouseItems = [new PlayerItemSnapshotItem { Uid = uid + 100, ItemId = 1_088_001, WarehouseId = 1000, StackSize = 1 }],
        });
    }

    private void AppendClaim(uint uid, string claim) => File.AppendAllText(UserPath(uid), claim + "\n");

    private Player CreateConnection(uint uid)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) => { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });
        var player = new Player(uid, connection, new CancellationTokenSource(), sent);
        players.Add(player);
        return player;
    }

    private async Task<Player> Login(Owner owner, uint uid)
    {
        var player = CreateConnection(uid);
        await SendLogin(owner, player);
        Assert.True(player.Sent.Any(IsAnswerOk),
            $"Expected ANSWER_OK for test UID {uid}, but world admission failed. Handler log:{Environment.NewLine}{owner.Log}");
        Assert.Equal(uid, Current(owner, player).Character.Uid);
        return player;
    }

    private async Task SendLogin(Owner owner, Player player) => await owner.Handler.HandleFrameAsync(player.Connection,
        ReadFrame(new LegacyMsgLoginGamePacket(player.Uid).ToFrameBytes()), player.Lifetime.Token);

    private async Task Open(Owner owner, Player left, Player right)
    {
        await Trade(owner, left, new(0) { UID = right.Uid });
        await Trade(owner, right, new(1) { UID = left.Uid });
        Assert.True(Current(owner, left).LegacyTradeActive);
        Assert.True(Current(owner, right).LegacyTradeActive);
    }

    private async Task Trade(Owner owner, Player player, LegacyTradePacket packet) => await owner.Handler.HandleFrameAsync(
        player.Connection, ReadFrame(packet.ToNativeFrameBytes()), player.Lifetime.Token);

    private async Task Move(Owner owner, Player player) => await owner.Handler.HandleFrameAsync(player.Connection,
        ReadFrame(new MsgMovementPacket
        {
            Direction = 5, Uid = player.Uid, Running = MsgMovementMode.Walk, Timestamp = 456,
        }.ToFrameBytes()), player.Lifetime.Token);

    private static async Task WaitForRetainedDisconnectCleanup(Owner owner)
    {
        const string marker = "Trade disconnect cleanup retained unresolved escrow";
        var elapsed = Stopwatch.StartNew();
        while (!owner.Log.ToString().Contains(marker, StringComparison.Ordinal) &&
            elapsed.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.True(owner.Log.ToString().Contains(marker, StringComparison.Ordinal),
            $"Queued fenced disconnect cleanup did not finish within five seconds. Handler log:{Environment.NewLine}{owner.Log}");
    }

    private static WorldSession Current(Owner owner, Player player)
    {
        Assert.True(owner.Sessions.TryGet(player.Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private void AssertWallet(uint uid, long money, long cps)
    {
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
        Assert.Equal(money, character.Money);
        Assert.Equal(cps, character.ConquerPoints);
        Assert.Equal("333", character.Attributes["BoundConquerPoints"]);
        Assert.Equal("444", character.Attributes["BankCps"]);
    }

    private Guid AssertClaim(uint uid, long money, uint cps)
    {
        var attributes = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Attributes;
        Assert.True(attributes.TryGetValue("ManagedTradeEscrowId", out var text));
        Assert.True(Guid.TryParseExact(text, "N", out var id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(money.ToString(CultureInfo.InvariantCulture), attributes["ManagedTradeEscrowMoney"]);
        Assert.Equal(cps.ToString(CultureInfo.InvariantCulture), attributes["ManagedTradeEscrowCP"]);
        return id;
    }

    private void AssertClearedClaim(uint uid)
    {
        var attributes = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Attributes;
        Assert.True(!attributes.TryGetValue("ManagedTradeEscrowId", out var id) || string.IsNullOrEmpty(id));
        Assert.True(!attributes.TryGetValue("ManagedTradeEscrowMoney", out var money) || money == "0");
        Assert.True(!attributes.TryGetValue("ManagedTradeEscrowCP", out var cps) || cps == "0");
    }

    private Dictionary<string, byte[]> SaveBytes() => Directory.GetFiles(UsersRoot, "*.ini")
        .Concat(Directory.GetFiles(ItemsRoot, "*.bin")).ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private void AssertSavesUnchanged(Dictionary<string, byte[]> expected)
    {
        var actual = SaveBytes();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static bool IsAnswerOk(byte[] bytes) => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Content.Count > 3 && packet.Content[3] == "ANSWER_OK";

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    // The handler and retained disconnect cleanup can log on different threads.
    // Writes and diagnostic snapshots must share the same lock.
    private sealed class CapturedTradeLog : StringWriter
    {
        private readonly object gate = new();

        public override void Write(char value)
        {
            lock (gate) base.Write(value);
        }

        public override void Write(string? value)
        {
            lock (gate) base.Write(value);
        }

        public override void WriteLine(string? value)
        {
            lock (gate) base.WriteLine(value);
        }

        public override Task WriteAsync(string? value)
        {
            lock (gate) base.Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(string? value)
        {
            lock (gate) base.WriteLine(value);
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            lock (gate) return base.ToString();
        }
    }

    private sealed record Owner(WorldServerFrameHandler Handler, WorldSessionStore Sessions, StringWriter Log);
    private sealed record Player(uint Uid, ConquerTcpConnection Connection, CancellationTokenSource Lifetime, ConcurrentQueue<byte[]> Sent);
}
