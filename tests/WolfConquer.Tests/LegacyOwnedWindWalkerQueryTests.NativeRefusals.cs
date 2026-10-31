using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Theory]
    [InlineData(true, "words-reference", 2)]
    [InlineData(true, "registrations-reference", 2)]
    [InlineData(true, "orphan-bit", 3)]
    [InlineData(false, "words-reference", 3)]
    public Task RealQueryComparerInterferenceCannotPartiallyRemoveRenewOrPublishTheGenuineFan(
        bool remove, string mutation, int interferingLookup) => Run(async f =>
    {
        AssertJoined(f, true); var native = f.Native; var player = native.Player!; var vector = player.BitVector;
        var row = f.Row!; var timer = row.Timer; var originalWords = vector.bits; var originalRegistrations = vector.ArrayFlags;
        var captured = NativeRows(originalRegistrations); var beforeWords = (uint[])originalWords.Clone();
        var managedBefore = LegacyWindWalkerStatusProjection.Read(f.Current.Character);
        uint[]? injectedWords = null; ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>? injectedRegistrations = null;
        var comparer = new WindInterferenceComparer(interferingLookup, () =>
        {
            // Failure-only escaped raw interference. It never supplies an
            // accepting result, owner, flag row, timer or query capability.
            if (mutation == "words-reference") vector.bits = injectedWords = (uint[])originalWords.Clone();
            else if (mutation == "registrations-reference")
                vector.ArrayFlags = injectedRegistrations = new(captured.Select(value => new KeyValuePair<int, LegacyNativeStatusFlagsBigVector32.Flag>(value.Key, value.Row)));
            else originalWords[0] ^= 1U; // Deliberately invalid orphan bit; do not silently repair it.
        }, native, player, vector);
        // This declared negative comparer retains ONLY the actual producer's
        // existing registrations. No new positive raw Flag is constructed.
        var escaped = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(
            captured.Select(value => new KeyValuePair<int, LegacyNativeStatusFlagsBigVector32.Flag>(value.Key, value.Row)), comparer);
        Dictionary<string, byte[]>? committedEquipmentFiles = null; bool armed = false; int first = f.Sent.Count;
        vector.ArrayFlags = escaped;
        try
        {
            AssertJoined(f, true); // Inert comparer, same genuine actors/rows, before the actual operation.
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Uid || armed || !LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                armed = true; committedEquipmentFiles = f.Owner.Data.Files();
                Assert.Equal(remove ? (ushort)0 : (ushort)5,
                    Assert.Single(f.Current.PlayerItems.InventoryItems.Concat(f.Current.PlayerItems.EquipmentItems), value => value.Uid == Left).Position);
                Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
                // Only the real post-perfection native preparation is armed.
                // Existing query prefix/Store reads cannot consume this trigger.
                comparer.Arm();
            };
            if (remove) await f.UnequipAsync(); else await f.QueryAsync();
            f.BeforeSend = null;
            Assert.True(armed); Assert.True(comparer.Fired); Assert.True(comparer.FiredUnderActors);
            Assert.Equal(interferingLookup, comparer.FiredAtLookup); Assert.Null(comparer.InterferenceFailure);
            Assert.NotNull(committedEquipmentFiles); f.Owner.Data.AssertFiles(committedEquipmentFiles!);
            Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
            Assert.True(LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(f.Sent[^1]), out _));
            Assert.Equal(managedBefore, LegacyWindWalkerStatusProjection.Read(f.Current.Character));
            Assert.Equal(beforeWords[6], vector.bits[6]); Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
            AssertNativeRows(captured, vector.ArrayFlags); // Every actual unrelated registration/ref/policy remains intact.
            var expectedWords = (uint[])beforeWords.Clone(); if (mutation == "orphan-bit") expectedWords[0] ^= 1U;
            Assert.Equal(expectedWords, vector.bits);
            Assert.Same(injectedWords ?? originalWords, vector.bits);
            Assert.Same(injectedRegistrations ?? escaped, vector.ArrayFlags);
        }
        finally
        {
            f.BeforeSend = null; comparer.Disarm();
            // Restore only the declared escaped negative surface for cleanup.
            // No production preparation/commit/publication is retried here.
            beforeWords.CopyTo(originalWords, 0); vector.bits = originalWords; vector.ArrayFlags = originalRegistrations;
        }
        AssertJoined(f, true); Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
    }, mode: "pair");
}
