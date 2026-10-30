using System.Globalization;
using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupVendorRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupVendorRecoveryTests
{
    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown-uid")]
    [InlineData("malformed-tail")]
    public Task UnrelatedNamespaceItemsCannotBorrowTheCapturedVendorParticipantOverlay(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync();
        var uid = fault == "duplicate" ? Fixture.SoldUid : fault == "unknown-uid"
            ? LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 : Fixture.SoldUid + 1_000;
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(new([NewItem(uid)], [], [], [], []));
        if (fault == "malformed-tail") raw = raw.Concat(new byte[] { 0x71 }).ToArray();
        File.WriteAllBytes(Path.Combine(f.Data.Items, (Fixture.BuyerUid + 1).ToString(CultureInfo.InvariantCulture) + ".bin"), raw);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("foreign-character")]
    [InlineData("missing-items")]
    public Task AForeignOrMissingCurrentVendorParticipantCannotBeOverwrittenByTheRecordedIntent(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        if (fault == "missing-items") File.Delete(f.ItemsPath(Fixture.SellerUid));
        else File.WriteAllBytes(f.CharacterPath(Fixture.BuyerUid), Fixture.Replace(File.ReadAllBytes(f.CharacterPath(Fixture.BuyerUid)),
            new Dictionary<string, string> { ["Money=2000\r\n"] = "Money=1999\r\n" }));
        f.AssertRefused();
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task EveryKnownVendorStageRequiresItsOwnCapturedAfterImagePrefix(int ordinal) => WithFixture(async f =>
    {
        await f.StopAfterStageFlushedAsync(4); await f.CloseAsync();
        var path = f.StagePath(f.OrderedTargets[ordinal]); var bytes = File.ReadAllBytes(path); bytes[0] ^= 0xff;
        File.WriteAllBytes(path, bytes); f.AssertRefused();
    });

    [Theory]
    [InlineData(0)] [InlineData(3)]
    public Task ForeignVendorStageTransactionNamesRemainUntouchedEvidence(int ordinal) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        var target = f.OrderedTargets[ordinal];
        File.WriteAllBytes(target + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", f.After[target]);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("domain")] [InlineData("evidence")] [InlineData("items-root")]
    public Task VendorRecoveryCannotSubstituteAnotherConfiguredNamespaceIdentity(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync(); var configured = f.Data.Options();
        var changed = fault switch
        {
            "domain" => configured with { ExpectedDomain = Guid.NewGuid() },
            "evidence" => configured with { HistoricalEvidence = configured.HistoricalEvidence + " changed" },
            "items-root" => configured with { ItemsRoot = Path.Combine(f.Data.Root, "ForeignItems") },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        f.AssertRefused(changed);
    });

    [Fact]
    public Task ActualExclusiveLeaseIsRequiredBeforeAnyVendorRecoveryWrite() => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync();
        using (new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            f.AssertRefused(leaseAvailable: false);
        f.AssertLeaseAvailable(); f.Open(); f.AssertRecoveredImages();
    });

    [Fact]
    public Task GenuineRetainedProducerIntentIsNotAssumedCompletedByTheVendorSettlement() => WithFixture(async f =>
    {
        var roots = f.Data.Options().Roots;
        var inputs = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Fixture.BuyerUid);
        var hashes = inputs.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : SHA256.HashData(pair.Value));
        var recipe = new LegacyOrdinaryAdmissionRecipe(Fixture.BuyerUid, roots, hashes,
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Character]!), SHA256.HashData(inputs[LegacyTradeJournalFileKind.Spells]!),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Proficiencies]!), [new(Fixture.OrdinaryType, 3, 10, 10)]);
        var payload = recipe.Encode(); Assert.Equal(payload, LegacyOrdinaryAdmissionRecipe.Decode(payload, roots).Encode());
        var digest = SHA256.HashData(payload);
        var request = new LegacyItemUidReservationRequest(new Guid(digest.AsSpan(0, 16)), LegacyOrdinaryAdmissionUidOwner.Writer,
            Fixture.BuyerUid, Fixture.BuyerUid, Convert.ToHexString(digest).ToLowerInvariant(), 1);
        Assert.True(f.Runtime.Ordinary.Registry.TryPrepareProducerIntent(f.Data.ExpectedDomain, request, payload,
            out var prepared, out var reason), reason);
        Assert.Null(Assert.IsType<LegacyItemUidProducerIntentSnapshot>(prepared).Allocation);
        f.RegistryState = File.ReadAllBytes(f.Data.State);
        await f.StageAsync(0); await f.CloseAsync(); f.AssertRefused();
    });
}
