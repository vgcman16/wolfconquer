using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false, "throw")]
    [InlineData(false, "retire")]
    [InlineData(false, "retireThrow")]
    [InlineData(false, "ended")]
    [InlineData(true, "throw")]
    [InlineData(true, "retire")]
    [InlineData(true, "retireThrow")]
    [InlineData(true, "ended")]
    public Task CommittedChestPublicationFaultNeverReopensOldPayment(bool cp, string fault) => RunAsync(async fixture =>
    {
        fixture.Seed(39, firstStack: 2, secondStack: 1, firstBound: 1);
        await fixture.AssertCommittedChestPublicationAsync(cp, fault);
    });

    private sealed partial class Fixture
    {
        // Failure/retirement observation only, after the real physical send
        // delegate captured its frame. Never supplies accepted grant/save state.
        private Action? ChestSendObserver { get; set; }

        internal async Task AssertCommittedChestPublicationAsync(bool cp, string fault)
        {
            var plan = PrepareChestJournal(cp);
            var context = Context;
            var original = Session;
            var originalBytes = JsonSerializer.SerializeToUtf8Bytes(original);
            WorldSession? committed = null;
            ChestSendObserver = () =>
            {
                committed = Current;
                Assert.Equal(plan.Updated with { PlayerItems = committed.PlayerItems }, committed);
                Assert.Equal(plan.After.Bytes, File.ReadAllBytes(ItemPath));
                if (cp) AssertCharacterCpSaved();
                else Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
                if (fault == "ended")
                {
                    lock (requiredChestTrades.SyncRoot) requiredChestTrades.MarkConnectionEnded(Connection);
                }
                else if (fault != "throw") Assert.True(sessions.TryRemove(Connection, out _));
                if (fault is "throw" or "retireThrow") throw new IOException("Owned postcommit send observation failed.");
            };
            // The whole production owner contains postcommit transport/observer
            // failures. The synchronous observer proves disk+Store before frame1.
            await InvokeChestOwnerAsync(context, original, cp);
            ChestSendObserver = null;
            Assert.NotNull(committed);
            Assert.Equal(plan.Updated with { PlayerItems = committed.PlayerItems }, committed);
            if (fault is "throw" or "ended") Assert.Same(committed, Current);
            else Assert.False(sessions.TryGet(Connection, out _));
            Assert.Single(Sent);
            AssertDebitFrame();
            Assert.Equal(originalBytes, JsonSerializer.SerializeToUtf8Bytes(original));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems));
            Assert.Equal(plan.After.Bytes, File.ReadAllBytes(ItemPath));
            if (cp) AssertCharacterCpSaved();
            else Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.False(LegacyTradeJournal.HasPending(Users));
            Assert.False(requiredChestRecovery!.IsBlocked);
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            // Same stale invocation cannot charge again, even when its original
            // key had stack2 and the committed copy still owns another payment.
            await InvokeChestOwnerAsync(context, original, cp);
            Assert.Single(Sent);
            if (fault is "throw" or "ended") Assert.Same(committed, Current);
            else Assert.False(sessions.TryGet(Connection, out _));
            foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            AssertOnlyExpectedFiles();
        }
    }
}
