using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("live-active-without-party", false)]
    [InlineData("live-and-disk-orphan-claim", true)]
    [InlineData("malformed-live-claim-tail", false)]
    public Task RegisteredPhysicalSashIncoherentTradeOrClaimCannotMoveOrPublish(string fault, bool withdraw) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var healthy = fixture.Current;
        Assert.False(healthy.LegacyTradeActive);
        Assert.False(fixture.Trades.TryGet(fixture.Connection, out _));
        Assert.True(LegacyTradeEscrowClaims.TryRead(healthy.Character.Attributes, out var absent, out var reason), reason);
        Assert.Null(absent);

        switch (fault)
        {
            case "live-active-without-party":
                // An inconsistent public live flag cannot stand in for the real party owner.
                fixture.Sessions.Set(fixture.Connection, healthy with { LegacyTradeActive = true });
                break;
            case "live-and-disk-orphan-claim":
                var attributes = new Dictionary<string, string>(healthy.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["ConquerPoints"] = "125" };
                var claim = new LegacyTradeEscrowClaim(Guid.Parse("5a34151b-31e4-44d7-a95e-973ce85d117b"), 0, 25);
                var pending = LegacyTradeEscrowClaims.WithClaim(healthy with
                { Character = healthy.Character with { ConquerPoints = 125, Attributes = attributes } }, claim);
                // Real isolated debit + durable claim AFTER completed startup recovery.
                // Do not invent a party or reinitialize/refund the pending fixture.
                Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(healthy, pending, out reason), reason);
                fixture.Sessions.Set(fixture.Connection, pending);
                var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
                Assert.Equal(125, saved.ConquerPoints);
                Assert.True(LegacyTradeEscrowClaims.TryRead(saved.Attributes, out var diskClaim, out reason), reason);
                Assert.Equal(claim, diskClaim);
                Assert.True(LegacyTradeEscrowClaims.TryRead(fixture.Current.Character.Attributes, out var liveClaim, out reason), reason);
                Assert.Equal(claim, liveClaim);
                Assert.False(fixture.Current.LegacyTradeActive);
                break;
            case "malformed-live-claim-tail":
                // Append a malformed complete claim after genuine parsed birth.
                // The owned disk image remains healthy; no serializer or saver is replaced.
                var malformed = new Dictionary<string, string>(healthy.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyTradeEscrowClaims.IdKey] = "not-a-canonical-trade-id",
                    [LegacyTradeEscrowClaims.MoneyKey] = "25",
                    [LegacyTradeEscrowClaims.CpKey] = "0",
                };
                fixture.Sessions.Set(fixture.Connection, healthy with
                { Character = healthy.Character with { Attributes = malformed } });
                Assert.False(LegacyTradeEscrowClaims.TryRead(fixture.Current.Character.Attributes, out _, out _));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Assert.False(fixture.Trades.TryGet(fixture.Connection, out _));
        Assert.False(fixture.Trades.Contains(fixture.Connection));
        var before = fixture.Capture();

        await fixture.InvokeTransferAsync(withdraw);

        fixture.AssertUnchanged(before);
        Assert.False(fixture.Trades.TryGet(fixture.Connection, out _));
    });
}
