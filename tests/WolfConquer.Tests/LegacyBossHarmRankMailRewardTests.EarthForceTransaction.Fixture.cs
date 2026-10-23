using System.Collections;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    private async Task<EarthForceTransactionScene> RunEarthForceTransactionAsync(
        ushort fragments, string fault = "none", bool rate = true,
        bool accepted = true, int inventoryCount = 0, bool splitFragments = false,
        string ownership = "current", bool failPublication = false)
    {
        var owned = Path.Combine(rootDirectory, "EarthForceTransaction-" + Guid.NewGuid().ToString("N"));
        var users = Path.Combine(owned, "Users");
        var itemsRoot = Path.Combine(owned, "PlayersItems");
        Directory.CreateDirectory(users);
        var inventory = new List<PlayerItemSnapshotItem> { CreateInventoryItem(9, 720_001) };
        if (fragments != 0)
        {
            if (splitFragments)
            {
                inventory.Add(CreateInventoryItem(1, 3_008_754, 2));
                inventory.Add(CreateInventoryItem(2, 3_008_754, checked((ushort)(fragments - 2))));
            }
            else inventory.Add(CreateInventoryItem(1, 3_008_754, fragments));
        }
        if (fault == "uid-exhausted") inventory.Add(CreateInventoryItem(uint.MaxValue, 720_002));
        for (var index = inventory.Count; index < inventoryCount; index++)
            inventory.Add(CreateInventoryItem(checked((uint)(100 + index)), checked((uint)(940_000 + index))));
        var source = CreateSession(KillerUid, "LastHit", mapId: 3998,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40906EarthForceRate"] = rate ? "1" : "0",
                ["LegacyMonsterDeathConquerPointRatePercent"] = "0",
                ["LegacyMonsterDeathConquerPointRateRoll"] = "99",
            });
        source = source with
        {
            UsersRoot = users,
            PlayerItems = new PlayerItemSnapshot(inventory.ToArray(), [], [], [], []),
            Character = source.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot([
                    new PlayerQuestSnapshotEntry(3646,
                        accepted ? PlayerQuestStatus.Accepted : PlayerQuestStatus.Finished, 0, [0U])]),
            },
        };
        WriteCharacterSnapshotForSession(users, source);
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(KillerUid, source.PlayerItems);
        var userFile = Path.Combine(users, KillerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var itemFile = Path.Combine(itemsRoot, KillerUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var userBefore = File.ReadAllBytes(userFile);
        var itemBefore = File.ReadAllBytes(itemFile);
        var physicalBefore = PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems);
        var rowReferences = source.PlayerItems.InventoryItems.ToArray();
        var attributesBefore = source.Character.Attributes.ToArray();
        var replacement = source with { Character = source.Character with { Money = 77 } };
        var replacementBefore = PlayerItemSnapshotLoader.ToLegacyBytes(replacement.PlayerItems);
        WorldSession? committed = null;
        WorldSessionStore? sessions = null;
        ConquerTcpConnection? connection = null;
        sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (!ReferenceEquals(previous, source) || ReferenceEquals(current.PlayerItems, source.PlayerItems)) return;
            committed = current;
            if (ownership == "after-commit") sessions!.Set(connection!, replacement);
        });
        var sent = new List<byte[]>();
        var publicationOwners = new List<WorldSession?>();
        connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            if (IsEarthForceTransactionRewardFrame(bytes))
            {
                sessions.TryGet(connection!, out var owner);
                publicationOwners.Add(owner);
                if (failPublication) throw new IOException("owned EarthForce output failure");
            }
            return ValueTask.CompletedTask;
        });
        if (ownership != "unregistered")
            sessions.Set(connection, ownership == "stale" ? replacement : source);
        var fragmentType = CreateItemType(3_008_754, durability: fault == "invalid-fragment" ? 65_536U : 11U);
        var earthType = CreateItemType(3_008_745, durability: fault == "invalid-earth" ? 65_536U : 23U);
        if (fault == "malformed-earth")
        {
            var fields = earthType.Fields.ToArray();
            fields[ItemType.DurabilityFieldIndex] = "invalid";
            earthType = earthType with { Fields = fields };
        }
        var retiringFields = new EarthForceRetiringFields(earthType.Fields,
            () => sessions.Set(connection, replacement));
        if (fault == "metadata-retire") earthType = earthType with { Fields = retiringFields };
        var types = new List<ItemType>();
        if (fault != "missing-fragment") types.Add(fragmentType);
        if (fault != "missing-earth") types.Add(earthType);
        var family = CreateBossFamily(40906, "EarthForceCarrier", boss: false);
        var spawn = CreateBossSpawn(family, 3998);
        var world = CreateWorldDataCatalog(family, spawn, types);
        var monster = LegacyMonsterInstance.FromGenerator(spawn, family, world.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsters = new MonsterRuntimeStateStore();
        PrimeMonsterForServerAuthoritativeFinalHit(monsters, monster);
        using var log = new StringWriter();
        retiringFields.Armed = fault == "metadata-retire";
        Exception? failure = null;
        try
        {
            await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(connection, log, source,
                    WorldData: world, Sessions: ownership == "missing-store" ? null : sessions,
                    MonsterStates: monsters, Mailboxes: new LegacyMailboxStore(Path.Combine(owned, "Mail")),
                    PlayerItemsRoot: itemsRoot),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(KillerUid, monster.Uid, monster.X, monster.Y,
                    MsgAttackType.Physical, int.MaxValue, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes()));
        }
        catch (Exception exception) { failure = exception; }
        sessions.TryGet(connection, out var current);
        return new(source, current, replacement, committed, sent, publicationOwners, log.ToString(), failure,
            physicalBefore, replacementBefore, itemBefore, File.ReadAllBytes(itemFile),
            userBefore, File.ReadAllBytes(userFile), rowReferences, attributesBefore, retiringFields.Fired);
    }

    private static bool IsEarthForceTransactionRewardFrame(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return LegacyGameItemPacket.TryParse(frame, out var item) && item.ItemId is 3_008_754 or 3_008_745 ||
            MsgItemUsagePacket.TryParse(frame, out var usage) && usage.Action == MsgItemUsageAction.RemoveInventory;
    }

    private sealed class EarthForceRetiringFields(IReadOnlyList<string> values, Action retire) : IReadOnlyList<string>
    {
        internal bool Armed { get; set; }
        internal bool Fired { get; private set; }
        public int Count => values.Count;
        public string this[int index]
        {
            get
            {
                var scalar = values[index];
                if (Armed && !Fired && index == ItemType.DurabilityFieldIndex)
                { Fired = true; retire(); }
                return scalar;
            }
        }
        public IEnumerator<string> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record EarthForceTransactionScene(
        WorldSession Original, WorldSession? Current, WorldSession Replacement, WorldSession? Committed,
        IReadOnlyList<byte[]> Sent, IReadOnlyList<WorldSession?> PublicationOwners, string Log, Exception? Failure,
        byte[] PhysicalBefore, byte[] ReplacementBefore, byte[] ItemsBefore, byte[] ItemsAfter,
        byte[] UserBefore, byte[] UserAfter, PlayerItemSnapshotItem[] RowReferences,
        KeyValuePair<string, string>[] AttributesBefore, bool MetadataRetired);
}
