using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryQuestCaptureTests
{
    [Theory]
    [InlineData(PlayerQuestStatus.Accepted, false)]
    [InlineData(PlayerQuestStatus.Accepted, true)]
    [InlineData(PlayerQuestStatus.Finished, false)]
    [InlineData(PlayerQuestStatus.Finished, true)]
    [InlineData(PlayerQuestStatus.Available, false)]
    [InlineData(PlayerQuestStatus.Available, true)]
    public void EveryDefinedStatusRetainsZeroOrFullWidthIntentions(PlayerQuestStatus status, bool nonempty)
    {
        var original = new PlayerQuestSnapshot(new[]
        {
            new PlayerQuestSnapshotEntry(11, status, uint.MaxValue,
                nonempty ? new uint[] { 0, uint.MaxValue } : Array.Empty<uint>()),
        });
        AssertImageRoundTrip(PlayerQuestSnapshotLoader.Serialize(original));
    }

    [Fact]
    public void EmptyBookRetainsItsExactFourByteImage()
    {
        Assert.True(LegacyVictoryQuestCapture.TryCapture(PlayerQuestSnapshot.Empty, out var capture, out var reason), reason);
        Assert.Equal(new byte[4], capture.CopyImage());
        AssertImageRoundTrip(new byte[4]);
    }

    [Fact]
    public void MixedBookPreservesUnsortedOrderAllFieldsAndAcceptedAliases()
    {
        var image = Convert.FromHexString(
            "03000000" +
            "680F000000000000FFFFFFFF0200000064000000FFFFFFFF" +
            "07000000010000000000000000000000" +
            "09000000020000000000000000000000");
        Assert.True(LegacyVictoryQuestCapture.TryParseImage(image, out var parsed, out var reason), reason);
        Assert.Equal(new uint[] { 3944, 7, 9 }, parsed.Quests.Select(row => row.Uid));
        Assert.Same(parsed.Quests[0], Assert.Single(parsed.AcceptedQuests));
        Assert.Equal(uint.MaxValue, parsed.Quests[0].Time);
        Assert.Equal(new uint[] { 100, uint.MaxValue }, parsed.Quests[0].Intentions);
        AssertImageRoundTrip(image);
    }

    [Fact]
    public void ActualReviewQuestProducerCollectionExpressionRemainsCapturable()
    {
        var snapshot = LegacyQuestListPacket.EnsureLegacyReviewStarterQuest(
            PlayerQuestSnapshot.Empty, 10, out var uid, out var added);
        Assert.True(added);
        Assert.Equal(uid, Assert.Single(snapshot.Quests).Uid);
        Assert.True(LegacyVictoryQuestCapture.TryCapture(snapshot, out var capture, out var reason), reason);
        Assert.True(capture.IsCurrent(snapshot));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(snapshot), capture.CopyImage());
    }

    [Theory]
    [InlineData("short")]
    [InlineData("negativeCount")]
    [InlineData("hugeCount")]
    [InlineData("zeroUid")]
    [InlineData("duplicateUid")]
    [InlineData("undefinedStatus")]
    [InlineData("negativeIntentions")]
    [InlineData("hugeIntentions")]
    [InlineData("truncatedHeader")]
    [InlineData("truncatedIntentions")]
    [InlineData("trailingByte")]
    [InlineData("zeroCountTail")]
    [InlineData("invalidTailUid")]
    [InlineData("invalidTailStatus")]
    [InlineData("invalidTailIntentions")]
    public void CompleteRawWalkRejectsMalformedCountsRowsAndTail(string fault)
    {
        var image = InvalidImage(fault);
        var before = (byte[])image.Clone();
        Assert.False(LegacyVictoryQuestCapture.TryParseImage(image, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.StartsWith("victoryQuest", reason);
        Assert.Equal(before, image);
    }

    [Fact]
    public void OversizedImageRefusesWithoutPublishingASnapshot()
    {
        var image = new byte[LegacyVictoryQuestCapture.MaximumImageBytes + 1];
        Assert.False(LegacyVictoryQuestCapture.TryParseImage(image, out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("victoryQuestImageLengthInvalid", reason);
    }

    private static void AssertImageRoundTrip(byte[] image)
    {
        Assert.True(LegacyVictoryQuestCapture.TryParseImage(image, out var parsed, out var reason), reason);
        Assert.True(LegacyVictoryQuestCapture.TryCapture(parsed, out var capture, out reason), reason);
        Assert.True(capture.IsCurrent(parsed));
        Assert.True(capture.MatchesImage(image));
        Assert.Equal(image, capture.CopyImage());
        Assert.Equal(image, PlayerQuestSnapshotLoader.Serialize(parsed));
    }

    private static byte[] InvalidImage(string fault)
    {
        var image = PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshot(new[]
        {
            new PlayerQuestSnapshotEntry(3944, PlayerQuestStatus.Accepted, 7, new uint[] { 100, 3 }),
            new PlayerQuestSnapshotEntry(5555, PlayerQuestStatus.Finished, 8, Array.Empty<uint>()),
        }));
        switch (fault)
        {
            case "short": return image[..3];
            case "negativeCount": PutInt(image, 0, -1); break;
            case "hugeCount":
                var countOnly = new byte[sizeof(int)];
                PutInt(countOnly, 0, int.MaxValue);
                return countOnly;
            case "zeroUid": PutInt(image, 4, 0); break;
            case "duplicateUid": PutInt(image, 28, 3944); break;
            case "undefinedStatus": PutInt(image, 8, 3); break;
            case "negativeIntentions": PutInt(image, 16, -1); break;
            case "hugeIntentions": PutInt(image, 16, int.MaxValue); break;
            case "truncatedHeader": return image[..^1];
            case "truncatedIntentions": PutInt(image, 0, 1); return image[..27];
            case "trailingByte": return image.Concat(new byte[] { 1 }).ToArray();
            case "zeroCountTail": PutInt(image, 0, 0); break;
            case "invalidTailUid": PutInt(image, 28, 0); break;
            case "invalidTailStatus": PutInt(image, 32, -1); break;
            case "invalidTailIntentions": PutInt(image, 40, 1); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        return image;
    }

    private static void PutInt(byte[] image, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(offset, sizeof(int)), value);
}
