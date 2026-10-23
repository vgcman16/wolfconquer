using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryQuestCaptureTests
{
    [Theory]
    [InlineData("nullSnapshot")]
    [InlineData("nullQuests")]
    [InlineData("nullRow")]
    [InlineData("zeroUid")]
    [InlineData("duplicateUid")]
    [InlineData("undefinedStatus")]
    [InlineData("nullIntentions")]
    [InlineData("staleAccepted")]
    [InlineData("acceptedForeign")]
    public void CaptureRejectsMalformedWholeBookAndCachedAcceptedIndex(string fault)
    {
        var rows = new[]
        {
            new PlayerQuestSnapshotEntry(11, PlayerQuestStatus.Accepted, 7, new uint[] { 100 }),
            new PlayerQuestSnapshotEntry(12, PlayerQuestStatus.Finished, 8, Array.Empty<uint>()),
        };
        PlayerQuestSnapshot snapshot = new(rows);
        switch (fault)
        {
            case "nullSnapshot": snapshot = null!; break;
            case "nullQuests": snapshot = snapshot with { Quests = null! }; break;
            case "nullRow": rows[1] = null!; break;
            case "zeroUid": rows[1] = rows[1] with { Uid = 0 }; break;
            case "duplicateUid": rows[1] = rows[1] with { Uid = 11 }; break;
            case "undefinedStatus": rows[1] = rows[1] with { Status = (PlayerQuestStatus)3 }; break;
            case "nullIntentions": rows[1] = rows[1] with { Intentions = null! }; break;
            case "staleAccepted": rows[0] = rows[0] with { Status = PlayerQuestStatus.Finished }; break;
            case "acceptedForeign": ((PlayerQuestSnapshotEntry[])snapshot.AcceptedQuests)[0] = rows[0] with { }; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Assert.False(LegacyVictoryQuestCapture.TryCapture(snapshot, out var capture, out var reason));
        Assert.Null(capture);
        Assert.StartsWith("victoryQuest", reason);
    }

    [Theory]
    [InlineData("rootRecord")]
    [InlineData("listCount")]
    [InlineData("rowEquivalent")]
    [InlineData("rowOrder")]
    [InlineData("intentionValue")]
    [InlineData("intentionListCount")]
    [InlineData("acceptedAlias")]
    public void CapturedPhysicalReferencesAndValuesMustRemainCurrent(string change)
    {
        var intentions = new List<uint> { 100, 2 };
        var rows = new List<PlayerQuestSnapshotEntry>
        {
            new(11, PlayerQuestStatus.Accepted, 7, intentions),
            new(12, PlayerQuestStatus.Finished, 8, Array.Empty<uint>()),
        };
        var snapshot = new PlayerQuestSnapshot(rows);
        var before = PlayerQuestSnapshotLoader.Serialize(snapshot);
        Assert.True(LegacyVictoryQuestCapture.TryCapture(snapshot, out var capture, out var reason), reason);
        Assert.True(capture.IsCurrent(snapshot));
        var current = snapshot;
        switch (change)
        {
            case "rootRecord": current = snapshot with { }; break;
            case "listCount": rows.Add(new(13, PlayerQuestStatus.Available, 0, Array.Empty<uint>())); break;
            case "rowEquivalent": rows[0] = rows[0] with { }; break;
            case "rowOrder": (rows[0], rows[1]) = (rows[1], rows[0]); break;
            case "intentionValue": intentions[0]++; break;
            case "intentionListCount": intentions.Add(9); break;
            case "acceptedAlias": ((PlayerQuestSnapshotEntry[])snapshot.AcceptedQuests)[0] = rows[0] with { }; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.False(capture.IsCurrent(current));
        Assert.Equal(before, capture.CopyImage());
    }

    [Fact]
    public void ReturnedImageCopiesCannotChangeCapturedBaseline()
    {
        var snapshot = PlayerQuestSnapshotLoader.Parse(Convert.FromHexString("00000000"));
        Assert.True(LegacyVictoryQuestCapture.TryCapture(snapshot, out var capture, out var reason), reason);
        var first = capture.CopyImage();
        first[0] = 1;
        Assert.False(capture.MatchesImage(first));
        Assert.True(capture.MatchesImage(new byte[4]));
        Assert.Equal(new byte[4], capture.CopyImage());
        Assert.True(capture.IsCurrent(snapshot));
    }
}
