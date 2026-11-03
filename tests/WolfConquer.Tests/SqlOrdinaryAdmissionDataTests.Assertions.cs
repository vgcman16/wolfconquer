using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    internal static void AssertCompleteData(SqlOrdinaryAdmissionData data)
    {
        Assert.Equal(501L, data.Source.AccountId); Assert.Equal(9001U, data.Source.CharacterUid);
        Assert.Equal(9001L, data.Character.Uid); Assert.Equal(1L, data.Character.Online);
        Assert.Equal(1000L, data.Character.Money); Assert.Equal(100L, data.Character.ConquerPoints);
        Assert.Equal("kept", data.Character.PaidNobility["OpaquePaid"]);
        AssertQuests(data.Character.PlayerQuests);
        Assert.Equal(new uint[] { 81001, 81002, 81003, 81004, 81005, 81006, 81007, 82001, 82002, 82003, 82004, 82005, 82006, 82007 }, data.Source.ItemUids.ToArray());
        var locations = Locations(data.Items); ushort[] positions = [0, 213, 217, 4, 211, 0, 0]; uint[] warehouses = [0, 0, 0, 0, 0, 100, 8];
        for (int index = 0; index < 7; index++)
        {
            var row = Assert.Single(locations[index]);
            Assert.Equal(checked((uint)(81001 + index)), row.Uid); Assert.Equal(1100009U, row.ItemId);
            Assert.Equal(positions[index], row.Position); Assert.Equal(warehouses[index], row.WarehouseId);
            Assert.Equal((ushort)25, row.Durability); Assert.Equal((ushort)30, row.MaximumDurability);
            Assert.Equal((byte)3, row.Plus); Assert.Equal((ushort)1, row.StackSize);
            Assert.Equal(checked((uint)(5 + index)), row.PerfectionLevel); Assert.Equal(checked((uint)(100 + index)), row.PerfectionProgress);
            Assert.Equal(9001U, row.OwnerUid); Assert.Equal("root-" + index, row.OwnerName); Assert.Equal("signed-" + index, row.Signature);
            var child = Assert.Single(row.DepositedItems);
            Assert.Equal(checked((uint)(82001 + index)), child.Uid); Assert.Equal(1088001U, child.ItemId);
            Assert.Equal((ushort)2, child.StackSize); Assert.Equal((byte)1, child.Bound);
            Assert.Equal(checked((uint)(20 + index)), child.PerfectionLevel); Assert.Equal(checked((uint)(200 + index)), child.PerfectionProgress);
            Assert.Equal(9001U, child.OwnerUid); Assert.Equal("child-" + index, child.OwnerName); Assert.Equal("child-signed-" + index, child.Signature);
        }
        Assert.Equal(new PlayerSpellSnapshotEntry[] { new(1110, 1, 0, 0, 0, 0), new(1002, 2, 0x12345678, 3, 4, 1) }, data.Spells.Spells);
        Assert.Equal(new PlayerProficiencySnapshotEntry[] { new(500, 3, 2, 0x01020304, 0x05060708), new(400, 1, 0, 0, 9) }, data.Proficiencies.Proficiencies);
        var gear = data.CombatGear;
        Assert.Equal((11U, 22U, 33U, 12U, 1), (gear.MasteryPoints, gear.TaoistReiki, gear.PirateExp, gear.Lottery, gear.Count));
        Assert.Equal(new CombatGearAirPowersSnapshot(9, 8, 7, 6, 5, 4, 3), gear.AirPowers);
        var equipment = Assert.Single(gear.Items);
        Assert.Equal((701U, 4U, 123U, 1U, 2U, 928000U), (equipment.ItemID, equipment.Level, equipment.Exp, equipment.Equip, equipment.Transform, equipment.MasteryScore));
        Assert.Equal(new uint[] { 101, 102, 103, 104, 105, 106, 107 }, equipment.Animas[0]);
        Assert.Equal(new uint[] { 201, 202, 203, 204, 205, 206, 207 }, equipment.Animas[1]);
        Assert.Equal(new uint[] { 11, 12, 13, 14, 15, 16 }, equipment.BlackJades);
        Assert.Equal(2, gear.BlackJadeBag.Count); Assert.Equal(new uint[] { 301, 302 }, gear.BlackJadeBag.BlackJades);
    }

    internal static void AssertQuests(PlayerQuestSnapshot quests)
    {
        Assert.Equal(2, quests.Quests.Count);
        Assert.Equal((1U, PlayerQuestStatus.Accepted, 7U), (quests.Quests[0].Uid, quests.Quests[0].Status, quests.Quests[0].Time));
        Assert.Equal(new uint[] { 11, 22 }, quests.Quests[0].Intentions);
        Assert.Equal((2U, PlayerQuestStatus.Finished, 9U), (quests.Quests[1].Uid, quests.Quests[1].Status, quests.Quests[1].Time));
        Assert.Equal(new uint[] { 33 }, quests.Quests[1].Intentions); Assert.Equal(1U, Assert.Single(quests.AcceptedQuests).Uid);
    }
}
