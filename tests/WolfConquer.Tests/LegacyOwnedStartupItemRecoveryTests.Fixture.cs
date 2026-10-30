using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedStartupItemRecoveryTests
{
    private const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    private const uint ItemUid = 1_000_091;
    private const uint ItemId = 1_000_000;

    private static Task WithFixture(Action<StartupFixture> test, bool separateAccount = false) =>
        WithFixtureAsync(scope => { test(scope); return Task.CompletedTask; }, separateAccount);

    private static async Task WithFixtureAsync(Func<StartupFixture, Task> test, bool separateAccount = false)
    {
        StartupFixture? scope = null;
        Exception? primary = null;
        try { scope = new StartupFixture(); scope.Initialize(separateAccount); await test(scope); }
        catch (Exception error) { primary = error; }
        List<Exception> cleanup = scope is null ? [] : await scope.CloseAsync();
        if (primary is null && cleanup.Count == 0 && scope is not null)
            try { scope.Data.Dispose(); } catch (Exception error) { cleanup.Add(error); }
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException(cleanup);
        // Failed assertions/cleanup deliberately retain this guarded owned temp root.
    }

    private sealed class StartupFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly List<Func<ValueTask>> close = [];
        internal long Account { get; private set; }
        internal string CharacterPath => Path.Combine(Data.Users, Account.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        internal string StagePath(bool character) => (character ? CharacterPath : ItemsPath) + ".managed-trade-" +
            JsonNode.Parse(File.ReadAllBytes(PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
        internal byte[] BeforeCharacter = [], AfterCharacter = [], BeforeItems = [], AfterItems = [];
        internal byte[] RegistryState = [];
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Data.State))!["HighWater"]!.GetValue<uint>();

        internal void Initialize(bool separateAccount)
        {
            Account = separateAccount ? 31 : Uid;
            if (separateAccount) File.Move(Data.UserPath, CharacterPath);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, Snapshot(ItemUid, 5));
            Data.Bootstrap();
            BeforeCharacter = File.ReadAllBytes(CharacterPath);
            AfterCharacter = Replace(BeforeCharacter, "Money=777\r\n", "Money=700\r\n");
            BeforeItems = File.ReadAllBytes(ItemsPath);
            AfterItems = PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid, 10));
            RegistryState = File.ReadAllBytes(Data.State);
        }

        internal IReadOnlyList<LegacyTradeJournalChange> Changes(byte[]? afterItems = null, byte[]? afterCharacter = null) =>
            [new(LegacyTradeJournalFileKind.Character, Account, BeforeCharacter, afterCharacter ?? AfterCharacter),
             new(LegacyTradeJournalFileKind.Items, Uid, BeforeItems, afterItems ?? AfterItems)];

        internal void Stage(int boundary, byte[]? afterItems = null, byte[]? afterCharacter = null)
        {
            using var interruption = LegacyJournalInterruption.StopAfter(boundary);
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(Data.Users, Data.Items,
                Changes(afterItems, afterCharacter), interruption, out var reason));
            Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
            Assert.True(interruption.PendingObserved);
            Assert.Equal(Enumerable.Range(0, boundary + 1), interruption.Visited);
            Assert.True(LegacyTradeJournal.HasPending(Data.Users));
            Assert.Equal(boundary >= 1 ? afterCharacter ?? AfterCharacter : BeforeCharacter, File.ReadAllBytes(CharacterPath));
            Assert.Equal(boundary >= 2 ? afterItems ?? AfterItems : BeforeItems, File.ReadAllBytes(ItemsPath));
        }

        internal void StopAfterStageFlushed(int ordinal)
        {
            using var interruption = LegacyJournalInterruption.StopAfterStageFlushed(ordinal);
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(Data.Users, Data.Items,
                Changes(), interruption, out var reason));
            Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
            Assert.True(interruption.PendingObserved);
            Assert.True(LegacyTradeJournal.HasPending(Data.Users));
            Assert.Equal(BeforeCharacter, File.ReadAllBytes(CharacterPath));
            Assert.Equal(BeforeItems, File.ReadAllBytes(ItemsPath));
            Assert.Equal(AfterCharacter, File.ReadAllBytes(StagePath(character: true)));
            if (ordinal == 2) Assert.Equal(AfterItems, File.ReadAllBytes(StagePath(character: false)));
            else Assert.False(File.Exists(StagePath(character: false)));
        }

        internal LegacyOwnedAdmissionRuntime Open()
        {
            var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
            if (opened is not null) OwnAsync(opened);
            Assert.True(success, reason);
            var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Assert.True(runtime.Ordinary.IsReconciliationComplete);
            Assert.True(runtime.Recovery.IsReadyForOrdinaryAdmission(Data.Options().Roots));
            return runtime;
        }

        internal void AssertRecoveredImages()
        {
            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
            Assert.Equal(AfterCharacter, File.ReadAllBytes(CharacterPath));
            Assert.Equal(AfterItems, File.ReadAllBytes(ItemsPath));
            Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
            Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, HighWater);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _),
                Path.GetFileName(CharacterPath));
            Assert.Equal(Uid, saved.Uid);
            Assert.Equal(700L, saved.Money);
            var item = Assert.Single(new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(Uid).InventoryItems);
            Assert.Equal(ItemUid, item.Uid);
            Assert.Equal((ushort)10, item.Durability);
            Assert.Equal((ushort)10, item.MaximumDurability);
        }

        internal void AssertRefused(LegacyOwnedAdmissionOptions? options = null, bool leaseAvailable = true)
        {
            var before = Data.Files();
            var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, options ?? Data.Options(), out var opened, out var reason);
            if (opened is not null) OwnAsync(opened);
            Assert.False(success, reason);
            Assert.Null(opened);
            Assert.NotEmpty(reason);
            Data.AssertFiles(before);
            Assert.True(LegacyTradeJournal.HasPending(Data.Users));
            if (leaseAvailable) AssertLeaseAvailable();
        }

        internal void AssertLeaseAvailable()
        {
            using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(0L, lease.Length);
        }

        internal T Own<T>(T resource) where T : IDisposable
        { close.Add(() => { resource.Dispose(); return ValueTask.CompletedTask; }); return resource; }
        internal T OwnAsync<T>(T resource) where T : IAsyncDisposable
        { close.Add(resource.DisposeAsync); return resource; }
        internal void CloseWith(Func<ValueTask> action) => close.Add(action);
        internal async Task<List<Exception>> CloseAsync()
        {
            var errors = new List<Exception>();
            for (var index = close.Count - 1; index >= 0; index--)
                try { await close[index](); } catch (Exception error) { errors.Add(error); }
            return errors;
        }
    }

    private static PlayerItemSnapshot Snapshot(uint uid, ushort durability, bool includeBirth = false) => new(
        includeBirth ? [Item(uid, durability), Item(uid + 1, durability)] : [Item(uid, durability)], [], [], [], []);

    private static PlayerItemSnapshotItem Item(uint uid, ushort durability) => new()
    { Uid = uid, ItemId = ItemId, Durability = durability, MaximumDurability = 10, StackSize = 1, Color = 5 };

    private static byte[] Replace(byte[] image, string before, string after)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
        Assert.Contains(before, text, StringComparison.Ordinal);
        return encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(before, after, StringComparison.Ordinal))).ToArray();
    }
}
