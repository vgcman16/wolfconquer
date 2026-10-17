using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanRelationStateTests
{
    [Fact]
    public void TryReadParsesClanCoreAndRelationLists()
    {
        using var fixture = CreateFixture(
            """
            Count=3
            113/FixtureClan/FixtureLeader/0/820500000/None/0/0/0/0/
            2/90/91/
            1/92/
            """);

        Assert.True(LegacyClanRelationState.TryRead(fixture.Path, out var state));

        Assert.Equal(113U, state.ClanId);
        Assert.Equal("FixtureClan", state.Name);
        Assert.Equal("FixtureLeader", state.LeaderName);
        Assert.Equal((byte)0, state.Level);
        Assert.Equal(820500000U, state.Donation);
        Assert.Equal("None", state.Bulletin);
        Assert.Equal((byte)0, state.BattlePower);
        Assert.Equal([90U, 91U], state.AllyIds);
        Assert.Equal([92U], state.EnemyIds);
    }

    [Fact]
    public void TryReadDefaultsMissingOrMalformedRelationLinesToEmptyLists()
    {
        using var fixture = CreateFixture(
            """
            Count=3
            106/AlternateClan/AlternateClan/4/90000000/None/4/0/0/0/
            bad/90/
            """);

        Assert.True(LegacyClanRelationState.TryRead(fixture.Path, out var state));

        Assert.Equal(106U, state.ClanId);
        Assert.Empty(state.AllyIds);
        Assert.Empty(state.EnemyIds);
    }

    [Fact]
    public void WriteToPreservesCoreClanLineWhileReplacingRelations()
    {
        using var fixture = CreateFixture(
            """
            Count=3
            113/FixtureClan/FixtureLeader/0/820500000/None/0/0/0/0/
            0/
            1/90/
            """);

        Assert.True(LegacyClanRelationState.TryRead(fixture.Path, out var state));

        state.WithRelations([7U, 8U], [9U]).WriteTo(fixture.Path);

        var lines = File.ReadAllLines(fixture.Path);
        Assert.Equal("Count=3", lines[0]);
        Assert.Equal("113/FixtureClan/FixtureLeader/0/820500000/None/0/0/0/0/", lines[1]);
        Assert.Equal("2/7/8/", lines[2]);
        Assert.Equal("1/9/", lines[3]);
    }

    private static Fixture CreateFixture(string contents)
    {
        var root = Path.Combine(Path.GetTempPath(), "wolfconquer-clan-relations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "113.txt");
        File.WriteAllText(path, contents.Trim('\r', '\n').ReplaceLineEndings(Environment.NewLine));
        return new Fixture(root, path);
    }

    private sealed class Fixture(string root, string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
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
