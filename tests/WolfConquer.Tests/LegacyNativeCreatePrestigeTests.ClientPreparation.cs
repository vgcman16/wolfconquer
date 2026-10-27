using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData("missing-world")]
    [InlineData("null-tail")]
    [InlineData("mismatched-uid-tail")]
    [InlineData("null-items")]
    public void ClientPreparationLateWorldRefusalPreservesClientPerfectionGuildAndBothRankOwners(string fault) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var world = Ranking.Ranks[Ranking.Type.World];
            switch (fault)
            {
                case "missing-world": Ranking.Ranks.Remove(Ranking.Type.World); break;
                case "null-tail": world.MaxItems = 1; world.Items.Add(999, null!); break;
                case "mismatched-uid-tail":
                    world.MaxItems = 1;
                    world.Items.Add(999, new Ranking.Entry { UID = 23, TotalPoints = 1, Points = [] });
                    break;
                case "null-items": world.Items = null!; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Theory]
    [InlineData("rune")]
    [InlineData("perfection-status")]
    public void ClientPreparationMissingRealRequiredEffectOwnerRefusesWithoutPlaceholderOrPartialPublication(string owner) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            if (owner == "rune") client.Rune = null;
            else client.PerfectionStatus = null!;
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationForeignPhysicalHundredWeaponsOwnerRefusesBeforeChangingOwnedClientOutputs() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var foreign = new LegacyNativeGameClientRuntime();
            foreign.Player.UID = 99;
            foreign.Player.Name = "Other";
            client.HundredWeapons = new LegacyNativeHundredWeapons(foreign);
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationTooShortPointArrayRefusesWithoutResettingItsExistingValues() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            client.PrestigePoints = Enumerable.Repeat(999u, 38).ToArray();
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationActualEquipmentOverflowRefusesWithoutPublishingWrappedPointsOrTotal() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client); // Level0 keeps the effect input valid.
            client.Equipment!.CurentEquip = [new() { ItemPoints = uint.MaxValue }];
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationUnrepresentablePhysicalPrestigeLevelRefusesWithoutSignedNarrowingOrPartialOutputs() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            client.Equipment!.CurentEquip = [new() { ItemPoints = 10_000 }];
            client.PrestigeLevel = uint.MaxValue;
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Theory]
    [InlineData(39)]
    [InlineData(44)]
    public void ClientPreparationValidLegacyPointLengthsPreserveTheirShapeAndPhysicalEffectOwner(int length) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var physical = client.PerfectionStatus;
            var old = Enumerable.Repeat(999u, length).ToArray();
            client.PrestigePoints = old;
            client.CreatePrestigePoints();
            Assert.NotSame(old, client.PrestigePoints);
            Assert.All(old, value => Assert.Equal(999u, value));
            Assert.Equal(length, client.PrestigePoints.Length);
            Assert.Equal(40_000u, client.PrestigePoints[3]);
            Assert.All(client.PrestigePoints.Where((_, index) => index != 3), value => Assert.Equal(0u, value));
            Assert.Equal(40_000u, client.MyPrestigePoints);
            Assert.Same(physical, client.PerfectionStatus);
            Assert.Equal(new double[16], ClientPreparationEffects(physical));
            Assert.Equal(client.PrestigePoints, client.PrestrigeEntry!.Points);
            Assert.NotSame(client.PrestigePoints, client.PrestrigeEntry.Points);
            Assert.Equal(40_000u, client.Player.MyGuildMember!.PrestigePoints);
        });

    [Fact]
    public void ClientPreparationSuccessfulRebuildCopiesValuesIntoExistingEffectAndKeepsEntryPointsIndependent() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            var physical = client.PerfectionStatus;
            var oldPoints = client.PrestigePoints;
            var oldPointValues = oldPoints.ToArray();
            var oldEntry = client.PrestrigeEntry!;
            var oldEntryValues = ClientPreparationEntryValues(oldEntry);
            var oldEntryPoints = oldEntry.Points;
            var oldEntryPointValues = oldEntryPoints?.ToArray();
            var member = client.Player.MyGuildMember;
            SetNextPrestigeClientInputs(client);
            client.CreatePrestigePoints();
            Assert.Same(physical, client.PerfectionStatus);
            Assert.Equal(new double[16], ClientPreparationEffects(physical));
            Assert.NotSame(oldPoints, client.PrestigePoints);
            Assert.Equal(oldPointValues, oldPoints);
            Assert.Equal(oldEntryValues, ClientPreparationEntryValues(oldEntry));
            Assert.Equal(20_000u, oldPoints[3]);
            Assert.Equal(20_000u, oldEntry.TotalPoints);
            Assert.Same(oldEntryPoints, oldEntry.Points);
            Assert.Equal(oldEntryPointValues, oldEntry.Points);
            Assert.Equal(20_000u, oldEntry.Points![3]);
            var published = client.PrestrigeEntry!;
            Assert.NotSame(oldEntry, published);
            Assert.Equal(40_000u, published.TotalPoints);
            Assert.Equal(client.PrestigePoints, published.Points);
            Assert.NotSame(client.PrestigePoints, published.Points);
            Assert.Same(published, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
            Assert.Same(published, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.Same(member, client.Player.MyGuildMember);
            Assert.Equal(40_000u, member!.PrestigePoints);
            client.PrestigePoints[0] = 7;
            Assert.Equal(0u, published.Points![0]);
        });
}
