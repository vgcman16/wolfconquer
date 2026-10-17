using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBattlePowerCalculatorTests
{
    [Fact]
    public void EquipmentBattlePowerMatchesLegacyQualityPlusSocketAnimaFormula()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(
                    itemId: 120269,
                    position: 3,
                    plus: 12,
                    socketOne: 13,
                    socketTwo: 72,
                    animaItemId: 800019),
                Equipment(
                    itemId: 421009,
                    position: 4,
                    plus: 3,
                    socketOne: 3),
                Equipment(
                    itemId: 181525,
                    position: 9,
                    plus: 12,
                    socketOne: 13,
                    socketTwo: 13),
                Equipment(
                    itemId: 619009,
                    position: 5,
                    plus: 12),
            ],
            RuneItems: []);

        var battlePower = LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(playerItems);

        Assert.Equal(53U, battlePower);
    }

    [Fact]
    public void EquipmentBattlePowerUsesActiveAlternateSetAndBaseFallbacks()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 111009, position: 1, plus: 1),
                Equipment(itemId: 112006, position: 51, plus: 2),
                Equipment(itemId: 120009, position: 3, plus: 3),
            ],
            RuneItems: []);

        var inactiveBattlePower = LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(playerItems);
        var activeBattlePower = LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(
            playerItems,
            alternateEquipmentActive: true);

        Assert.Equal(12U, inactiveBattlePower);
        Assert.Equal(10U, activeBattlePower);
    }

    [Fact]
    public void PlayerSpawnFallbackIncludesCalculatedEquipmentBattlePower()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3, plus: 12, socketOne: 13),
            ],
            RuneItems: []);

        var packet = LegacyPlayerSpawnPacket.FromSnapshot(CreateSnapshot(), playerItems);

        Assert.Equal(168UL, packet.GetNumericField(75));
    }

    private static PlayerItemSnapshotItem Equipment(
        uint itemId,
        ushort position,
        byte plus = 0,
        byte socketOne = 0,
        byte socketTwo = 0,
        uint animaItemId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Position = position,
            Plus = plus,
            SocketOne = socketOne,
            SocketTwo = socketTwo,
            AnimaItemId = animaItemId,
        };
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "1293600.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
