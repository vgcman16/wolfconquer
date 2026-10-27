using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public Task OrdinaryRankMemberShowProjectsRealSavedWorldChampionAndPreservesSourceWire() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var owner = fixture.Current;
            var files = fixture.SavedFiles();
            await fixture.Query(ChampionRequest(entityUid: uint.MaxValue));
            var bytes = Assert.Single(fixture.Sent);
            // Independent source-schema golden: opcode2054, outer Show0,
            // item Type1/Rank1, all appearance tags and required zero20/21.
            // This is a plain C# source contract assertion, not D13 live proof.
            Assert.Equal(Convert.FromHexString(
                "410006080800123908011001182922044865726F2A054775696C64300738084009480A500B580C600D680E700F7810800111880112900113980114A00100A80100"), bytes);
            Assert.True(LegacyRankMemberShowPacket.TryParse(ReadFrame(bytes), out var response));
            Assert.Equal(0U, response.Type);
            Assert.Equal(new LegacyRankMemberShowEntry(
                Type: 1, Rank: 1, EntityUid: 41, Name: "Hero", GuildName: "Guild", Mesh: 7,
                HairStyle: 8, Head: 9, Garment: 10, LeftWeapon: 11, LeftWeaponAccessory: 12,
                RightWeapon: 13, RightWeaponAccessory: 14, MountArmor: 15, Armor: 16,
                Wing: 17, WingPlus: 18, Title: 19, Flag: 20, Profession: 0, WeaponWar: 0),
                Assert.Single(response.Items));
            Assert.Same(owner, fixture.Current);
            fixture.AssertFilesUnchanged(files);
            Assert.Contains("validated prestige snapshot", fixture.Log.ToString());
        });

    [Fact]
    public Task OrdinaryRankMemberShowSelectsHighestTotalWithStableTieAndIgnoresUnrelatedRequestItems() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var request = new LegacyRankMemberShowPacket(0,
            [
                LegacyRankMemberShowEntry.Empty with { Type = 1, Name = "Forged", Rank = 99, EntityUid = 999 },
                LegacyRankMemberShowEntry.Empty with { Type = 42, EntityUid = 23 },
            ]).ToFrameBytes();
            await fixture.Query(request);
            Assert.True(LegacyRankMemberShowPacket.TryParse(ReadFrame(Assert.Single(fixture.Sent)), out var response));
            var champion = Assert.Single(response.Items);
            Assert.Equal(17U, champion.EntityUid);
            Assert.Equal("FirstTie", champion.Name);
            Assert.Equal(1U, champion.Rank);
            Assert.Equal(1U, champion.Type);
        },
        [
            PrestigeRankingQueryTestData.Row(11, 20_000, name: "Lower"),
            PrestigeRankingQueryTestData.Row(17, 40_000, name: "FirstTie"),
            PrestigeRankingQueryTestData.Row(19, 40_000, name: "SecondTie"),
        ]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OrdinaryRankMemberShowDistinguishesValidEmptyAndIneligibleSnapshotsFromCorruption(bool ineligible) =>
        RunPrestigeQueryAsync(async fixture =>
        {
            await fixture.Query();
            // Existing managed empty compatibility frame, with a valid catalog.
            Assert.Equal(Convert.FromHexString("060006080800"), Assert.Single(fixture.Sent));
            Assert.Contains("validated prestige snapshot", fixture.Log.ToString());
        }, ineligible ? [PrestigeRankingQueryTestData.Row(total: 19_999)] : []);

    [Fact]
    public Task OrdinaryRankMemberShowPreservesExactUtf8ForNonAsciiAndSupplementaryScalarText() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            await fixture.Query();
            Assert.Equal(Convert.FromHexString(
                "470006080800123F080110011829220948C3A9726FF09F90892A06E99BAAE9BE8D300738084009480A500B580C600D680E700F7810800111880112900113980114A00100A80100"),
                Assert.Single(fixture.Sent));
        }, [PrestigeRankingQueryTestData.Row(name: "H\u00E9ro\U0001F409", guild: "\u96EA\u9F8D")]);
}
