using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualD13FrameMailListingUsesLiteralTitleNineAndSenderTen() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true);
        var before = fixture.CaptureTransferFrame();
        var owner = fixture.ReadEnteredMailboxPackageSession();
        var ownerJson = JsonSerializer.Serialize(owner);
        var first = fixture.Sent.Count;

        await fixture.FrameAsync(D13MailLiteralListRequest());

        var frame = Assert.Single(fixture.Sent.Skip(first));
        var rows = D13MailLiteralRows(frame, expectedType: 0, expectedCount: 1);
        Assert.Equal(2, rows.Length);
        Assert.Equal(21UL, D13MailLiteralScalar(rows[0], 1));
        Assert.Equal(22UL, D13MailLiteralScalar(rows[1], 1));
        AssertD13MailLiteralText(rows[0], "Atomic attachment", "System sentinel");
        AssertD13MailLiteralText(rows[1], "Unselected sentinel", "Other sender");
        Assert.Equal(AtomicMailMoney, D13MailLiteralScalar(rows[0], 2));
        Assert.Equal((ulong)AtomicMailCp, D13MailLiteralScalar(rows[0], 3));
        Assert.Equal((ulong)AtomicMailItemUid, D13MailLiteralScalar(rows[0], 5));
        fixture.AssertFiles(before.Files);
        Assert.Same(owner, fixture.ReadEnteredMailboxPackageSession());
        Assert.Equal(ownerJson, JsonSerializer.Serialize(owner));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13ClaimAllRefreshUsesLiteralTextTagsAfterCommittedFramesAndOnReplay(bool mixed) =>
        RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: mixed);
        var before = fixture.CaptureTransferFrame();
        var expectedFiles = ExpectedAtomicMailFiles(fixture, before, mixed);
        var observed = 0;
        fixture.SendObserver = _ => { fixture.AssertFiles(expectedFiles); observed++; };

        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll: true));

        fixture.SendObserver = null;
        Assert.Equal(mixed ? 7 : 3, observed);
        AssertAtomicMailCommitted(fixture, before, mixed);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        var prefix = new List<byte[]>();
        if (mixed)
        {
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{AtomicMailMoney}@@").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.Money, checked((ulong)before.Owner.Character.Money + AtomicMailMoney))]).ToFrameBytes());
            prefix.Add(LegacyMsgMessagePacket.SystemTalk($"You received {AtomicMailCp} ConquerPoints.").ToFrameBytes());
            prefix.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)before.Owner.Character.ConquerPoints + AtomicMailCp))]).ToFrameBytes());
        }
        prefix.Add(AtomicMailItemFrame(before));
        Assert.Equal(prefix.Count + 2, frames.Length);
        for (var index = 0; index < prefix.Count; index++) Assert.Equal(prefix[index], frames[index]);
        AssertD13MailLiteralClaimRefresh(frames[^2]);
        Assert.Equal(Fixture.AtomicMailRequest(claimAll: true), frames[^1]);

        var replayFirst = fixture.Sent.Count;
        await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll: true));

        AssertAtomicMailCommitted(fixture, before, mixed);
        var replay = fixture.Sent.Skip(replayFirst).ToArray();
        Assert.Equal(2, replay.Length);
        AssertD13MailLiteralClaimRefresh(replay[0]);
        Assert.Equal(Fixture.AtomicMailRequest(claimAll: true), replay[1]);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13MailTextTagsDoNotTurnFullBagRefusalIntoRefreshOrAcknowledgement(bool claimAll) =>
        RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(inventoryCount: 40, mixed: true, flag: 0);
        var before = CaptureMailboxFullBag(fixture);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var first = fixture.Sent.Count;
            var logStart = fixture.Log.GetStringBuilder().Length;
            fixture.SendObserver = _ => AssertMailboxFullBagState(fixture, before);

            await fixture.FrameAsync(Fixture.AtomicMailRequest(claimAll));

            fixture.SendObserver = null;
            AssertMailboxFullBagState(fixture, before);
            AssertMailboxFullBagOnlyError(fixture, first, logStart);
        }
        var listingFirst = fixture.Sent.Count;

        await fixture.FrameAsync(D13MailLiteralListRequest());

        var rows = D13MailLiteralRows(Assert.Single(fixture.Sent.Skip(listingFirst)), expectedType: 0, expectedCount: 1);
        var retained = Assert.Single(rows, row => D13MailLiteralScalar(row, 1) == AtomicMailId);
        AssertD13MailLiteralText(retained, "Atomic attachment", "System sentinel");
        Assert.Equal(AtomicMailMoney, D13MailLiteralScalar(retained, 2));
        Assert.Equal((ulong)AtomicMailCp, D13MailLiteralScalar(retained, 3));
        Assert.Equal((ulong)AtomicMailItemUid, D13MailLiteralScalar(retained, 5));
        fixture.AssertFiles(before.Disk.Files);
        Assert.Same(before.Owner, fixture.ReadEnteredMailboxPackageSession());
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
    });

    private static byte[] D13MailLiteralListRequest() =>
        new LegacyMailListPacket(LegacyMailListType.FullPage, Count: 1, Mails: []).ToFrameBytes();

    private static void AssertD13MailLiteralClaimRefresh(byte[] frame)
    {
        var row = Assert.Single(D13MailLiteralRows(frame, expectedType: 1, expectedCount: 1));
        AssertD13MailLiteralText(row, "Atomic attachment", "System sentinel");
        Assert.Equal((ulong)AtomicMailId, D13MailLiteralScalar(row, 1));
        Assert.Equal(0UL, D13MailLiteralScalar(row, 2));
        Assert.Equal(0UL, D13MailLiteralScalar(row, 3));
        Assert.Equal(0UL, D13MailLiteralScalar(row, 5));
        Assert.Equal(19UL, D13MailLiteralScalar(row, 6));
        Assert.Equal(0UL, D13MailLiteralScalar(row, 7));
    }
}
