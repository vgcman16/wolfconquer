using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("uidZero")]
    [InlineData("unknownClass")]
    [InlineData("foreignPlayer")]
    [InlineData("foreignEquipment")]
    [InlineData("reboundUser")]
    [InlineData("unownedNinja")]
    [InlineData("levelWidth")]
    public Task InvalidRankingOwnershipCannotPublishOrEraseAnyRank(string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        scope.Ninja.FireMastery = 1000;
        var invoked = scope.Ninja;
        switch (mutation)
        {
            case "uidZero": scope.Client.Player.UID = 0; break;
            case "unknownClass": scope.SetClass(4999); scope.Ninja.Items = null!; break;
            case "foreignPlayer": scope.Client.Player = new LegacyNativeGameClientRuntime().Player; break;
            case "foreignEquipment": scope.Client.Equipment = new LegacyNativeEquipItemStorage(new LegacyNativeGameClientRuntime()); break;
            case "reboundUser": scope.Ninja.user = new LegacyNativeGameClientRuntime(); break;
            case "levelWidth": scope.SetLevel(300); break;
            case "unownedNinja":
                invoked = new LegacyNativeNinja { user = scope.Client };
                scope.Client.MyNinja = invoked;
                break;
        }
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => invoked.UpdateRank());
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("nullOwner")]
    [InlineData("wrongCategory")]
    [InlineData("aliasedOwner")]
    [InlineData("zeroCap")]
    [InlineData("negativeCap")]
    [InlineData("nullGate")]
    [InlineData("nullMap")]
    [InlineData("nullTail")]
    [InlineData("zeroUidTail")]
    [InlineData("mismatchedUidTail")]
    [InlineData("wrongTypeTail")]
    [InlineData("oldCorruptWind")]
    [InlineData("unknownBucket")]
    public Task EveryRequiredRankAndTailIsAdmittedBeforeAnyMapStore(string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        scope.Ninja.FireMastery = 1000;
        var fire = scope.Rank(RankType.Fire);
        switch (mutation)
        {
            case "missing": LegacyNativeNinjaRank.Ranks.Remove(RankType.Lightning); break;
            case "nullOwner": LegacyNativeNinjaRank.Ranks[RankType.Lightning] = null!; break;
            case "wrongCategory": fire._Type = RankType.Water; break;
            case "aliasedOwner": LegacyNativeNinjaRank.Ranks[RankType.Water] = fire; break;
            case "zeroCap": fire.MaxItems = 0; break;
            case "negativeCap": fire.MaxItems = -1; break;
            case "nullGate": fire.SynRoot = null!; break;
            case "nullMap": fire.RankingList = null!; break;
            case "nullTail": fire.MaxItems = 1; fire.RankingList[99] = null!; break;
            case "zeroUidTail": fire.RankingList[0] = new() { UID = 0, Type = RankType.Fire }; break;
            case "mismatchedUidTail": fire.RankingList[99] = new() { UID = 98, Type = RankType.Fire }; break;
            case "wrongTypeTail": fire.RankingList[99] = new() { UID = 99, Type = RankType.Wind }; break;
            case "oldCorruptWind": scope.Rank(RankType.Wind).RankingList[17].Type = RankType.Overall; break;
            case "unknownBucket": LegacyNativeNinjaRank.Ranks[(RankType)99] = new((RankType)99) { MaxItems = 10 }; break;
        }
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData("missingMetadata")]
    [InlineData("nullMetadata")]
    [InlineData("duplicateMetadata")]
    [InlineData("nullMetadataTail")]
    [InlineData("scoreOverflow")]
    [InlineData("elementOverflow")]
    [InlineData("overallOverflow")]
    public Task RealScorePreparationRefusesMissingAmbiguousOrUnrepresentableInputs(string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        scope.Item(100, 100, 1);
        if (mutation != "missingMetadata") scope.Meta(7, 100, 1, score: 1000);
        switch (mutation)
        {
            case "nullMetadata": LegacyNativeNinjaFile.gouyu_type = null!; break;
            case "duplicateMetadata": scope.Meta(8, 100, 1, score: 1000); break;
            case "nullMetadataTail": scope.Metadata[99] = null!; break;
            case "scoreOverflow": scope.Metadata[7].Score = uint.MaxValue; scope.Ninja.FireMastery = 1; break;
            case "elementOverflow": scope.Ninja.Fire = uint.MaxValue; break;
            case "overallOverflow":
                scope.Ninja.FireMastery = uint.MaxValue - 1000;
                scope.Ninja.WaterMastery = 1;
                break;
        }
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });
}
