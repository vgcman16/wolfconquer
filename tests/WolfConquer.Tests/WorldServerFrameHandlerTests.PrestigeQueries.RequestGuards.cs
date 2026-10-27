using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("smaller")]
    [InlineData("larger")]
    public Task OrdinaryRankMemberShowRefusesAbsentOrInconsistentDeclaredPrestigeCount(string corruption) =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var counts = Assert.IsType<Dictionary<string, int>>(fixture.WorldData.Manifest.Counts);
            Assert.Equal(1, counts["prestigeRanks"]);
            switch (corruption)
            {
                case "missing": counts.Remove("prestigeRanks"); break;
                case "negative": counts["prestigeRanks"] = -1; break;
                case "smaller": counts["prestigeRanks"] = 0; break;
                case "larger": counts["prestigeRanks"] = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            var owner = fixture.Current;
            var files = fixture.SavedFiles();
            await fixture.Query();
            Assert.Empty(fixture.Sent);
            Assert.Same(owner, fixture.Current);
            fixture.AssertFilesUnchanged(files);
            Assert.Contains("required prestige catalog count is missing or inconsistent", fixture.Log.ToString());
        });

    [Fact]
    public Task OrdinaryRankMemberShowUnsupportedFamiliesRetainExplicitEmptyCompatibilityAndNeverBorrowChampion() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            foreach (var query in new[]
            {
                new LegacyRankMemberShowPacket(0, []),
                new LegacyRankMemberShowPacket(0, [LegacyRankMemberShowEntry.Empty with { Type = 2 }]),
                new LegacyRankMemberShowPacket(0,
                    [LegacyRankMemberShowEntry.Empty with { Type = 2 }, LegacyRankMemberShowEntry.Empty with { Type = 1 }]),
                new LegacyRankMemberShowPacket(1, [LegacyRankMemberShowEntry.Empty with { Type = 1, EntityUid = 41 }]),
                new LegacyRankMemberShowPacket(uint.MaxValue, [LegacyRankMemberShowEntry.Empty with { Type = 1 }]),
            })
            {
                fixture.Sent.Clear();
                await fixture.Query(query.ToFrameBytes(), fixture.Context with { WorldData = null });
                Assert.True(LegacyRankMemberShowPacket.TryParse(ReadFrame(Assert.Single(fixture.Sent)), out var response));
                Assert.Equal(query.Type, response.Type);
                if (query.Type == 1)
                    Assert.Equal(LegacyRankMemberShowEntry.Empty, Assert.Single(response.Items));
                else Assert.Empty(response.Items);
            }
            Assert.Contains("requested family is not modeled", fixture.Log.ToString());
        });
}
