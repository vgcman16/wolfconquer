using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerKeepsResponseDurabilitySourceNoOp()
    {
        var casterWeapon = new PlayerItemSnapshotItem
        {
            Uid = 900_001,
            ItemId = 421_003,
            Durability = 73,
            MaximumDurability = 100,
            Position = 4,
        };
        var targetArmor = new PlayerItemSnapshotItem
        {
            Uid = 900_002,
            ItemId = 130_003,
            Durability = 41,
            MaximumDurability = 100,
            Position = 3,
        };
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(playerItems: CreateEquipmentItems(casterWeapon));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            playerItems: CreateEquipmentItems(targetArmor));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(casterWeapon.Durability, Assert.Single(updatedCasterSession.PlayerItems.EquipmentItems).Durability);
        Assert.Equal(targetArmor.Durability, Assert.Single(updatedTargetSession.PlayerItems.EquipmentItems).Durability);
        Assert.DoesNotContain(
            casterSent.Concat(targetSent),
            frame => LegacyGameItemPacket.TryParse(ReadFrame(frame), out var item) &&
                item.Mode == LegacyGameItemPacket.UpdateItemMode &&
                (item.Uid == casterWeapon.Uid || item.Uid == targetArmor.Uid));
        AssertNormalThunderDamage(casterSent);
        AssertNormalThunderDamage(targetSent);
    }

    private static PlayerItemSnapshot CreateEquipmentItems(params PlayerItemSnapshotItem[] equipmentItems)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipmentItems,
            RuneItems: []);
    }
}
