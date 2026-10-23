using System.Collections;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    private async Task<RewardMessageScene> KillRewardMessageMonsterAsync(
        string kind, string ownership = "current", bool ownsScroll = true,
        bool includeScrollType = true, uint scrollDurability = 0,
        bool fullInventory = false, ulong luckyPoints = 12,
        bool accepted = true, uint? highestItemUid = null)
    {
        var map = kind switch { "cloud" => 1020, "earth" => 3998, "war" => 10250, _ => throw new ArgumentException(kind) };
        var familyId = kind switch { "cloud" => 13, "earth" => 40906, _ => 10260 };
        var family = CreateBossFamily(familyId, kind == "war" ? "300-StarWarDevil" : kind, boss: false);
        var spawn = CreateBossSpawn(family, map);
        var sceneRoot = Path.Combine(rootDirectory, "RewardMessages-" + Guid.NewGuid().ToString("N"));
        var users = Path.Combine(sceneRoot, "Users");
        var itemsRoot = Path.Combine(sceneRoot, "PlayersItems");
        Directory.CreateDirectory(users);
        var attributes = kind == "war"
            ? DeityLandInventoryRewardsDisabledAttributes(
                ("DeityLandLuckyPoints", luckyPoints.ToString(CultureInfo.InvariantCulture)),
                ("LegacyDeityLandWarDevilLuckyRewardRoll", "22"),
                ("LegacyDeityLandWarDevilBaseRewardRoll", "2"),
                ("LegacyDeityLandRandomDropRate", "0"), ("LegacyMonsterDropRoll", "40"))
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40906EarthForceRate"] = "1",
                ["LegacyMap1020Family13Quest1341Rate"] = "0",
                ["LegacyMap1020Family13Quest1333Rate"] = "0",
                ["LegacyMap1020Family13Quest1783Rate"] = "0",
            };
        // Keep the independent generic kill-CP branch deterministic; this scene
        // measures the specific item reward, not unrelated random wallet grants.
        attributes["LegacyMonsterDeathConquerPointRatePercent"] = "0";
        attributes["LegacyMonsterDeathConquerPointRateRoll"] = "99";
        var source = CreateSession(KillerUid, "LastHit", mapId: map, attributes: attributes);
        var inventory = new List<PlayerItemSnapshotItem>();
        if (kind == "cloud" && ownsScroll) inventory.Add(CreateInventoryItem(7, 720_856, 2));
        if (highestItemUid.HasValue) inventory.Add(CreateInventoryItem(highestItemUid.Value, 720_001));
        if (fullInventory)
            inventory.AddRange(Enumerable.Range(100, 40 - inventory.Count)
                .Select(uid => CreateInventoryItem((uint)uid, (uint)(940_000 + uid))));
        var quest = kind switch { "cloud" => 1337U, "earth" => 3646U, _ => 3944U };
        source = source with
        {
            UsersRoot = users,
            PlayerItems = new PlayerItemSnapshot(inventory.ToArray(), [], [], [], []),
            Character = source.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot([
                    new PlayerQuestSnapshotEntry(quest,
                        accepted && kind != "war" ? PlayerQuestStatus.Accepted : PlayerQuestStatus.Finished, 0, [0U])]),
            },
        };
        WriteCharacterSnapshotForSession(users, source);
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(KillerUid, source.PlayerItems);
        var itemPath = Path.Combine(itemsRoot, KillerUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var userPath = Path.Combine(users, KillerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var itemPreimage = File.ReadAllBytes(itemPath);
        var userPreimage = File.ReadAllBytes(userPath);
        var rowPreimage = PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems);
        var replacement = source with { Character = source.Character with { Money = 77 } };
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var receiptOwnership = new List<bool>();
        var itemOwnership = new List<bool>();
        var originalIds = source.PlayerItems.InventoryItems.Select(item => item.Uid).ToHashSet();
        ConquerTcpConnection? connection = null;
        WorldSessionStore? sessions = null;
        var observerRetired = false;
        sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (ownership == "retire-after-commit" && !observerRetired && previous is not null &&
                current.PlayerItems.InventoryItems.Any(item => item.ItemId == 720_856 && !originalIds.Contains(item.Uid)))
            {
                observerRetired = true;
                sessions!.Set(connection!, replacement);
            }
        });
        var retiredOnAttack = false;
        connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.Text == "You~received~the~Cloud~Riding~Scroll!")
            {
                receiptOwnership.Add(sessions.TryGet(connection!, out var receiptOwner) && receiptOwner is not null &&
                    receiptOwner.PlayerItems.InventoryItems.Any(item => item.ItemId == 720_856 && !originalIds.Contains(item.Uid)));
                if (ownership == "retire-after-receipt") sessions.Set(connection!, replacement);
            }
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var itemPacket) && itemPacket.ItemId == 720_856)
                itemOwnership.Add(sessions.TryGet(connection!, out var itemOwner) && itemOwner is not null &&
                    itemOwner.PlayerItems.InventoryItems.Any(item => item.Uid == itemPacket.Uid));
            if (ownership == "replace-on-attack" && !retiredOnAttack &&
                MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical)
            {
                retiredOnAttack = true;
                sessions.Set(connection!, replacement);
            }
            return ValueTask.CompletedTask;
        });
        var peerConnection = CreateConnection(peerSent);
        if (ownership != "unregistered")
            sessions.Set(connection, ownership == "stale" ? replacement : source);
        sessions.Set(peerConnection, CreateSession(8_888, "Watcher"));
        var metadata = CreateItemType(720_856, durability: scrollDurability);
        var retiringFields = new RetiringCloudFields(metadata.Fields, () => sessions.Set(connection, replacement));
        if (ownership == "metadata-retire") metadata = metadata with { Fields = retiringFields };
        ItemType[] catalogItems = kind switch
        {
            "cloud" => includeScrollType ? [metadata] : [],
            "earth" => [CreateItemType(3_008_754), CreateItemType(3_008_745)],
            _ => [CreateItemType(3_316_793), CreateItemType(3_008_735)],
        };
        var world = CreateWorldDataCatalog(family, spawn, items: catalogItems);
        var monster = LegacyMonsterInstance.FromGenerator(spawn, family, world.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsters = new MonsterRuntimeStateStore();
        PrimeMonsterForServerAuthoritativeFinalHit(monsters, monster);
        using var log = new StringWriter();
        retiringFields.Armed = ownership == "metadata-retire";
        Exception? failure = null;
        try
        {
            await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(connection, log, source,
                    WorldData: world, Sessions: ownership == "missing" ? null : sessions,
                    MonsterStates: monsters, Mailboxes: new LegacyMailboxStore(Path.Combine(sceneRoot, "Mail")),
                    PlayerItemsRoot: itemsRoot),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(KillerUid, monster.Uid, monster.X, monster.Y,
                    MsgAttackType.Physical, int.MaxValue, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes()));
        }
        catch (Exception exception) { failure = exception; }
        sessions.TryGet(connection, out var updated);
        return new RewardMessageScene(source, updated, replacement, sent, peerSent, log.ToString(), failure,
            rowPreimage, itemPreimage, File.ReadAllBytes(itemPath), userPreimage, File.ReadAllBytes(userPath),
            receiptOwnership, itemOwnership, retiringFields.Fired, observerRetired);
    }

    // Failure-only scheduling seam: real ItemType read, original scalar, real
    // store writer. This never authorizes a grant or supplies a save result.
    private sealed class RetiringCloudFields(IReadOnlyList<string> original, Action retire) : IReadOnlyList<string>
    {
        internal bool Armed { get; set; }
        internal bool Fired { get; private set; }
        public int Count => original.Count;
        public string this[int index]
        {
            get
            {
                var value = original[index];
                if (Armed && !Fired && index == ItemType.DurabilityFieldIndex)
                { Fired = true; retire(); }
                return value;
            }
        }
        public IEnumerator<string> GetEnumerator() => original.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record RewardMessageScene(WorldSession Original, WorldSession? Current, WorldSession Replacement,
        IReadOnlyList<byte[]> Sent, IReadOnlyList<byte[]> PeerSent, string Log, Exception? Failure,
        byte[] OriginalRows, byte[] ItemBefore, byte[] ItemAfter, byte[] UserBefore, byte[] UserAfter,
        IReadOnlyList<bool> ReceiptOwnership, IReadOnlyList<bool> ItemOwnership, bool MetadataRetired, bool ObserverRetired);
}
