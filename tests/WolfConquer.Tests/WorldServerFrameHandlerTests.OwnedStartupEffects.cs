using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public Task OwnedEquipmentStartupIssuesActualDerivedSpellsBeforeRuneItems()
    {
        const uint uid = OwnedEquipmentBehaviorFixture.Actor;
        var weapon = OwnedEquipmentBehaviorFixture.Item(500_109, fields: new Dictionary<int, string> { [18] = "31676", [19] = "5000" });
        var rune = OwnedEquipmentBehaviorFixture.Item(4_010_501);
        var rank = Enumerable.Repeat("0", 23).ToArray();
        rank[0] = uid.ToString(CultureInfo.InvariantCulture); rank[1] = "HeroOne"; rank[2] = "40";
        rank[3] = "1000"; rank[4] = "7051"; rank[5] = "140"; rank[20] = ""; rank[^1] = "";
        var weekly = RankingSnapshotParser.ParseCombatGearRankRow("fixture://startup-weekly", 1, string.Join("/", rank));
        const string rankRow = "21939@@21930@@6@@Dark@@0@@0@@0@@0@@10@@0@@1@@0@@800@@1@@0@@16@@417@@0@@0@@0@@0@@0@@0@@0@@48@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@416@@1@@800@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";
        const string venomRow = "20006@@20000@@16@@Venom@@1@@0@@1@@8@@5@@0@@30150@@0@@550@@2@@16@@16@@355@@0@@0@@0@@1@@0@@0@@3@@16@@0@@0@@0@@0@@0@@1@@0@@0@@800@@0@@3000@@1@@20@@0@@0@@0@@0@@0@@0@@30100@@0@@0@@800@@1000@@0@@0@@0@@0@@";
        var catalog = LegacyItemWorldDataFixture.Create(new ItemType[] { weapon, rune }, [],
            combatGearRanks: new CombatGearRankEntry[] { weekly },
            magic: new MagicType[] { MagicTypeParser.ParseLine(rankRow), MagicTypeParser.ParseLine(venomRow) });
        var item = new PlayerItemSnapshotItem { Uid = 2001, ItemId = 500_109, Position = 4,
            Durability = 100, MaximumDurability = 100, MythSoul = 15,
            PerfectionLevel = 10, OwnerUid = uid, OwnerName = "HeroOne" };
        var runeItem = new PlayerItemSnapshotItem { Uid = 3001, ItemId = 4_010_501, Position = 101,
            Durability = 100, MaximumDurability = 100 };
        var actor = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 61_229),
            new PlayerItemSnapshot([], [], [], new PlayerItemSnapshotItem[] { item },
                new PlayerItemSnapshotItem[] { runeItem }), PlayerSpellSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(actor, catalog, owner =>
        {
            var current = owner.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal("20000:5,21930:10", current.Character.Attributes["ManagedDerivedSpellOrigins"]);
            Assert.Contains(current.PlayerSpells.Spells, spell => spell.Id == 21_930 && spell.Level == 10);
            Assert.Contains(current.PlayerSpells.Spells, spell => spell.Id == 20_000 && spell.Level == 5);
            var sent = owner.ActorFrames;
            var equipmentIndex = FindLegacyGameItemIndex(sent, 2001);
            var runeIndex = FindLegacyGameItemIndex(sent, 3001);
            Assert.Equal(equipmentIndex + 13, runeIndex);
            Assert.Equal(new ushort[] { WorldPacketOpcodes.MsgItemUsage, WorldPacketOpcodes.LegacySpell,
                WorldPacketOpcodes.LegacySpell, WorldPacketOpcodes.LegacyServerTimer,
                WorldPacketOpcodes.LegacyMsgUpdate, WorldPacketOpcodes.LegacyMsgUpdate, WorldPacketOpcodes.LegacyPlayerStatus,
                WorldPacketOpcodes.LegacyMsgUpdate, WorldPacketOpcodes.LegacyMsgUpdate, WorldPacketOpcodes.LegacyMsgUpdate,
                WorldPacketOpcodes.LegacyMsgUpdate, WorldPacketOpcodes.LegacyUserTotalRefineLev },
                sent.Skip(equipmentIndex + 1).Take(12).Select(bytes => ReadFrame(bytes).Header.Opcode));
            Assert.True(LegacyStartupItemUsagePacket.TryParse(ReadFrame(sent[equipmentIndex + 1]), out var usage));
            Assert.Equal(LegacyStartupItemUsagePacket.EquipmentStatusAction, usage.Action);
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[equipmentIndex + 2]), out var rankSpell));
            Assert.Equal(21_930, rankSpell.Id); Assert.Equal(10, rankSpell.Level);
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[equipmentIndex + 3]), out var venomSpell));
            Assert.Equal(20_000, venomSpell.Id); Assert.Equal(5, venomSpell.Level);
            Assert.Equal(WorldPacketOpcodes.LegacyServerTimer, ReadFrame(sent[equipmentIndex + 4]).Header.Opcode);
            Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[equipmentIndex + 12]), out var refine));
            Assert.Equal(uid, refine.Uid);
            Assert.Equal(10U, refine.Level);
            Assert.False(owner.Recovery.IsBlocked);
            return Task.CompletedTask;
        });
    }
}
