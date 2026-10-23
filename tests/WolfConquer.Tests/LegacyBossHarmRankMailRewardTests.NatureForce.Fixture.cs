using System.Collections;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    public static TheoryData<string> NatureForceRewardKinds => new() { "rune", "hammer", "wheel", "star", "stone" };

    public static IEnumerable<object[]> NatureForceInvalidMetadataCases()
    {
        foreach (var kind in new[] { "rune", "hammer", "wheel", "star", "stone" })
            foreach (var fault in new[] { "missing", "durability", "malformed", "uid" })
                yield return [kind, fault];
    }

    private sealed record NatureForceSpec(int Family, uint Quest, uint Item, string AutoRoute);

    private static NatureForceSpec NatureForceSpecification(string kind) => kind switch
    {
        "rune" => new(40900, 3636, 3_008_752, "map3998-rune-fragment-autopatcher"),
        "hammer" => new(40902, 3638, 3_008_750, "map3998-violet-bat-king-hammer-autopatcher"),
        "wheel" => new(40905, 3645, 3_008_743, "map3998-wheel-of-nature-autopatcher"),
        "star" => new(40907, 3648, 3_008_748, "map3998-star-ore-autopatcher"),
        "stone" => new(40908, 3644, 3_008_742, "map3998-building-stone-autopatcher"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private async Task<NatureForceScene> RunNatureForceRewardAsync(string kind,
        ushort existing = 0, string fault = "none", string ownership = "current",
        bool accepted = true, bool rate = true, bool full = false, bool failSend = false)
    {
        var spec = NatureForceSpecification(kind);
        var owned = Path.Combine(rootDirectory, "NatureForce-" + Guid.NewGuid().ToString("N"));
        var users = Path.Combine(owned, "Users");
        var itemsRoot = Path.Combine(owned, "PlayersItems");
        Directory.CreateDirectory(users);
        var rows = new List<PlayerItemSnapshotItem> { CreateInventoryItem(9, 720_001) };
        if (existing != 0)
            rows.Add(new PlayerItemSnapshotItem { Uid = 1, ItemId = spec.Item, StackSize = existing,
                Effect = fault == "frame" ? 256U : 0U });
        if (fault == "uid") rows.Add(CreateInventoryItem(uint.MaxValue, 720_002));
        if (full)
            for (var index = rows.Count; index < 40; index++)
                rows.Add(CreateInventoryItem((uint)(100 + index), (uint)(940_000 + index)));
        var source = CreateSession(KillerUid, "LastHit", mapId: 3998,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMap3998Family40900RuneFragmentRate"] = rate ? "1" : "0",
                ["LegacyMap3998Family40905WheelOfNatureRate"] = rate ? "1" : "0",
                ["LegacyMonsterDeathConquerPointRatePercent"] = "0",
                ["LegacyMonsterDeathConquerPointRateRoll"] = "99",
            });
        source = source with { UsersRoot = users,
            PlayerItems = new PlayerItemSnapshot(rows.ToArray(), [], [], [], []),
            Character = source.Character with { PlayerQuests = new PlayerQuestSnapshot([
                new PlayerQuestSnapshotEntry(spec.Quest,
                    accepted ? PlayerQuestStatus.Accepted : PlayerQuestStatus.Finished, 0, [0U])]) } };
        WriteCharacterSnapshotForSession(users, source);
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(KillerUid, source.PlayerItems);
        var userPath = Path.Combine(users, KillerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var itemPath = Path.Combine(itemsRoot, KillerUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var userBefore = File.ReadAllBytes(userPath);
        var itemBefore = File.ReadAllBytes(itemPath);
        var physicalBefore = PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems);
        var rowBefore = source.PlayerItems.InventoryItems.ToArray();
        var attributesBefore = source.Character.Attributes.ToArray();
        var replacement = source with { Character = source.Character with { Money = 77 } };
        WorldSession? committed = null;
        ConquerTcpConnection? connection = null;
        WorldSessionStore? sessions = null;
        sessions = new((_, before, current) =>
        {
            if (!ReferenceEquals(before, source) || ReferenceEquals(current.PlayerItems, source.PlayerItems)) return;
            committed = current;
            if (ownership == "after-commit") sessions!.Set(connection!, replacement);
        });
        var sent = new List<byte[]>();
        var publicationOwners = new List<WorldSession?>();
        connection = new(null, null, (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            if (IsNatureForceItemFrame(bytes, spec.Item))
            {
                sessions.TryGet(connection!, out var currentOwner);
                publicationOwners.Add(currentOwner);
                if (failSend) throw new IOException("owned nature-force output failure");
            }
            return ValueTask.CompletedTask;
        });
        sessions.Set(connection, ownership == "stale" ? replacement : source);
        var type = CreateItemType((int)spec.Item, durability: fault == "durability" ? 65_536U : 17U);
        if (fault == "malformed")
        {
            var fields = type.Fields.ToArray();
            fields[ItemType.DurabilityFieldIndex] = "invalid";
            type = type with { Fields = fields };
        }
        var retirement = new NatureForceRetiringFields(type.Fields, () => sessions.Set(connection, replacement));
        if (fault == "retire") type = type with { Fields = retirement };
        var family = CreateBossFamily(spec.Family, kind, boss: false);
        var spawn = CreateBossSpawn(family, 3998);
        var world = CreateWorldDataCatalog(family, spawn, fault == "missing" ? [] : [type]);
        var monster = LegacyMonsterInstance.FromGenerator(spawn, family, world.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsters = new MonsterRuntimeStateStore();
        PrimeMonsterForServerAuthoritativeFinalHit(monsters, monster);
        using var log = new StringWriter();
        retirement.Armed = fault == "retire";
        Exception? failure = null;
        try
        {
            await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(connection, log, source,
                    WorldData: world, Sessions: sessions, MonsterStates: monsters,
                    Mailboxes: new LegacyMailboxStore(Path.Combine(owned, "Mail")), PlayerItemsRoot: itemsRoot),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(KillerUid, monster.Uid, monster.X, monster.Y,
                    MsgAttackType.Physical, int.MaxValue, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes()));
        }
        catch (Exception exception) { failure = exception; }
        Assert.True(sessions.TryGet(connection, out var result));
        return new(spec, source, result!, replacement, committed, sent, publicationOwners, log.ToString(), failure,
            physicalBefore, rowBefore, attributesBefore, userBefore, File.ReadAllBytes(userPath),
            itemBefore, File.ReadAllBytes(itemPath), retirement.Fired);
    }

    private static bool IsNatureForceItemFrame(byte[] bytes, uint itemId) =>
        LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) && item.ItemId == itemId;

    private sealed class NatureForceRetiringFields(IReadOnlyList<string> fields, Action retire) : IReadOnlyList<string>
    {
        internal bool Armed { get; set; }
        internal bool Fired { get; private set; }
        public int Count => fields.Count;
        public string this[int index]
        {
            get
            {
                var scalar = fields[index];
                if (Armed && !Fired && index == ItemType.DurabilityFieldIndex)
                { Fired = true; retire(); }
                return scalar;
            }
        }
        public IEnumerator<string> GetEnumerator() => fields.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record NatureForceScene(NatureForceSpec Spec, WorldSession Original,
        WorldSession Current, WorldSession Replacement, WorldSession? Committed,
        IReadOnlyList<byte[]> Sent, IReadOnlyList<WorldSession?> PublicationOwners,
        string Log, Exception? Failure, byte[] PhysicalBefore, PlayerItemSnapshotItem[] RowsBefore,
        KeyValuePair<string, string>[] AttributesBefore, byte[] UserBefore, byte[] UserAfter,
        byte[] ItemsBefore, byte[] ItemsAfter, bool Retired);
}
