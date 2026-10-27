using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Theory]
    [InlineData("missing-family")]
    [InlineData("null-rank")]
    [InlineData("wrong-owner-type")]
    [InlineData("null-map")]
    [InlineData("null-row")]
    [InlineData("wrong-key")]
    [InlineData("wrong-row-category")]
    [InlineData("zero-uid")]
    [InlineData("zero-cap")]
    [InlineData("unsorted")]
    [InlineData("over-cap")]
    public void InvalidCompleteSaveSnapshotCannotTouchThePriorFileOrAnyEscapedOwner(string invalid) =>
        WithOwnedRankPersistence(files =>
        {
            files.Seed("owned prior bytes");
            _ = SeedPersistenceGraph();
            var overall = Ranking.Ranks[Ranking.Type.Overall];
            switch (invalid)
            {
                case "missing-family": Ranking.Ranks.Remove(Ranking.Type.Wind); break;
                case "null-rank": Ranking.Ranks[Ranking.Type.Wind] = null!; break;
                case "wrong-owner-type": Ranking.Ranks[Ranking.Type.Wind]._Type = Ranking.Type.Fire; break;
                case "null-map": Ranking.Ranks[Ranking.Type.Wind].RankingList = null!; break;
                case "null-row": overall.RankingList.Add(900, null!); break;
                case "wrong-key": AddSaveRow(overall, 900, 901, 1); break;
                case "wrong-row-category": overall.RankingList[500].Type = Ranking.Type.Water; break;
                case "zero-uid": AddSaveRow(overall, 0, 0, 1); break;
                case "zero-cap": overall.MaxItems = 0; break;
                case "unsorted": AddSaveRow(overall, 900, 900, 3000); break;
                case "over-cap":
                    for (uint index = 0; index < 10; index++) AddSaveRow(overall, 900 + index, 900 + index, 1900 - index);
                    break;
            }
            var before = new PersistenceGraphImage();
            var error = Record.Exception(Ranking.Save);
            before.RequireCurrent();
            files.Expect("owned prior bytes");
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    [InlineData(false, "slash")]
    [InlineData(true, "slash")]
    [InlineData(false, "control")]
    [InlineData(true, "control")]
    public void NullOrAmbiguousSaveTextCannotExecuteTheQueuedValidPrefix(bool guild, string invalid) =>
        WithOwnedRankPersistence(files =>
        {
            files.Seed("owned prior bytes");
            _ = SeedPersistenceGraph();
            var rank = Ranking.Ranks[Ranking.Type.Water];
            var tail = AddSaveRow(rank, 900, 900, 1);
            var text = invalid switch { "null" => null!, "slash" => "bad/text", _ => "bad\0text" };
            if (guild) tail.GuildName = text; else tail.Name = text;
            var before = new PersistenceGraphImage();
            var error = Record.Exception(Ranking.Save);
            before.RequireCurrent();
            files.Expect("owned prior bytes");
            Assert.IsType<InvalidOperationException>(error);
        });

    private static Ranking.Entry AddSaveRow(Ranking.Rank rank, uint key, uint uid, uint points)
    {
        var row = Entry(uid, points); row.Type = rank._Type;
        rank.RankingList.Add(key, row);
        return row;
    }
}
