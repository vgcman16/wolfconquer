using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class NativeRiseGlobalEntryFixture : IDisposable
    {
        private const ushort Rise = 15_440;
        private const ushort SentinelSpell = 60_000;
        private static readonly DateTimeOffset SentinelDeadline = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly WorldSessionStore sessions = new();
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ManualRuntimeTimeProvider clock = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldPacketHandlerContext context;
        private readonly bool mpPrefix;
        private readonly int coldTime;
        private readonly string? mutation;
        private bool armed;
        private int callbackCount;
        private int framesAtCallback;
        private WorldSession? replacement;

        public NativeRiseGlobalEntryFixture(bool learned = true, bool mpPrefix = false,
            int coldTime = 4_000, string? mutation = null)
        {
            this.mpPrefix = mpPrefix;
            this.coldTime = coldTime;
            this.mutation = mutation;
            ItemType RawItem(int id, string hp, string mp)
            {
                var fields = Enumerable.Repeat("0", 64).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "RiseGlobalRaw" + id;
                fields[18] = hp; fields[19] = mp;
                return new ItemType(id, fields[1], fields);
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(Rise, sort: 6, useMana: 37, useStamina: 11, damage: 27,
                    duration: 60, range: 18, coolDown: 500, coldTime: coldTime),
                 CreateMagicRow(7_001, sort: 32, useMana: 0, useStamina: 0, damage: 100,
                    coolDown: 0, coldTime: 0)],
                items: [RawItem(111_098, "5000", "1000"), RawItem(410_098, "0", "0")]);
            var owner = CreateSession(mapId: 1_015, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 5_000, minMana: 500, stamina: 100,
                spellIds: learned ? [Rise, 7_001] : [7_001],
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems =
                [
                    new() { Uid = 99_391, ItemId = 111_098, Position = 1, Durability = 100, MaximumDurability = 100 },
                    new() { Uid = 99_394, ItemId = 410_098, Position = 4, Effect = mpPrefix ? 202U : 0U,
                        Durability = 100, MaximumDurability = 100 },
                ] },
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0",
                    ["LegacyWeaponEffectRateRoll.1175"] = "49", // Existing deterministic input for native Rate(50).
                }) with
                { MagicCooldowns = new Dictionary<ushort, DateTimeOffset> { [SentinelSpell] = SentinelDeadline } };
            var peer = CreateSession(uid: TargetPlayerUid, name: "RiseGlobalPeer", mapId: 1_015,
                x: 832, y: 584, strength: 0, spirit: 0, minHitPoints: 5_000, minMana: 0,
                spellIds: [], attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            ownerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    Order.Enqueue((PlayerUid, bytes));
                    if (!armed || mutation is null || !Scalar(bytes, LegacyMsgUpdateDataType.Mana, 800) ||
                        Interlocked.CompareExchange(ref callbackCount, 1, 0) != 0) return ValueTask.CompletedTask;
                    var current = Owner;
                    Assert.Equal("100", current.Character.Attributes["Stamina"]);
                    Assert.Equal(800L, current.Character.MinMana);
                    Assert.Equal(Now.ToString("O", CultureInfo.InvariantCulture),
                        current.Character.Attributes["LegacyAttackStampUtc"]);
                    Assert.False(HasLegacyStatusFlag(current.Character.Attributes, 227));
                    var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(current.MagicCooldowns);
                    Assert.Equal(SentinelDeadline, cooldowns[SentinelSpell]);
                    Assert.Single(cooldowns); // Prefix publication precedes the selected spell's CanUse.
                    var attributes = new Dictionary<string, string>(current.Character.Attributes)
                        { ["RiseGlobalPrefixMarker"] = mutation };
                    if (mutation == "rebind")
                    {
                        attributes["Stamina"] = "77";
                        replacement = current with
                        {
                            Character = current.Character with { Attributes = attributes, MinMana = 777, MinHitPoints = 4_321 },
                            MagicCooldowns = new Dictionary<ushort, DateTimeOffset> { [SentinelSpell] = SentinelDeadline },
                        };
                        Assert.True(sessions.TryRemove(ownerConnection!, out var removed));
                        Assert.Same(current, removed);
                        sessions.Set(ownerConnection!, replacement); // Declared same-UID client-lifetime sentinel input.
                    }
                    else
                        sessions.Set(ownerConnection!, current with { Character = current.Character with
                            { Attributes = attributes, MinHitPoints = mutation == "dead" ? 0 : current.Character.MinHitPoints } });
                    framesAtCallback = Order.Count;
                    return ValueTask.CompletedTask;
                });
            peerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, _) => { Order.Enqueue((TargetPlayerUid, memory.ToArray())); return ValueTask.CompletedTask; });
            sessions.Set(ownerConnection, owner);
            sessions.Set(peerConnection, peer);
            context = new WorldPacketHandlerContext(ownerConnection, log, owner, WorldData: world,
                Sessions: sessions, TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 0x32));
            Assert.DoesNotContain(Owner.PlayerSpells.Spells, spell => spell.Id == 1_175);
            Assert.Empty(Owner.PlayerItems.RuneItems);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public int CallbackCount => Volatile.Read(ref callbackCount);
        public DateTimeOffset Now => clock.GetUtcNow();
        public WorldSession Owner => Assert.IsType<WorldSession>(sessions.TryGet(ownerConnection, out var current) ? current : null);
        private WorldSession Peer => Assert.IsType<WorldSession>(sessions.TryGet(peerConnection, out var current) ? current : null);
        public byte[][] FramesSince(uint uid, int start) => Order.ToArray().Skip(start)
            .Where(entry => entry.Recipient == uid).Select(entry => entry.Bytes).ToArray();
        public void Advance(TimeSpan duration) => clock.Advance(duration);
        public void Arm() => armed = true;
        public void Disarm() => armed = false;

        public async Task QueryAsync()
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var statusBytes = Assert.Single(FramesSince(PlayerUid, start),
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            Assert.Equal(1U, status.MagicAttack);
            Assert.Equal(5_000U, status.MaxHP);
            Assert.Equal(1_000U, status.MaxMana);
            Assert.Equal(5_000L, Owner.Character.MinHitPoints);
            Assert.Equal(500L, Owner.Character.MinMana);
            Assert.Equal(mpPrefix ? 202U : 0U, LegacyPhysicalStatusOverlay.ForOwner(Owner).RightWeaponItemEffect);
            Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(Owner).RidingCropItemId);
        }

        private Task CastAsync(ushort id) => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 832, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: id)).ToFrameBytes())).AsTask();
        public Task CastRiseAsync() => CastAsync(Rise);
        public async Task MountAsync()
        {
            var start = Order.Count;
            await CastAsync(7_001); // Actual public producer: never assign Ride, its deadline or an AttackStamp.
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 0x32));
            Assert.Single(ParseSpellAnimations(FramesSince(PlayerUid, start)), packet => packet.SpellId == 7_001);
            Advance(TimeSpan.FromMilliseconds(500));
        }

        public (LegacyMsgUpdateDataType Type, ulong Value)[] CostsSince(int start) =>
            ParseMsgUpdates(FramesSince(PlayerUid, start)).SelectMany(packet => packet.Updates)
                .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
                .Select(entry => (entry.Type, entry.Value)).ToArray();

        public void AssertUnspent()
        {
            Assert.Equal("100", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(500L, Owner.Character.MinMana);
            Assert.Equal(5_000L, Owner.Character.MinHitPoints);
            Assert.Equal(5_000L, Peer.Character.MinHitPoints);
            var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(Owner.MagicCooldowns);
            Assert.Single(cooldowns);
            Assert.Equal(SentinelDeadline, cooldowns[SentinelSpell]);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
        }

        public void AssertNoRiseSince(int start)
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var frames = FramesSince(uid, start);
                Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId == Rise);
                Assert.DoesNotContain(frames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
                Assert.DoesNotContain(frames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
                Assert.DoesNotContain(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Query.AttackType == MsgAttackType.Magic && packet.Query.SpellId == Rise);
            }
            Assert.Empty(CostsSince(start));
        }

        public void AssertCompletedSince(int start, ulong expectedStamina, long expectedMana, bool dead)
        {
            Assert.True(Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var producedStamp));
            Assert.Equal(Now.ToString("O", CultureInfo.InvariantCulture), producedStamp);
            Assert.Equal(expectedStamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(expectedMana, Owner.Character.MinMana);
            Assert.Equal(dead ? 50_000L : 55_000L, Owner.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
            Assert.Equal("27", Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
            var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(Owner.MagicCooldowns);
            Assert.Equal(SentinelDeadline, cooldowns[SentinelSpell]);
            Assert.Equal(coldTime > 0 ? 2 : 1, cooldowns.Count);
            if (coldTime > 0) Assert.Equal(Now.AddMilliseconds(coldTime), cooldowns[Rise]);
            else Assert.False(cooldowns.ContainsKey(Rise));
            var interactionBytes = Assert.Single(FramesSince(PlayerUid, start), bytes =>
                MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.SpellId == Rise &&
                packet.Query.AttackType == MsgAttackType.Magic);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(interactionBytes), out var interaction));
            Assert.Equal(PlayerUid, interaction.Query.Uid);
            Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
            var statusBytes = Assert.Single(FramesSince(PlayerUid, start),
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(13_001U, status.MagicAttack);
            Assert.Equal(55_000U, status.MaxHP);
            Assert.Equal(1_000U, status.MaxMana);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var frames = FramesSince(uid, start);
                var rise = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == Rise);
                Assert.Equal(PlayerUid, rise.AttackerUid);
                Assert.Equal(PlayerUid, rise.AttackedUid);
                Assert.Equal(0U, Assert.Single(rise.Targets).Damage);
                Assert.DoesNotContain(frames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
            }
            Assert.Equal(5_000L, Peer.Character.MinHitPoints);
        }

        public string[] PrefixAndCostTraceSince(int start)
        {
            var trace = new List<string>();
            foreach (var entry in Order.ToArray().Skip(start))
            {
                if (LegacySpellAnimationPacket.TryParse(ReadFrame(entry.Bytes), out var animation) && animation.SpellId == 1_175)
                    trace.Add($"{entry.Recipient}:spell:1175");
                else if (LegacyMsgUpdatePacket.TryParse(ReadFrame(entry.Bytes), out var update))
                    foreach (var scalar in update.Updates)
                        if (scalar.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina)
                            trace.Add($"{entry.Recipient}:{(scalar.Type == LegacyMsgUpdateDataType.Mana ? "mana" : "stamina")}:{scalar.Value}");
            }
            return trace.ToArray();
        }

        public void AssertReplacementUntouched()
        {
            Assert.NotNull(replacement);
            Assert.Same(replacement, Owner);
            Assert.Equal(PlayerUid, Owner.Character.Uid);
            Assert.Equal("rebind", Owner.Character.Attributes["RiseGlobalPrefixMarker"]);
            Assert.Equal("77", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(777L, Owner.Character.MinMana);
            Assert.Equal(4_321L, Owner.Character.MinHitPoints);
            var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(Owner.MagicCooldowns);
            Assert.Single(cooldowns);
            Assert.Equal(SentinelDeadline, cooldowns[SentinelSpell]);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
            Assert.Equal(framesAtCallback, Order.Count);
            Assert.Equal(5_000L, Peer.Character.MinHitPoints);
        }

        private static bool Scalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == type && entry.Value == value);

        public void Dispose()
        {
            GC.KeepAlive(ownerConnection); GC.KeepAlive(peerConnection);
            log.Dispose(); // No listener, runtime loop, save/account files or external process is created.
        }
    }
}
