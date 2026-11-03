using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndPresentEmptySidecarsYieldEmptyOwnedModelsButKeepTheirExactSourcePresence(bool present)
    {
        var inputs = present ? new SqlWorldStateSidecarInput[]
        {
            new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.EmptyGear()),
        } : SqlWorldStateBundleTests.Missing();
        var before = SqlWorldStateBundleTests.Bundle(SqlWorldStateTestData.Image(501, 9001), inputs);
        var plan = Plan(before); var first = plan.ReadPreparedData(); var second = plan.ReadPreparedData();
        Assert.Same(plan.After, first.Source); Assert.False(plan.HasChanges); AssertRawBundle(before, first.Source);
        Assert.NotSame(first.Spells, second.Spells); Assert.NotSame(first.Proficiencies, second.Proficiencies);
        Assert.NotSame(first.Character.PlayerQuests, second.Character.PlayerQuests); Assert.NotSame(first.CombatGear, second.CombatGear);
        Assert.Empty(first.Spells.Spells); Assert.Empty(first.Proficiencies.Proficiencies); Assert.Empty(first.Character.PlayerQuests.Quests);
        Assert.Empty(first.CombatGear.Items); Assert.Empty(first.CombatGear.BlackJadeBag.BlackJades);
        Assert.All(Locations(first.Items), rows => Assert.Empty(rows));
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(present ? SqlWorldStateSidecarPresence.Present : SqlWorldStateSidecarPresence.Missing, first.Source.Sidecars[index].Presence);
            Assert.Equal(present ? inputs[index].Bytes.ToArray() : [], first.Source.Sidecars[index].CopyBytes());
        }
    }
}
