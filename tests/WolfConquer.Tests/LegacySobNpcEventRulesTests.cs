using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySobNpcEventRulesTests
{
    [Theory]
    [InlineData(LegacySobNpcEventRules.StakeKind)]
    [InlineData(LegacySobNpcEventRules.ScarecrowKind)]
    public void Map1039StakeAndScarecrowAreTrainingSobs(int kind)
    {
        var sobNpc = SobNpc(
            name: "TrainingDummy",
            kind: kind,
            mapId: LegacySobNpcEventRules.TrainingMapId,
            life: 0,
            maxLife: 0);

        Assert.True(LegacySobNpcEventRules.IsTrainingSob(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc));
    }

    [Fact]
    public void EventPoleIsDamageableAndDiesPersistently()
    {
        var sobNpc = SobNpc(
            name: "AttackPole",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: 1_038,
            life: 50_000_000,
            maxLife: 50_000_000);

        Assert.False(LegacySobNpcEventRules.IsTrainingSob(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.PersistentDeath,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc));
    }

    [Theory]
    [InlineData("LeftGate", LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh)]
    [InlineData("RightGate", LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh)]
    public void EventGateIsDamageableAndCanUseDefaultDemolitionMesh(string name, int expectedMesh)
    {
        var sobNpc = SobNpc(
            id: 25_005,
            name: name,
            kind: LegacySobNpcEventRules.GateKind,
            mapId: 1_038,
            life: 10_000_000,
            maxLife: 10_000_000);

        Assert.False(LegacySobNpcEventRules.IsTrainingSob(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.GateDemolition,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.Equal(expectedMesh, LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc));
        Assert.True(LegacySobNpcEventRules.TryGetDefaultDemolitionMesh(sobNpc, out var mesh));
        Assert.Equal(expectedMesh, mesh);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh)]
    public void GuildWarGateUidsUseDefaultDemolitionMeshEvenWhenNamesAreLocalized(int id, int expectedMesh)
    {
        var sobNpc = SobNpc(
            id: id,
            name: "Gate",
            kind: LegacySobNpcEventRules.GateKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            life: 10_000_000,
            maxLife: 10_000_000);

        Assert.True(LegacySobNpcEventRules.IsKnownGuildWarSob(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.GateDemolition,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.Equal(expectedMesh, LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc));
    }

    [Theory]
    [InlineData(LegacySobNpcEventRules.GuildWarExorcimPillarUid)]
    [InlineData(LegacySobNpcEventRules.GuildWarTenacityPillarUid)]
    public void GuildWarSpecialPillarUidsAreDamageableEvenWithLegacyKind64(int id)
    {
        var sobNpc = SobNpc(
            id: id,
            name: "SpecialPillar",
            kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            life: 120_000_000,
            maxLife: 120_000_000);

        Assert.True(LegacySobNpcEventRules.IsKnownGuildWarSob(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.PersistentDeath,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar",
        100_009)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarTenacityPillarUid,
        "TenacityPillar",
        100_008)]
    public void GuildWarCapturePillarUidsReturnOwnershipChangeDwParam(
        int id,
        string name,
        int expectedDwParam)
    {
        var sobNpc = SobNpc(
            id: id,
            name: name,
            kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            life: 120_000_000,
            maxLife: 120_000_000);

        Assert.True(LegacySobNpcEventRules.IsKnownGuildWarSob(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsGuildWarCapturePillar(sobNpc));
        Assert.True(LegacySobNpcEventRules.IsGuildWarOwnershipChangeBroadcastPillar(sobNpc));
        Assert.Equal(
            (uint)expectedDwParam,
            LegacySobNpcEventRules.GetGuildWarOwnershipChangeDwParam(sobNpc).GetValueOrDefault());
        Assert.True(LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(sobNpc, out var dwParam));
        Assert.Equal((uint)expectedDwParam, dwParam);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar",
        LegacySobNpcEventRules.GuildWarCapturePillarKind,
        LegacySobNpcEventRules.TrainingMapId)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar",
        LegacySobNpcEventRules.PoleKind,
        LegacySobNpcEventRules.GuildWarMapId)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarTenacityPillarUid,
        "TenacityPillar",
        LegacySobNpcEventRules.GateKind,
        LegacySobNpcEventRules.GuildWarMapId)]
    public void GuildWarCapturePillarHelpersRejectWrongMapOrKind(
        int id,
        string name,
        int kind,
        int mapId)
    {
        var sobNpc = SobNpc(
            id: id,
            name: name,
            kind: kind,
            mapId: mapId,
            life: 120_000_000,
            maxLife: 120_000_000);

        Assert.False(LegacySobNpcEventRules.IsGuildWarCapturePillar(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsGuildWarOwnershipChangeBroadcastPillar(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetGuildWarOwnershipChangeDwParam(sobNpc));
        Assert.False(LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(sobNpc, out var dwParam));
        Assert.Equal(0U, dwParam);
    }

    [Fact]
    public void GuildWarCapturePillarHelpersUseSourceUidInsteadOfName()
    {
        var sobNpc = SobNpc(
            id: 25_005,
            name: "ExorcimPillar",
            kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            life: 120_000_000,
            maxLife: 120_000_000);

        Assert.False(LegacySobNpcEventRules.IsKnownGuildWarSob(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsGuildWarCapturePillar(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsGuildWarOwnershipChangeBroadcastPillar(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetGuildWarOwnershipChangeDwParam(sobNpc));
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarHonorPillarUid,
        "HonorPillar",
        LegacySobNpcEventRules.GuildWarCapturePillarKind)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarPillarUid,
        "GuildPillar",
        LegacySobNpcEventRules.PoleKind)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        "LeftGate",
        LegacySobNpcEventRules.GateKind)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        "RightGate",
        LegacySobNpcEventRules.GateKind)]
    public void GuildWarNonBroadcastSobsDoNotReturnOwnershipChangeDwParam(
        int id,
        string name,
        int kind)
    {
        var sobNpc = SobNpc(
            id: id,
            name: name,
            kind: kind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            life: 120_000_000,
            maxLife: 120_000_000);

        Assert.True(LegacySobNpcEventRules.IsKnownGuildWarSob(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsGuildWarCapturePillar(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsGuildWarOwnershipChangeBroadcastPillar(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetGuildWarOwnershipChangeDwParam(sobNpc));
        Assert.False(LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(sobNpc, out var dwParam));
        Assert.Equal(0U, dwParam);
    }

    [Fact]
    public void NeutralSobWithoutDamageableKindOrHitpointsIsNotDamageable()
    {
        var sobNpc = SobNpc(
            name: "Bank",
            kind: 2,
            mapId: 1_000,
            life: 10_000,
            maxLife: 10_000);

        Assert.False(LegacySobNpcEventRules.IsTrainingSob(sobNpc));
        Assert.False(LegacySobNpcEventRules.IsEventDamageableSob(sobNpc));
        Assert.Equal(
            LegacySobNpcDeathBehavior.NotDamageable,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.Null(LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc));
    }

    [Fact]
    public void StaticStatueSobMatchesLegacySavedTailIdentity()
    {
        var sobNpc = SobNpc(
            id: 105_176,
            name: "StaticStatue",
            kind: 0,
            mapId: 1_002,
            life: 936_350,
            maxLife: 936_350);
        var staticStatue = StaticStatueRecord(sobNpc);

        Assert.True(LegacySobNpcEventRules.IsStaticStatueSob(sobNpc, [staticStatue]));
        Assert.Equal(
            LegacySobNpcDeathBehavior.PersistentDeath,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc, [staticStatue]));
        Assert.False(LegacySobNpcEventRules.IsStaticStatueSob(sobNpc with { X = sobNpc.X + 1 }, [staticStatue]));
        Assert.False(LegacySobNpcEventRules.IsStaticStatueSob(
            sobNpc,
            [new StaticStatueRecord(298, [298, 34, 1, 179], "StaticStatue.txt", 2, "298/34/1/179/", ["298", "34", "1", "179", ""])]));
    }

    private static SobNpc SobNpc(
        string name,
        int kind,
        int mapId,
        int life,
        int maxLife,
        int id = 25_005)
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: kind,
            Lookface: 0,
            MapId: mapId,
            X: 85,
            Y: 70,
            Life: life,
            MaxLife: maxLife,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{id},{name},{kind},0,{mapId},85,70,{life},{maxLife},1,0",
            Fields:
            [
                id.ToString(),
                name,
                kind.ToString(),
                "0",
                mapId.ToString(),
                "85",
                "70",
                life.ToString(),
                maxLife.ToString(),
                "1",
                "0",
            ]);
    }

    private static StaticStatueRecord StaticStatueRecord(SobNpc sobNpc)
    {
        var values = new[]
        {
            2,
            34,
            1,
            sobNpc.Id,
            sobNpc.X,
            sobNpc.Y,
            sobNpc.MapId,
            sobNpc.MaxLife,
            sobNpc.Life,
        };

        return new StaticStatueRecord(
            values[0],
            values,
            "StaticStatue.txt",
            2,
            string.Join('/', values) + "/",
            [.. values.Select(value => value.ToString()), string.Empty]);
    }
}
