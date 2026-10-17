using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class NativeEquipmentCacheFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly ConquerTcpConnection connection;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly WorldPacketHandlerContext context;
        private readonly ManualRuntimeTimeProvider clock = new(RootMagicEntryOrigin);

        public NativeEquipmentCacheFixture(int classCode = 14_005, long spirit = 10,
            bool pirateAndJiang = false, bool venom = false,
            Func<NativeEquipmentCacheFixture, byte[], ValueTask>? onSend = null)
        {
            ItemType Item(uint id, uint mana, uint hp = 0)
            {
                var fields = Enumerable.Repeat("0", 60).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "NativeCache" + id;
                fields[18] = hp.ToString(CultureInfo.InvariantCulture);
                fields[19] = mana.ToString(CultureInfo.InvariantCulture);
                return new ItemType(checked((int)id), fields[1], fields);
            }
            DaoqiDictType Jade()
            {
                var fields = Enumerable.Repeat("0", 16).ToArray();
                fields[0] = "90001"; fields[2] = "23"; fields[3] = "7";
                fields[4] = "23"; fields[5] = "8"; fields[^1] = "";
                return DaoqiDictTypeParser.ParseRow("fixture://native-MaxMP-jade", 1, string.Join("@@", fields));
            }
            CollectionRewardTier Collection(int id, int value) => new(id, 700, 0, 0, 23, value, 0,
                "fixture://native-MaxMP-collection", id, "", [id.ToString(CultureInfo.InvariantCulture),
                    "700", "0", "0", "23", value.ToString(CultureInfo.InvariantCulture), "0"]);
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(14_500, sort: 6, useMana: 0, useStamina: 0,
                    damage: 1, duration: 60, range: 18, coolDown: 0, coldTime: 0)],
                items: [Item(111_095, 10, 5_000), Item(110_099, 20), Item(410_095, 30),
                    Item(150_095, 40), Item(120_095, 50)],
                daoqiDictTypes: pirateAndJiang ? [Jade()] : null,
                combatGear: pirateAndJiang ? [CreateRootMissingArrowSourceGear() with
                    { Id = 701, GearTypeId = 701, MagicTypeIds = [],
                        SourceFile = "fixture://native-MaxMP/loaded701" }] : null,
                jiangHuAttributes: pirateAndJiang
                    ? [JiangHuParser.ParseAttributeRow("fixture://native-MaxMana-Jiang", 1, "1 15 1 100")] : null,
                collectionRewardTiers: pirateAndJiang ? [Collection(1, 11), Collection(2, 13)] : null);
            var equipment = new PlayerItemSnapshotItem[]
            {
                new() { Uid = 99_101, ItemId = 111_095, Position = 1, Durability = 100, MaximumDurability = 100,
                    PurificationItemId = 110_099, PurificationDuration = 0, MythSoul = venom ? 11U : 0U },
                new() { Uid = 99_102, ItemId = 120_095, Position = 2, Durability = 100, MaximumDurability = 100, Effect = 203 },
                new() { Uid = 99_106, ItemId = 150_095, Position = 6, Durability = 100, MaximumDurability = 100, Effect = 201 },
                new() { Uid = 99_104, ItemId = 410_095, Position = 4, Durability = 100, MaximumDurability = 100, Effect = 202 },
            };
            var attributes = new Dictionary<string, string> { ["PkMode"] = "0", ["Stamina"] = "100" };
            if (pirateAndJiang)
            {
                var fields = new List<string> { PlayerUid.ToString(CultureInfo.InvariantCulture), "NativeCache",
                    "NativeCache", "140", "3", "0", "1", "30000", "0" };
                for (var stage = 0; stage < 9; stage++)
                {
                    fields.Add(stage == 0 ? "1" : "0");
                    for (var star = 0; star < 9; star++)
                    {
                        fields.Add("0"); // Native UID filter still includes the two nonzero inactive stars.
                        fields.Add(stage == 0 && star < 2 ? "271" : "0");
                    }
                }
                attributes["JiangHu"] = string.Join("/", fields);
            }
            var owner = CreateSession(mapId: 1_000, strength: 0, spirit: spirit, classCode: classCode,
                minHitPoints: 5_000, minMana: 1_000, stamina: 100, spellIds: [14_500],
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = equipment }, attributes: attributes);
            if (pirateAndJiang)
                owner = owner with { CombatGear = CombatGearSnapshot.Empty with { Count = 1,
                    Items = [new CombatGearSnapshotItem(701, 1, 0, 1, 0, 0, [], [90_001])] } };
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    Frames.Enqueue(bytes);
                    if (onSend is not null) await onSend(this, bytes);
                });
            sessions.Set(connection, owner);
            context = new WorldPacketHandlerContext(connection, log, owner, WorldData: world, Sessions: sessions,
                TimeProvider: clock, LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(Owner).MaxMana);
            Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(Owner).RightWeaponItemEffect);
            Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(Owner).RingItemEffect);
            Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(Owner).NecklaceItemEffect);
        }

        public ConcurrentQueue<byte[]> Frames { get; } = new();
        public WorldSession Owner => Assert.IsType<WorldSession>(sessions.TryGet(connection, out var value) ? value : null);
        public Task QueryAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes())).AsTask();
        public async Task<LegacyPlayerStatusPacket> ReadStatusAsync()
        {
            var start = Frames.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            return Assert.Single(Frames.ToArray().Skip(start).Select(ReadFrame)
                .Where(frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode)
                .Select(frame => { Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var packet)); return packet; }));
        }
        public Task UnequipAsync(ushort slot)
        {
            var item = Assert.Single(Owner.PlayerItems.EquipmentItems, item => item.Position == slot);
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgItemUsagePacket(item.Uid, slot, MsgItemUsageAction.Unequip, 0, 0, 0, 0)
                    .ToFrameBytes())).AsTask();
        }
        public Task FineRainAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 14_500)).ToFrameBytes())).AsTask();
        public void ChangeSpiritDuringPublication(long value)
        {
            var owner = Owner;
            Assert.Equal(PlayerUid, owner.Character.Uid);
            sessions.Set(connection, owner with { Character = owner.Character with { Spirit = value } });
        }
        public void Dispose() => log.Dispose();
    }
}
