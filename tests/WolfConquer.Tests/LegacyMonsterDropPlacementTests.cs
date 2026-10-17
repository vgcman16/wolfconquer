using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterDropPlacementTests
{
    [Fact]
    public void ProjectUsesSelectorDropCountAndScattersFromPreviousPlacement()
    {
        var monster = MonsterInstance();
        var killer = Killer();
        var selection = LegacyMonsterDeathDropSelector.Select(
            monster,
            MonsterFamily(),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 43,
                MoneyAmount: 50_000,
                DropCountRateRoll: 99),
            KnownItems(1_091_020));

        var projection = LegacyMonsterDropPlacement.Project(
            monster,
            killer,
            selection,
            new LegacyMonsterDropPlacementRolls(FirstFloorUid: 900_100),
            new LegacyMonsterDropPlacementContext
            {
                IsGroundValid = static (_, _, _) => true,
            });

        Assert.Equal(LegacyMonsterDeathDropKind.Money, selection.Kind);
        Assert.Equal((byte)3, selection.DropCount);
        Assert.Equal(3, projection.Decisions.Count);
        Assert.All(projection.Decisions, decision => Assert.Equal(LegacyMonsterDropPlacementStatus.Placed, decision.Status));
        Assert.Equal(
            new[]
            {
                (X: (ushort)841, Y: (ushort)573),
                (X: (ushort)840, Y: (ushort)572),
                (X: (ushort)839, Y: (ushort)571),
            },
            projection.Placed.Select(decision => (decision.X, decision.Y)).ToArray());
        Assert.Equal(
            new[] { 900_100U, 900_101U, 900_102U },
            projection.Placed.Select(decision => decision.FloorUid).ToArray());

        var first = projection.Placed[0];
        Assert.NotNull(first.Packet);
        Assert.NotNull(first.RuntimeEntry);
        Assert.Equal(1_091_020U, first.Packet.ItemId);
        Assert.Equal(1_293_600U, first.Packet.ItemOwnerUid);
        Assert.True(first.OwnerOnly);
        Assert.Equal(FloorItemRuntimePickupKind.Money, first.PickupKind);
        Assert.Equal(50_000, first.Amount);
        Assert.Equal(FloorItemRuntimePickupKind.Money, first.RuntimeEntry.Kind);
        Assert.Equal(50_000, first.RuntimeEntry.Amount);
    }

    [Fact]
    public void ProjectRejectsBlockedMonsterDropMapEvenWhenSelectionWasSupplied()
    {
        var projection = LegacyMonsterDropPlacement.Project(
            MonsterInstance(mapId: 10_250),
            Killer(mapId: 10_250),
            new LegacyMonsterDeathDropSelection(
                LegacyMonsterDeathDropKind.Item,
                "supplied selection",
                DropCount: 1,
                ItemId: 1_002_000),
            new LegacyMonsterDropPlacementRolls(FirstFloorUid: 900_200));

        var decision = Assert.Single(projection.Decisions);
        Assert.Equal(LegacyMonsterDropPlacementStatus.MapRejected, decision.Status);
        Assert.Null(decision.Packet);
        Assert.Null(decision.RuntimeEntry);
        Assert.Contains("blocked monster-drop map 10250", decision.Reason);
    }

    [Fact]
    public void ProjectReturnsNoValidGroundDecisionWhenAddGroundItemCannotPlace()
    {
        var projection = LegacyMonsterDropPlacement.Project(
            MonsterInstance(),
            Killer(),
            new LegacyMonsterDeathDropSelection(
                LegacyMonsterDeathDropKind.Item,
                "supplied selection",
                DropCount: 2,
                ItemId: 1_002_000),
            new LegacyMonsterDropPlacementRolls(FirstFloorUid: 900_300),
            new LegacyMonsterDropPlacementContext
            {
                IsGroundValid = static (_, _, _) => false,
            });

        Assert.Equal(2, projection.Decisions.Count);
        Assert.All(projection.Decisions, decision =>
        {
            Assert.Equal(LegacyMonsterDropPlacementStatus.NoValidGround, decision.Status);
            Assert.Null(decision.Packet);
            Assert.Null(decision.RuntimeEntry);
            Assert.Equal(0U, decision.FloorUid);
            Assert.Contains("AddGroundItem found no unoccupied valid cell", decision.Reason);
        });
        Assert.Equal((ushort)841, projection.Decisions[0].OriginX);
        Assert.Equal((ushort)573, projection.Decisions[0].OriginY);
        Assert.Equal((ushort)0, projection.Decisions[1].OriginX);
        Assert.Equal((ushort)0, projection.Decisions[1].OriginY);
    }

    [Fact]
    public void ProjectMarksVisibleNonOwnerAsOwnerLockedAndKeepsFarViewerHidden()
    {
        var projection = LegacyMonsterDropPlacement.Project(
            MonsterInstance(),
            Killer(),
            new LegacyMonsterDeathDropSelection(
                LegacyMonsterDeathDropKind.Item,
                "supplied selection",
                DropCount: 1,
                ItemId: 1_002_000),
            new LegacyMonsterDropPlacementRolls(FirstFloorUid: 900_400),
            new LegacyMonsterDropPlacementContext
            {
                IsGroundValid = static (_, _, _) => true,
                Observers =
                [
                    new LegacyMonsterDropObserver(1_293_601, 1015, 842, 573),
                    new LegacyMonsterDropObserver(1_293_602, 1015, 860, 573),
                ],
            });

        var decision = Assert.Single(projection.Placed);
        var owner = Assert.Single(decision.Visibility, visibility => visibility.ViewerUid == 1_293_600);
        var peer = Assert.Single(decision.Visibility, visibility => visibility.ViewerUid == 1_293_601);
        var far = Assert.Single(decision.Visibility, visibility => visibility.ViewerUid == 1_293_602);

        Assert.True(owner.Visible);
        Assert.False(owner.PickupOwnerLocked);
        Assert.True(peer.Visible);
        Assert.True(peer.PickupOwnerLocked);
        Assert.Contains("owner-locked", peer.Reason);
        Assert.Contains("team pickup exceptions", peer.Reason);
        Assert.False(far.Visible);
        Assert.False(far.PickupOwnerLocked);
        Assert.Contains("less than 18", far.Reason);
    }

    [Fact]
    public void ProjectKeepsPendingSourceGapsExplicitForItemProjection()
    {
        var projection = LegacyMonsterDropPlacement.Project(
            MonsterInstance(),
            Killer(),
            new LegacyMonsterDeathDropSelection(
                LegacyMonsterDeathDropKind.SpecialItem,
                "supplied special selection",
                DropCount: 1,
                ItemId: 1_088_000),
            new LegacyMonsterDropPlacementRolls(FirstFloorUid: 900_500));

        var decision = Assert.Single(projection.Placed);
        Assert.Contains(decision.PendingSourceBehaviors, reason => reason.Contains("Static terrain Valid-flag"));
        Assert.Contains(decision.PendingSourceBehaviors, reason => reason.Contains("item-base projection"));
        Assert.Contains(decision.PendingSourceBehaviors, reason => reason.Contains("special-drop"));
    }

    private static LegacyMonsterInstance MonsterInstance(int mapId = 1015)
    {
        return new LegacyMonsterInstance(
            Uid: 470_000,
            mapId,
            X: 841,
            Y: 573,
            Mesh: 200,
            Level: 87,
            Hitpoints: 0,
            MaxLife: 8_925,
            MonsterId: 18,
            Name: "Birdman",
            Boss: false,
            GeneratorId: 1,
            SpawnIndex: 0);
    }

    private static CharacterSnapshot Killer(int mapId = 1015)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "user02",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 15,
            FirstClass: 15,
            SecondClass: 15,
            Reborn: 0,
            Level: 87,
            Experience: 0,
            MapId: mapId,
            X: 841,
            Y: 573,
            PreviousMapId: mapId,
            PreviousMapX: 841,
            PreviousMapY: 573,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "1293600.ini",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
    }

    private static Monster MonsterFamily()
    {
        return new Monster(
            18,
            "Birdman",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            []);
    }

    private static LegacyMonsterDeathDropContext KnownItems(params uint[] itemIds)
    {
        var known = new HashSet<uint>(itemIds);
        return new LegacyMonsterDeathDropContext(known.Contains);
    }
}
