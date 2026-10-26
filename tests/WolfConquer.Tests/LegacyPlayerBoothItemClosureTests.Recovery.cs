using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public Task ActualFrameRecoversRegisteredBoothWalBeforeAdmissionWithoutClaimsOrSecondPayment(int prefix) =>
        RunBoothSettlementAsync(async fixture =>
        {
            var images = await fixture.PrepareRegisteredBoothRecoveryImagesAsync();
            fixture.PublishInterruptedBoothRecoveryImages(images, prefix);
            var firstAdmissionFrame = fixture.Harness.Sent.Count;
            var recoveredBeforeOutput = 0;
            Exception? recoveryObservationFailure = null;
            fixture.SettlementSendObserver = (_, _) =>
            {
                try
                {
                    fixture.AssertFiles(images.After.Files);
                    Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
                }
                catch (Exception error)
                {
                    var observed = fixture.ReadFiles();
                    var changed = observed.Keys.Concat(images.After.Files.Keys).Distinct().Where(key =>
                        !observed.TryGetValue(key, out var actual) || !images.After.Files.TryGetValue(key, out var expected) ||
                        !actual.AsSpan().SequenceEqual(expected));
                    recoveryObservationFailure ??= new InvalidOperationException(
                        "Recovery send file mismatch: " + string.Join(", ", changed), error);
                }
                recoveredBeforeOutput++;
                return Task.CompletedTask;
            };
            fixture.CreateBoothRecoveryFrameOwner();

            await fixture.RequestBoothRecoveryAdmissionAsync(buyer: true);

            Assert.True(recoveredBeforeOutput > 0);
            Assert.Equal("buyer", fixture.Harness.Sent[firstAdmissionFrame].Recipient);
            Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success,
                D13VendorTestHarness.BuyerCharacterUid, images.After.Buyer.Character),
                fixture.Harness.Sent[firstAdmissionFrame].Bytes);
            await fixture.RequestBoothRecoveryAdmissionAsync(buyer: false);
            fixture.SettlementSendObserver = null;
            if (recoveryObservationFailure is not null) throw recoveryObservationFailure;
            var sessions = fixture.ActualBoothRecoverySessions();
            Assert.True(sessions.TryGet(fixture.BuyerConnection, out var buyer), fixture.Log.ToString());
            Assert.True(sessions.TryGet(fixture.SellerConnection, out var seller), fixture.Log.ToString());
            AssertRecoveredBoothParticipant(images.After.Buyer, Assert.IsType<WorldSession>(buyer));
            AssertRecoveredBoothParticipant(images.After.Seller, Assert.IsType<WorldSession>(seller));
            fixture.AssertFiles(images.After.Files);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
            var buyerValues = JsonSerializer.Serialize(buyer);
            var sellerValues = JsonSerializer.Serialize(seller);
            var settledFrameCount = fixture.Harness.Sent.Count;

            // Listing books are live-only. A stale purchase replay after restart
            // cannot recreate the sold row, debit again, refund, or credit twice.
            await fixture.SendSettlementFrameAsync(fixture.BuyerConnection, BoothFixture.BuyRequest().ToFrameBytes());

            fixture.AssertFiles(images.After.Files);
            Assert.Equal(settledFrameCount, fixture.Harness.Sent.Count);
            Assert.True(sessions.TryGet(fixture.BuyerConnection, out var buyerAfterReplay));
            Assert.True(sessions.TryGet(fixture.SellerConnection, out var sellerAfterReplay));
            Assert.Same(buyer, buyerAfterReplay);
            Assert.Same(seller, sellerAfterReplay);
            Assert.Equal(buyerValues, JsonSerializer.Serialize(buyerAfterReplay));
            Assert.Equal(sellerValues, JsonSerializer.Serialize(sellerAfterReplay));
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
        });

    [Fact]
    public Task ActualFrameUnknownBoothWalItemImagePreservesAllEvidenceAndFencesBothAdmissions() =>
        RunBoothSettlementAsync(async fixture =>
        {
            var images = await fixture.PrepareRegisteredBoothRecoveryImagesAsync();
            fixture.PublishInterruptedBoothRecoveryImages(images, prefix: 2);
            var source = images.Before.Seller.PlayerItems;
            var foreign = source with
            {
                InventoryItems = source.InventoryItems.Select(row => row.WithDurability(4)).ToArray(),
            };
            new PlayerItemSnapshotLoader(fixture.Harness.PlayerItemsRoot)
                .SaveByCharacterUid(images.Before.Seller.Character.Uid, foreign);
            var foreignBytes = File.ReadAllBytes(fixture.ItemPath(images.Before.Seller));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(foreign), foreignBytes);
            var originalRelative = Path.GetRelativePath(fixture.Root, fixture.ItemPath(images.Before.Seller));
            Assert.False(foreignBytes.AsSpan().SequenceEqual(images.Before.Files[originalRelative]));
            Assert.False(foreignBytes.AsSpan().SequenceEqual(images.After.Files[originalRelative]));
            var evidence = fixture.ReadFiles(); // Includes the actual durable pending.json bytes.
            Assert.Contains(Path.Combine("Users", ".managed-trade-journal", "pending.json"), evidence.Keys);
            var sentBeforeAdmission = fixture.Harness.Sent.Count;
            var logBeforeAdmission = fixture.Log.ToString().Length;
            fixture.CreateBoothRecoveryFrameOwner();

            await fixture.RequestBoothRecoveryAdmissionAsync(buyer: true);
            await fixture.RequestBoothRecoveryAdmissionAsync(buyer: false);

            fixture.AssertFiles(evidence);
            Assert.Equal(sentBeforeAdmission, fixture.Harness.Sent.Count);
            Assert.True(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
            var sessions = fixture.ActualBoothRecoverySessions();
            Assert.False(sessions.TryGet(fixture.BuyerConnection, out _));
            Assert.False(sessions.TryGet(fixture.SellerConnection, out _));
            Assert.Empty(sessions.Snapshot());
            var log = fixture.Log.ToString()[logBeforeAdmission..];
            Assert.Contains("trade recovery blocked world admission/actions", log, StringComparison.Ordinal);
            Assert.Contains("tradeJournalRecoveryRequired", log, StringComparison.Ordinal);
            Assert.DoesNotContain("completed vendor purchase", log, StringComparison.Ordinal);
        });

    private static void AssertRecoveredBoothParticipant(WorldSession expected, WorldSession actual)
    {
        Assert.Equal(expected.AccountId, actual.AccountId);
        Assert.Equal(expected.Character.Uid, actual.Character.Uid);
        Assert.Equal(expected.Character.Money, actual.Character.Money);
        Assert.Equal(expected.Character.ConquerPoints, actual.Character.ConquerPoints);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems));
        Assert.Equal("preserved", actual.Character.Attributes["BoothClosureSentinel"]);
        Assert.False(actual.LegacyTradeActive);
        AssertNoBoothRecoveryClaims(actual);
        Assert.Null(actual.LegacyVendorListings);
    }
}
