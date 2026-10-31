using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task RealCompletedAccountsReleaseStrongClaimsBeforeOutputAndEofDoesNotAccumulateThem() => Run(async f =>
    {
        const int count = 8;
        var initial = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        uint highWater = initial["HighWater"]!.GetValue<uint>(); ulong generation = initial["Generation"]!.GetValue<ulong>();
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
        for (int index = 0; index < count; index++)
        {
            uint account = CreationFixture.Account + (uint)index;
            string name = "Life" + index;
            var entered = await f.EnterLifetimeAccountAsync(account, "life" + index, index == 0 ? f.Connection : null);
            Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, entered.Connection));
            Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
            int observed = 0;
            f.BeforeSend = (uid, _) =>
            {
                Assert.Equal(account, uid);
                Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
                AssertLifetimeCreated(f, entered.Connection, account, name, entered.Ticket);
                observed++;
            };
            try { await f.CreateLifetimeAccountAsync(entered.Connection, name); }
            finally { f.BeforeSend = null; }
            Assert.True(observed > 0); AssertLifetimeCreated(f, entered.Connection, account, name, entered.Ticket);
            Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
            var world = f.LifetimeWorldFiles();
            var allFiles = f.Data.Files();
            await entered.Connection.CompleteTransportCloseAsync();
            Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
            Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, entered.Connection));
            Assert.False(f.Store.TryGet(entered.Connection, out _));
            AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles());
            f.Data.AssertFiles(allFiles);
        }
        var final = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        Assert.Equal(highWater + count * 4U, final["HighWater"]!.GetValue<uint>());
        Assert.Equal(generation + count * 3UL, final["Generation"]!.GetValue<ulong>());
        Assert.Equal((ulong)count, final["ArchiveCount"]!.GetValue<ulong>());
    }, enter: false);

    [Fact]
    public Task ActualPreCharacterEofReleasesTheUnusedClaimWithoutChangingWorldOrUidState() => Run(async f =>
    {
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        var world = f.LifetimeWorldFiles();
        var allFiles = f.Data.Files();
        Assert.True(f.Runtime.TryEnterAuthenticatedCreation(f.Handler, f.Connection, "AfterEof", out var borrowed, out var reason), reason);
        Task closed;
        using (Assert.IsType<LegacyOwnedAdmissionRuntime.CreationLease>(borrowed))
        {
            // Genuine synchronous Runtime borrow; never await while its
            // creation monitor is held. EOF must defer exact-reference removal
            // until this same real borrow disposes against the closed transport.
            closed = f.Connection.CompleteTransportCloseAsync();
            Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
            Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
            f.Data.AssertFiles(allFiles);
        }
        await closed;
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles());
        f.Data.AssertFiles(allFiles);
        var next = await f.EnterLifetimeAccountAsync(CreationFixture.Account, "creation-owner");
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, next.Connection));
        await f.CreateLifetimeAccountAsync(next.Connection, "AfterEof");
        AssertLifetimeCreated(f, next.Connection, CreationFixture.Account, "AfterEof", next.Ticket);
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
    });

    [Fact]
    public Task RepeatedOldTransportEofCannotRemoveANewerActualRedemptionForTheSameAccount() => Run(async f =>
    {
        var old = f.Connection;
        await old.CompleteTransportCloseAsync(); Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
        var next = await f.EnterLifetimeAccountAsync(CreationFixture.Account, "creation-owner");
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, next.Connection));
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        var world = f.LifetimeWorldFiles();
        var allFiles = f.Data.Files();
        // Invoke the actual old TCP close path again, without constructing a
        // redemption/callback. Cleanup must compare the exact old reference.
        await old.CompleteTransportCloseAsync();
        AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles());
        f.Data.AssertFiles(allFiles);
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, next.Connection));
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, old));
        await f.CreateLifetimeAccountAsync(next.Connection, "NewerClaim");
        AssertLifetimeCreated(f, next.Connection, CreationFixture.Account, "NewerClaim", next.Ticket);
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualCompletedHistoryDeniesAnotherCreationEvenWhenAllSevenParticipantLeavesAreMissing(bool restart) => Run(async f =>
    {
        await CreateAndAssert(f);
        var original = CompletedIntent(f); var payload = original.CopyPayload(); var allocation = original.Allocation!;
        var beforeEof = f.Data.Files();
        await f.Connection.CompleteTransportCloseAsync(); Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
        f.Data.AssertFiles(beforeEof);
        if (restart)
        {
            await f.CloseAsync();
            using (var custody = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                f.DeleteCompletedLifetimeParticipantInputs();
            f.OpenLifetimeFrame();
        }
        else f.DeleteCompletedLifetimeParticipantInputs();
        foreach (var path in f.LifetimeParticipantPaths()) Assert.False(File.Exists(path));
        var world = f.LifetimeWorldFiles();
        var entered = await f.EnterLifetimeAccountAsync(CreationFixture.Account, "creation-owner");
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, entered.Connection));
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
        await f.CreateLifetimeAccountAsync(entered.Connection, "SecondBirth");
        Assert.Empty(f.Frames(CreationFixture.Account)); Assert.False(f.Store.TryGet(entered.Connection, out _));
        Assert.Equal(0U, entered.Connection.NativeGameClient!.Player.UID);
        AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles());
        foreach (var path in f.LifetimeParticipantPaths()) Assert.False(File.Exists(path));
        var retained = CompletedIntent(f); Assert.Equal(payload, retained.CopyPayload()); Assert.Equal(allocation, retained.Allocation);
        Assert.False(f.Runtime.Recovery.IsBlocked); Assert.False(File.Exists(f.PendingPath));
    });

    [Fact]
    public Task IssuedUncertainEofOnlyReleasesReferencesAndStartupFinishesTheSameActualRange() => Run(async f =>
    {
        var before = BeforeCreation(f);
        f.Handler.AuthenticatedCreationProducerInterruption = LegacyAuthenticatedCreationInterruption.AfterAllocation;
        try { await f.CreateAsync(); }
        finally { f.Handler.AuthenticatedCreationProducerInterruption = LegacyAuthenticatedCreationInterruption.None; }
        Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.False(File.Exists(f.PendingPath));
        var stateBytes = File.ReadAllBytes(f.Data.State); var issued = JsonNode.Parse(stateBytes)!;
        var intent = Assert.Single(issued["ProducerIntents"]!.AsArray())!;
        var allocation = Assert.Single(issued["Operations"]!.AsArray())!;
        var payload = Convert.FromBase64String(intent["PayloadBase64"]!.GetValue<string>());
        uint first = allocation["First"]!.GetValue<uint>(); int count = allocation["Count"]!.GetValue<int>();
        ulong allocatedGeneration = allocation["Generation"]!.GetValue<ulong>();
        Assert.Equal(before.HighWater + 1, first);
        var world = f.LifetimeWorldFiles();
        var allFiles = f.Data.Files();
        await f.Connection.CompleteTransportCloseAsync();
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        Assert.False(f.Runtime.Ordinary.IsCurrent(f.Data.Options().Roots));
        Assert.Equal(stateBytes, File.ReadAllBytes(f.Data.State)); AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles());
        f.Data.AssertFiles(allFiles);
        await f.CreateAsync(); Assert.Empty(f.Sent); AssertLifetimeWorldFiles(world, f.LifetimeWorldFiles()); f.Data.AssertFiles(allFiles);
        await f.CloseAsync();
        using (f.Open())
        {
            var completed = CompletedIntent(f); Assert.Equal(payload, completed.CopyPayload());
            Assert.Equal(first, completed.Allocation!.First); Assert.Equal(count, completed.Allocation.Count);
            Assert.Equal(allocatedGeneration, completed.Allocation.Generation);
            var recipe = Recipe(f, completed);
            foreach (var image in recipe.AfterImages(completed.Allocation))
                if (image.Key == LegacyTradeJournalFileKind.Mailbox) Assert.Null(image.Value);
                else Assert.Equal(image.Value, File.ReadAllBytes(f.DataPath(image.Key)));
            Assert.Equal(ExpectedStarterItems(recipe.Projection, completed.Allocation), File.ReadAllBytes(f.ItemsPath));
            var settled = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
            Assert.Equal(before.HighWater + (uint)count, settled["HighWater"]!.GetValue<uint>());
            Assert.Equal(before.Generation + 3, settled["Generation"]!.GetValue<ulong>());
            Assert.Equal(0, f.Runtime.RetainedCreationAccountCount); Assert.False(File.Exists(f.PendingPath));
        }
        var settledFiles = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settledFiles);
    });

    [Fact]
    public Task ActualUnknownPhysicalUidRefusesBeforeIntentAndLeavesTheRealUnusedPermitRetryable() => Run(async f =>
    {
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        var ledger = f.Ledger();
        var foreign = Path.Combine(f.Data.Items, (CreationFixture.Account + 200).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
        Assert.False(File.Exists(foreign));
        // Declared NEGATIVE external item input in this owned fixture only.
        // Its UID was never issued by this actual ledger; no receipt/history is
        // fabricated and no production path may delete or repair this evidence.
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(new(
            [new PlayerItemSnapshotItem { Uid = checked(ledger.HighWater + 1), ItemId = CreationFixture.RuneType,
                Position = 0, StackSize = 1, Durability = 100, MaximumDurability = 100 }], [], [], [], []));
        File.WriteAllBytes(foreign, image);
        var files = f.Data.Files();
        await f.CreateAsync();
        f.Data.AssertFiles(files); Assert.Equal(ledger, f.Ledger()); Assert.Empty(f.Sent);
        Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment); Assert.Null(f.Native.Rune);
        Assert.False(f.Runtime.Recovery.IsBlocked); Assert.False(File.Exists(f.PendingPath));
        Assert.Equal(1, f.Runtime.RetainedCreationAccountCount);
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        Assert.Equal(image, File.ReadAllBytes(foreign));
        foreach (var path in f.LifetimeParticipantPaths()) Assert.False(File.Exists(path));
        File.Delete(foreign); // Remove only the explicitly injected negative input.
        await CreateAndAssert(f);
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
    });

    private static void AssertLifetimeCreated(CreationFixture f, ConquerTcpConnection connection, uint account,
        string name, string ticket)
    {
        Assert.True(f.Runtime.Ordinary.Registry.TryReadProducerIntents(f.Data.ExpectedDomain, out var all, out var reason), reason);
        var completed = Assert.Single(all, row => row.Request.AccountId == account);
        Assert.True(f.Runtime.Ordinary.Registry.TryReadActiveProducerIntents(f.Data.ExpectedDomain, out var active, out reason), reason);
        Assert.Empty(active); Assert.NotNull(completed.Allocation);
        var recipe = Recipe(f, completed); Assert.Equal(name, recipe.Projection.Name);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket))).ToLowerInvariant(), recipe.TicketDigest);
        var current = f.CurrentFor(connection); var native = current.NativeGameClient!;
        Assert.Equal((long)account, current.AccountId); Assert.Equal((long)account, current.Character.Uid);
        Assert.Equal(account, native.Player.UID); Assert.True(f.Store.IsBoundSpellOwnerCurrent(native.OrdinarySpellBinding!));
        var items = ExpectedStarterItems(recipe.Projection, completed.Allocation!);
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(items, File.ReadAllBytes(f.Data.Options().Roots.PathFor(LegacyTradeJournalFileKind.Items, account)));
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, connection));
        Assert.Equal(0, f.Runtime.RetainedCreationAccountCount); Assert.False(File.Exists(f.PendingPath));
    }

    private static void AssertLifetimeWorldFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
