using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    private static readonly TimeSpan SettlementBound = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentBag<BoothFixture> UnfinishedBoothSettlements = [];

    private static async Task RunBoothSettlementAsync(Func<BoothFixture, Task> body)
    {
        var fixture = new BoothFixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupSettlementAsync(errors); }
        catch (Exception error)
        {
            errors.Add(error);
            UnfinishedBoothSettlements.Add(fixture);
            errors.Add(new InvalidOperationException($"Cleanup fault retained ownedRoot={fixture.Root}."));
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Booth settlement test/cleanup failed.", errors);
    }

    private sealed partial class BoothFixture
    {
        internal LegacyTradeStateStore SettlementTrades { get; } = new();
        internal WorldOperationBarrier SettlementOperations { get; } = new();
        internal LegacyTradeRecoveryState SettlementRecovery { get; private set; } = null!;
        internal Func<string, byte[], Task>? SettlementSendObserver { get; set; }
        internal List<TaskCompletionSource> SettlementReleases { get; } = [];
        private readonly HashSet<Task> settlementObserved = [];

        private ConquerTcpConnection CreateSettlementConnection(string recipient) => new(
            localEndPoint: null, remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: async (frame, _) =>
            {
                var bytes = frame.ToArray();
                lock (Harness.Sent) Harness.Sent.Add(new D13VendorSentFrame(recipient, bytes));
                if (SettlementSendObserver is { } observer) await observer(recipient, bytes);
            });

        private void InitializeSettlementOwners()
        {
            Assert.Null(SettlementRecovery);
            SettlementRecovery = new(Harness.UsersRoot, Harness.PlayerItemsRoot);
            Assert.True(SettlementRecovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(SettlementRecovery.IsReadyFor(Harness.UsersRoot, Harness.PlayerItemsRoot));
            Assert.False(SettlementRecovery.IsBlocked);
        }

        private async Task DispatchSettlementScopeAsync(ConquerTcpConnection connection, byte[] bytes)
        {
            var frame = D13VendorTestHarness.ReadFrame(bytes);
            var isBuy = MsgItemUsagePacket.TryParse(frame, out var request) &&
                request.Action == MsgItemUsageAction.BuyVendingItem;
            // Buy owns its real exclusive lease. Never nest Shared -> Exclusive.
            using var operation = isBuy ? null : await SettlementOperations.EnterSharedAsync();
            await Registry.HandleAsync(Context(connection), frame);
        }

        internal void SeedSettlementLongSellerAccount(long accountId)
        {
            Assert.True(accountId > uint.MaxValue);
            var seller = Harness.CreateSeller(300, 50, SevenStores(new[] { BoothItem(10) }, 40_000));
            SeedOne(SellerConnection, seller with { AccountId = accountId });
            SeedOne(BuyerConnection, Harness.CreateBuyer(1_000, 200,
                SevenStores(new[] { BoothItem(50) }, 30_000)));
            InitializeSettlementOwners();
            Assert.Equal(accountId, Seller.AccountId);
            Assert.NotEqual(Seller.AccountId, Seller.Character.Uid);
            Assert.True(File.Exists(CharacterPath(Seller)));
        }

        private async Task DepositSettlementScopeAsync(uint childUid)
        {
            var request = new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashDepositItem, childUid, 0, []);
            await Registry.HandleAsync(Context(SellerConnection), D13VendorTestHarness.ReadFrame(request.ToFrameBytes()));
            Assert.DoesNotContain(Seller.PlayerItems.InventoryItems, item => item.Uid == childUid);
            Assert.Single(Assert.Single(Seller.PlayerItems.InventoryItems, item => item.Uid == 10).DepositedItems,
                item => item.Uid == childUid);
            // The registered transfer owns its Exclusive commit and actual save.
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Seller.PlayerItems), File.ReadAllBytes(ItemPath(Seller)));
        }

        internal async Task ObserveSettlementAsync(Task actual)
        {
            try { await actual.WaitAsync(SettlementBound); }
            catch (Exception observation)
            {
                if (actual.IsCompleted)
                {
                    Exception? terminal = null;
                    try { await actual; } catch (Exception error) { terminal = error; }
                    settlementObserved.Add(actual);
                    if (terminal is not null && !ReferenceEquals(observation, terminal))
                        throw new AggregateException(observation, terminal);
                }
                throw;
            }
            settlementObserved.Add(actual);
        }

        internal async Task ObserveExpectedSettlementSendFailureAsync(int firstCall, IOException expected)
        {
            foreach (var actual in Calls.Skip(firstCall).ToArray())
            {
                var fault = await Record.ExceptionAsync(() => ObserveSettlementAsync(actual));
                Assert.Same(expected, Assert.IsType<IOException>(fault));
            }
        }

        internal void AssertSettlementSaved()
        {
            foreach (var session in new[] { Buyer, Seller })
            {
                var path = CharacterPath(session);
                var character = CharacterSnapshotParser.ParseText(File.ReadAllText(path), path);
                Assert.Equal(session.Character.Uid, character.Uid);
                Assert.Equal(session.Character.Money, character.Money);
                Assert.Equal(session.Character.ConquerPoints, character.ConquerPoints);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems), File.ReadAllBytes(ItemPath(session)));
            }
            Assert.False(LegacyTradeJournal.HasPending(Harness.UsersRoot));
            Assert.False(SettlementRecovery.IsBlocked);
        }

        internal void SeedSettlementClaim(bool buyer, bool malformed)
        {
            var connection = buyer ? BuyerConnection : SellerConnection;
            var before = buyer ? Buyer : Seller;
            if (malformed)
            {
                var values = new Dictionary<string, string>
                {
                    [LegacyTradeEscrowClaims.IdKey] = "not-an-escrow-id",
                    [LegacyTradeEscrowClaims.MoneyKey] = "25",
                    [LegacyTradeEscrowClaims.CpKey] = "0",
                };
                Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(CharacterPath(before),
                    before.Character.Uid, values, detailedIoFailures: true, out var reason), reason);
                var attrs = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in values) attrs[key] = value;
                Harness.Sessions.Set(connection, before with { Character = before.Character with { Attributes = attrs } });
                Assert.False(LegacyTradeEscrowClaims.TryRead((buyer ? Buyer : Seller).Character.Attributes, out _, out _));
                return;
            }
            // Real isolated orphan/pending claim, injected AFTER startup recovery.
            // This is not an invented active trade or a restart recovery proof.
            var money = before.Character.Money - 25;
            var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Money"] = money.ToString(CultureInfo.InvariantCulture) };
            var updated = LegacyTradeEscrowClaims.WithClaim(before with
            { Character = before.Character with { Money = money, Attributes = attributes } },
                new LegacyTradeEscrowClaim(Guid.Parse("0f4d63e5-98bd-4763-9b95-a55db76cb7cb"), 25, 0));
            Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(before, updated, out var failure), failure);
            Harness.Sessions.Set(connection, updated);
            Assert.True(LegacyTradeEscrowClaims.TryRead(updated.Character.Attributes, out var claim, out failure), failure);
            Assert.NotNull(claim);
            Assert.Equal(25, claim.Money);
        }

        internal async Task CleanupSettlementAsync(List<Exception> errors)
        {
            foreach (var release in SettlementReleases) release.TrySetResult(); // Only our send holds.
            var terminal = true;
            foreach (var actual in Calls.ToArray())
            {
                if (settlementObserved.Contains(actual)) continue;
                try { await ObserveSettlementAsync(actual); }
                catch (Exception error) { errors.Add(error); if (!actual.IsCompleted) terminal = false; }
            }
            if (!terminal)
            {
                UnfinishedBoothSettlements.Add(this);
                errors.Add(new InvalidOperationException($"Actual booth work is unfinished; retained ownedRoot={Root}."));
                return;
            }
            foreach (var connection in new[] { SellerConnection, BuyerConnection })
            {
                try
                {
                    var close = connection.CompleteTransportCloseAsync();
                    Calls.Add(close);
                    await ObserveSettlementAsync(close);
                }
                catch (Exception error) { errors.Add(error); terminal = false; }
            }
            if (terminal && (object?)settlementFrameOwner is IAsyncDisposable drainOwner)
            {
                var drain = drainOwner.DisposeAsync().AsTask();
                Calls.Add(drain);
                try { await ObserveSettlementAsync(drain); }
                catch (Exception error) { errors.Add(error); terminal = false; }
            }
            if (!terminal)
            {
                UnfinishedBoothSettlements.Add(this);
                errors.Add(new InvalidOperationException($"Booth transport/owner closure is unproven; retained ownedRoot={Root}."));
                return;
            }
            SettlementSendObserver = null;
            try
            {
                var path = Path.GetFullPath(Root);
                var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                if (!string.Equals(Path.GetDirectoryName(path), temporary,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    !Path.GetFileName(path).StartsWith("WolfConquer-D13VendorTests-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing cleanup outside the exact owned booth root.");
                Harness.Dispose();
            }
            catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
    }
}
