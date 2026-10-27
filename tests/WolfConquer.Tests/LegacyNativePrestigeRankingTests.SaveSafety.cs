using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [InlineData("invalid-type")]
    [InlineData("null-points")]
    public void SaveInvalidTypeOrMissingPointsRefusesBeforeReplacingOwnedPriorBytes(string fault)
        => RunOwnedPrestigeSave(files =>
        {
            files.Seed(Text(FullRow));
            var row = Entry();
            Place(row);
            if (fault == "invalid-type")
                row.type = (Ranking.Type)260;
            else
                row.Points = null;
            AssertOwnedPrestigeSaveRefusal(files);
        });

    [Theory]
    [InlineData("zero-uid")]
    [InlineData("mismatched-uid")]
    [InlineData("name-slash")]
    [InlineData("name-newline")]
    [InlineData("guild-slash")]
    [InlineData("invalid-unicode")]
    public void SaveMalformedTailRefusesWithoutPublishingItsValidPrefix(string fault)
        => RunOwnedPrestigeSave(files =>
        {
            files.Seed(Text(FullRow));
            var rank = Place(Entry());
            var tail = Entry(19);
            rank.Items.Add(19, tail);
            switch (fault)
            {
                case "zero-uid": tail.UID = 0; break;
                case "mismatched-uid": tail.UID = 23; break;
                case "name-slash": tail.Name = "Bad/Name"; break;
                case "name-newline": tail.Name = "Bad\nName"; break;
                case "guild-slash": tail.GuildName = "Bad/Guild"; break;
                case "invalid-unicode": tail.Name = "Bad\ud800"; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            AssertOwnedPrestigeSaveRefusal(files);
        });

    [Theory]
    [InlineData("null-rank")]
    [InlineData("null-items")]
    [InlineData("bucket-mismatch")]
    [InlineData("duplicate-persisted-uid")]
    public void SaveInvalidCurrentClassOwnersRefuseBeforeReplacingOwnedPriorBytes(string fault)
        => RunOwnedPrestigeSave(files =>
        {
            files.Seed(Text(FullRow));
            var rank = Place(Entry());
            switch (fault)
            {
                case "null-rank": Ranking.Ranks[Ranking.Type.Ninja] = null!; break;
                case "null-items": rank.Items = null!; break;
                case "bucket-mismatch": rank._Type = Ranking.Type.World; break;
                case "duplicate-persisted-uid":
                    var duplicate = Entry();
                    duplicate.type = Ranking.Type.Trojan;
                    duplicate.Class = 1000;
                    Place(duplicate, Ranking.Type.Trojan);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            AssertOwnedPrestigeSaveRefusal(files);
        });

    [Fact]
    public void SavePersistsCurrentRowsWithoutRepairingTheStaleSafeDictionaryCache()
        => RunOwnedPrestigeSave(files =>
        {
            files.Seed(Text(FullRow));
            var old = Place(Entry());
            var cached = Ranking.Ranks.GetValues();
            var replacement = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 30 };
            var current = Entry(88);
            current.Name = "Current";
            replacement.Items.Add(current.UID, current);
            Ranking.Ranks[Ranking.Type.Ninja] = replacement;
            Assert.Same(cached, Ranking.Ranks.GetValues());

            Ranking.Save();

            files.Expect(Text("88/Current/4/505/206/707/30004/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/2/201/202/"));
            Assert.Same(cached, Ranking.Ranks.GetValues());
            Assert.Same(old, Assert.Single(cached));
            Assert.Same(replacement, Ranking.Ranks[Ranking.Type.Ninja]);
            Assert.Same(current, Assert.Single(replacement.Items).Value);
            Assert.Single(old.Items);
        });

    [Fact]
    public void SaveCachedValidOwnerCannotHideACorruptCurrentTail()
        => RunOwnedPrestigeSave(files =>
        {
            files.Seed(Text(FullRow));
            var old = Place(Entry());
            var cached = Ranking.Ranks.GetValues();
            var replacement = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 30 };
            replacement.Items.Add(19, Entry(19));
            replacement.Items.Add(23, null!);
            Ranking.Ranks[Ranking.Type.Ninja] = replacement;
            Assert.Same(cached, Ranking.Ranks.GetValues());

            AssertOwnedPrestigeSaveRefusal(files);

            Assert.Same(cached, Ranking.Ranks.GetValues());
            Assert.Same(old, Assert.Single(cached));
            Assert.Single(old.Items);
        });
}
