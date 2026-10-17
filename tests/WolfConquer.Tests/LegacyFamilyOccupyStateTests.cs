using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFamilyOccupyStateTests
{
    [Fact]
    public void CreateShowInformationUsesWinnerForTournamentNpc()
    {
        using var fixture = CreateFixture(
            "Twin",
            "148/WalkingDead/0/722454/1/722454/17/Gipsy/0/722454/9/722454/");

        var information = LegacyFamilyOccupyState.CreateShowInformation(fixture.UsersRoot, 101888);

        Assert.Equal(148U, information.ClanId);
        Assert.Equal(101888U, information.NpcUid);
        Assert.Equal("WalkingDead", information.ClanName);
        Assert.Equal("Twin", information.MapName);
        Assert.False(information.Alive);
        Assert.Equal(1U, information.OccupationDays);
        Assert.Equal(722454U, information.Reward);
        Assert.Equal(722454U, information.NextReward);
    }

    [Fact]
    public void CreateShowInformationUsesBestWinnerForInformationNpc()
    {
        using var fixture = CreateFixture(
            "Twin",
            "148/WalkingDead/0/722454/1/722454/17/Gipsy/0/722454/9/722454/");

        var information = LegacyFamilyOccupyState.CreateShowInformation(fixture.UsersRoot, 101905);

        Assert.Equal(17U, information.ClanId);
        Assert.Equal(101905U, information.NpcUid);
        Assert.Equal("Gipsy", information.ClanName);
        Assert.Equal("Twin", information.MapName);
        Assert.Equal(9U, information.OccupationDays);
        Assert.Equal(722454U, information.Reward);
        Assert.Equal(722454U, information.NextReward);
    }

    [Fact]
    public void CreateShowInformationDefaultsKnownCityWhenFileMissing()
    {
        using var fixture = CreateEmptyFixture();

        var information = LegacyFamilyOccupyState.CreateShowInformation(fixture.UsersRoot, 101915);

        Assert.Equal(0U, information.ClanId);
        Assert.Equal(101915U, information.NpcUid);
        Assert.Equal("None", information.ClanName);
        Assert.Equal("Bird", information.MapName);
        Assert.Equal(0U, information.OccupationDays);
        Assert.Equal(0U, information.Reward);
        Assert.Equal(0U, information.NextReward);
    }

    private static Fixture CreateFixture(string city, string row)
    {
        var fixture = CreateEmptyFixture();
        var clanWarRoot = Path.Combine(fixture.DbRoot, "ClanWar");
        Directory.CreateDirectory(clanWarRoot);
        File.WriteAllText(Path.Combine(clanWarRoot, city + ".txt"), "Count=1" + Environment.NewLine + row);
        return fixture;
    }

    private static Fixture CreateEmptyFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolfconquer-family-occupy-" + Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(root, "Users");
        Directory.CreateDirectory(usersRoot);
        return new Fixture(root, usersRoot);
    }

    private sealed class Fixture(string dbRoot, string usersRoot) : IDisposable
    {
        public string DbRoot { get; } = dbRoot;

        public string UsersRoot { get; } = usersRoot;

        public void Dispose()
        {
            try
            {
                Directory.Delete(DbRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
