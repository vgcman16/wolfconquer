using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(12, 1)]
    [InlineData(13, 1)]
    [InlineData(255, 1)]
    [InlineData(12, 10)]
    [InlineData(13, 10)]
    [InlineData(255, 10)]
    public async Task NativeEquipmentQueryClampsRawItemPlusToTwelveForCachedMagicSlots(
        byte rawPlus, ushort position)
    {
        var itemId = position == 1 ? 111_099U : 201_003U;
        var bonusBaseId = position == 1 ? 111_090 : 201_000;
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "NativeRawPlusClampItem";
        fields[18] = "500";
        fields[29] = "7";
        var itemRow = new ItemType(checked((int)itemId), fields[1], fields);
        // Literal native ITPlus layout: HP,min,max,PDef,MA,MDef,agility,dodge.
        var plusTwelve = new ItemAddBonus(bonusBaseId, 12, [0, 0, 0, 0, 123, 0, 0, 0],
            SourceLine: 1, RawLine: FormattableString.Invariant($"{bonusBaseId} 12 0 0 0 0 123 0 0 0"));
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [],
            items: [itemRow], itemAddBonuses: [plusTwelve]);
        var rawItem = new PlayerItemSnapshotItem
        {
            Uid = 99_361, ItemId = itemId, Position = position, Plus = rawPlus,
            Durability = 100, MaximumDurability = 100,
        };
        var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
            strength: 0, spirit: 0, minHitPoints: 300, minMana: 0, spellIds: [],
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [rawItem] });
        var sessions = new WorldSessionStore();
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var frames = new List<byte[]>();
        var connection = CreateSendingConnection(frames);
        sessions.Set(connection, owner);
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: world, Sessions: sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        Assert.Equal(position, ItemType.GetLegacyEquipmentPosition(itemId));
        Assert.Equal(rawPlus, Assert.Single(owner.PlayerItems.EquipmentItems).Plus);
        Assert.Same(rawItem, Assert.Single(owner.PlayerItems.EquipmentItems));
        Assert.Equal(0L, owner.Character.Spirit);
        Assert.False(owner.Character.Attributes.ContainsKey("MagicAttack"));
        Assert.Null(owner.EquipmentHitpointsStatus);
        Assert.Equal(new[] { 0, 0, 0, 0, 123, 0, 0, 0 },
            world.ItemAddBonusByItemTypeIdAndLevel[(bonusBaseId, 12)].Values);

        // Real public Data251; no direct calculator/cache call or seeded status answer.
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
        var bytes = Assert.Single(frames,
            bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
        Assert.Equal(PlayerUid, status.Uid);
        // Native raw MA7 + normalized plusMA123 + Equip1540 increment1.
        Assert.Equal(position == 1 ? 131U : 1U, status.MagicAttack);
        // Source Fan10 redirects BOTH raw itemMA7 and plusMA123 to the flat field.
        Assert.Equal(position == 10 ? 130U : 0U, status.MagicDamageIncrease);
        Assert.Equal(0U, status.MagicPercent);
        Assert.Equal(0U, status.SkillCStrike);
        Assert.Equal(500U, status.MaxHP);
        var current = Assert.IsType<WorldSession>(sessions.TryGet(connection, out var saved) ? saved : null);
        Assert.Equal(300L, current.Character.MinHitPoints);
        GC.KeepAlive(connection);
    }
}
