using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Target = OwnedEquipmentBehaviorFixture.Peer;
    private const int OrdinaryDelay = 700;
    // Declared actual target has447 Vitality,10 Spirit,0 Strength/Agility,
    // class7051 (no Trojan multiplier), no equipment or external HP table.
    private const ulong TargetMaximum = 447 * 24 + 10 * 3;
    private sealed class AttackClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(int milliseconds)
        {
            long delta = TimeSpan.FromMilliseconds(milliseconds).Ticks;
            long before, after;
            do
            {
                before = Interlocked.Read(ref ticks);
                after = checked(before + delta);
            } while (Interlocked.CompareExchange(ref ticks, after, before) != before);
        }
    }

    private static Task Run(Func<AttackFixture, Task> body, long targetHp = 2000, bool peace = false, bool outOfRange = false,
        bool hawks = false, string? targetAction = null, bool enabledScheduling = false)
    {
        var clock = new AttackClock();
        var attacker = Saved(Actor, "BasicOwner", 830, 5000, strength: 300, peace: peace);
        var target = Saved(Target, "BasicTarget", outOfRange ? 840 : 831, targetHp, strength: 0, peace: false);
        if (targetAction is not null)
        {
            var attributes = new Dictionary<string, string>(target.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Action"] = targetAction };
            target = target with { Attributes = attributes };
        }
        var options = new LegacyRuntimeUpdateLoopOptions(enabledScheduling, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        PlayerSpellSnapshot spells = hawks ? new([new(15920, 9, 0, 0, 0, 0)]) : PlayerSpellSnapshot.Empty;
        Func<OwnedEquipmentBehaviorInput, WorldDataCatalog, LegacyRuntimeUpdateLoopOptions, Func<OwnedEquipmentBehaviorFixture, Task>, OwnedEquipmentBehaviorInput[], Task> runner =
            enabledScheduling ? OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync : OwnedEquipmentBehaviorFixture.RunWithOptionsAsync;
        return runner(new(attacker, PlayerItemSnapshot.Empty, spells),
            hawks ? HawksCatalog() : OwnedEquipmentBehaviorFixture.Catalog(), options, async owner =>
            {
                var f = new AttackFixture(owner, clock);
                Assert.Equal(targetHp, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(447L, f.Current(Target).Character.Vitality); Assert.Equal(10L, f.Current(Target).Character.Spirit);
                Assert.Equal(0L, f.Current(Target).Character.Strength); Assert.Equal(0L, f.Current(Target).Character.Agility);
                Assert.Equal(7051L, f.Current(Target).Character.Class); Assert.Empty(f.Current(Target).PlayerItems.EquipmentItems);
                foreach (uint uid in new[] { Actor, Target })
                {
                    var binding = f.Binding(uid);
                    Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
                    Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var speed));
                    Assert.NotNull(speed); Assert.Equal((ushort)0, speed.Right);
                    Assert.Equal((ushort)0, speed.Left); Assert.Equal((ushort)0, speed.Ring);
                }
                f.Clear(); await body(f);
            }, [new OwnedEquipmentBehaviorInput(target, PlayerItemSnapshot.Empty, spells)]);
    }

    private static WorldDataCatalog HawksCatalog()
    {
        // Exact retained DBServer/magictype.txt15928 row: level9, Damage100,
        // duration15. No synthetic power, forced roll or positive flag.
        const string row = "15928@@15920@@6@@Hawk`sAmbition@@0@@0@@0@@0@@9@@0@@100@@0@@100@@15@@0@@0@@256@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@1@@60000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";
        // Complete actual ID00 section: Weapon Archives effect, with no
        // IsRuneSkill field. Do not omit the descriptor or invent a false
        // classification to bypass a genuine selected-profile Rune provider.
        const string effect = "[1592000]\r\nName=Hawk`sAmbition\r\nTargetType=8\r\nClientRepresent=3\r\nSortOfAct=4\r\n" +
            "Role3DEffectOfAttaker=dpzc_atk\r\nDesc=Upgradable~at~Level~15\r\n" +
            "DescEx=Weapon~Archives~Effect\\nCounterblow~~~Triggering~Rate:~100%\\nWhen~triggered,~makes~the~caster~immune~from~damage~once~and~rebound~up~to~45%~damage~to~opponent~for~10~seconds.\r\n" +
            "IsDirTerrianEffect=3\r\nSoundOfAttacker=sound\\skill_dpzc.wav\r\nXp=4\r\nZoomNumVisable=0\r\n";
        return NativeCallerWorldDataFixture.Create(new ItemType[] { }, [], magic: new[] { MagicTypeParser.ParseLine(row) },
            magicEffects: MagicEffectParser.Parse(effect));
    }

    private static CharacterSnapshot Saved(uint uid, string name, long x, long hp, long strength, bool peace)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 7051, strength: strength,
            agility: 0, mapId: 1000, x: x, y: 584, minHitPoints: hp) with { Spirit = 10, MinMana = 50 };
        return character with { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PkMode"] = peace ? "1" : "0", ["DynamicID"] = "1", ["Stamina"] = "100", ["Action"] = "0",
            ["MaxMana"] = "50", ["ExpBallUsed"] = "0",
            ["EpicTrojan"] = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/",
        } };
    }

    private sealed class AttackFixture
    {
        internal readonly OwnedEquipmentBehaviorFixture Owner;
        internal readonly AttackClock Clock;
        internal readonly List<(uint Recipient, byte[] Bytes)> Order = [];
        internal Action<uint, byte[]>? BeforeSend;
        private Exception? observationFailure;
        internal AttackFixture(OwnedEquipmentBehaviorFixture owner, AttackClock clock)
        {
            Owner = owner; Clock = clock;
            owner.WireFrameRecorded = (uid, bytes) => { lock (Order) Order.Add((uid, bytes)); };
            owner.BeforeSend = (uid, bytes) =>
            {
                try { BeforeSend?.Invoke(uid, bytes); }
                catch (Xunit.Sdk.XunitException error) { observationFailure ??= error; throw; }
                if (!owner.HasActualWire(uid)) lock (Order) Order.Add((uid, bytes));
            };
        }
        internal WorldSession Current(uint uid = Actor) => Owner.Current(uid);
        internal LegacyOrdinarySpellBinding Binding(uint uid = Actor) =>
            Assert.IsType<LegacyOrdinarySpellBinding>(Current(uid).NativeGameClient!.OrdinarySpellBinding);
        internal Task AttackAsync() => Owner.SendAsync(Actor, Request().ToFrameBytes());
        internal async Task SitAsync()
        {
            var before = Files(); var stamp = Binding().Player.AttackStamp;
            await Owner.SendAsync(Actor, new MsgActionPacket
                { Type = MsgActionType.ChangeStance, DwParam = 250 }.ToFrameBytes());
            Assert.Equal(250U, LegacyActionState.RequireCurrent(Current().Character));
            Assert.Equal(250U, (uint)Binding().Player.Action);
            Assert.Equal(stamp, Binding().Player.AttackStamp);
            Assert.False(Binding().Client.OnAutoAttack);
            AssertFiles(before); Clear();
        }
        internal async Task MintHawksAsync()
        {
            var catalog = Assert.IsType<WorldDataCatalog>(Binding().ItemCatalog);
            var descriptor = Assert.Single(catalog.MagicEffects, row => row.Id == 1592000);
            Assert.Equal("Hawk`sAmbition", descriptor.Attributes["Name"]);
            Assert.False(LegacyRuneSpellClassification.HasExplicitEntry(catalog, 15920));
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(Current(Actor).Character.Attributes, 256));
            Assert.False(Current(Actor).Character.Attributes.ContainsKey("HawksAmbitionPower"));
            foreach (uint uid in new[] { Target })
            {
                Assert.False(LegacyPlayerDeathState.HasStatusFlag(Current(uid).Character.Attributes, 256));
                await Owner.SendAsync(uid, new MsgAttackPacket(new(uid, uid, 0, 0, MsgAttackType.Magic,
                    0, 0, 0, MsgAttackEffect.None, 0, SpellId: 15920)).ToFrameBytes());
                Assert.True(LegacyPlayerDeathState.HasStatusFlag(Current(uid).Character.Attributes, 256));
                Assert.Equal("100", Current(uid).Character.Attributes["HawksAmbitionPower"]);
                Assert.Contains(Owner.SnapshotFrames(uid), bytes => LegacySpellAnimationPacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == 15920);
            }
            AssertCoherent(2000); Clear();
        }
        internal async Task TickAsync()
        {
            await Owner.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal (uint Recipient, byte[] Bytes)[] SnapshotOrder() { lock (Order) return [.. Order]; }
        internal void Clear() { lock (Order) Order.Clear(); Owner.ClearRecordedFrames(Actor); Owner.ClearRecordedFrames(Target); }
        internal Dictionary<string, byte[]> Files() => Owner.Data.Files();
        internal void AssertFiles(Dictionary<string, byte[]> expected) => Owner.Data.AssertFiles(expected);
        internal void AssertCoherent(long targetHp)
        {
            foreach (uint uid in new[] { Actor, Target })
            {
                var current = Current(uid); var binding = Binding(uid);
                Assert.True(Owner.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Equal(uid == Target ? targetHp : 5000, current.Character.MinHitPoints);
                Assert.Equal(checked((int)current.Character.MinHitPoints), binding.Player.HitPoints);
                Assert.Equal(50U, binding.Player.Mana); Assert.Equal((ushort)100, binding.Player.Stamina);
                Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)binding.Player.Action);
                var bytes = File.ReadAllBytes(Owner.ItemPath(uid));
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(bytes)),
                    PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems),
                    PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            }
            Assert.False(LegacyTradeJournal.HasPending(Owner.Data.Users));
        }
        internal async Task HitAsync(long beforeHp, long afterHp, bool repeat)
        {
            var before = Files(); var after = HpImage(this, before, beforeHp, afterHp); int start = SnapshotOrder().Length;
            var stamp = Clock.GetLocalNow().DateTime;
            BeforeSend = (_, bytes) =>
            {
                var frame = OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
                if (MsgAttackPacket.TryParse(frame, out var incoming) && incoming.Query.AttackType == MsgAttackType.Physical)
                {
                    AssertFiles(after); AssertCoherent(afterHp); AssertCommittedActor(this, stamp);
                    Assert.Equal(InteractionGolden(repeat), bytes);
                }
                if (IsHpPair(bytes, Target))
                {
                    AssertFiles(after); AssertCoherent(afterHp); AssertCommittedActor(this, stamp);
                    Assert.Equal(IsHp(bytes, Target) ? HpGolden(Target, afterHp) : MaximumGolden(Target), bytes);
                }
            };
            try
            {
                if (repeat) await TickAsync(); else await AttackAsync();
                await Owner.DrainWireObservationsAsync();
            }
            finally { BeforeSend = null; }
            AssertFiles(after); AssertCoherent(afterHp); AssertCommittedActor(this, stamp);
            Assert.True(Binding().Client.OnAutoAttack, Owner.Log.ToString());
            var outputs = SnapshotOrder().Skip(start).ToArray();
            var interactions = outputs.Where(row => IsPhysical(row.Bytes)).ToArray();
            Assert.Equal(new[] { Actor, Target }, interactions.Select(row => row.Recipient));
            Assert.All(interactions, row => Assert.Equal(InteractionGolden(repeat), row.Bytes));
            var hp = Assert.Single(outputs, row => IsHp(row.Bytes, Target));
            Assert.Equal(Target, hp.Recipient); Assert.Equal(HpGolden(Target, afterHp), hp.Bytes);
            var pair = outputs.Where(row => IsHpPair(row.Bytes, Target)).ToArray(); Assert.Equal(2, pair.Length);
            Assert.All(pair, row => Assert.Equal(Target, row.Recipient));
            Assert.Equal(MaximumGolden(Target), pair[0].Bytes); Assert.Equal(HpGolden(Target, afterHp), pair[1].Bytes);
            Assert.True(Array.IndexOf(outputs, hp) > Array.FindLastIndex(outputs, row => IsPhysical(row.Bytes)));
            Assert.True(Array.IndexOf(outputs, pair[0]) > Array.FindLastIndex(outputs, row => IsPhysical(row.Bytes)));
        }
    }

    private static MsgAttackPacket Request() => new(new(Actor, Target, 120, 121, MsgAttackType.Physical,
        0, 0, 17, MsgAttackEffect.None, 19));
    private static void AssertCommittedActor(AttackFixture f, DateTime stamp)
    {
        Assert.Equal(0U, LegacyActionState.RequireCurrent(f.Current().Character));
        Assert.Equal(0U, (uint)f.Binding().Player.Action);
        Assert.Equal(stamp, f.Binding().Player.AttackStamp);
    }
    private static bool IsPhysical(byte[] bytes) => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Query.AttackType == MsgAttackType.Physical && packet.Query.Uid == Actor && packet.Query.OpponentUid == Target;
    private static bool IsHp(byte[] bytes, uint uid) => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Hitpoints);
    private static bool IsHpPair(byte[] bytes, uint uid) => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints);
    private static Dictionary<string, byte[]> HpImage(AttackFixture f, Dictionary<string, byte[]> before, long oldHp, long newHp)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string path = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Target));
        var text = CharacterSnapshotFileEncoding.Decode(result[path], out var encoding);
        string token = "MinHitPoints=" + oldHp.ToString(CultureInfo.InvariantCulture) + "\r\n";
        Assert.Equal(1, text.Split(token, StringSplitOptions.None).Length - 1);
        text = text.Replace(token, "MinHitPoints=" + newHp.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        result[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); return result;
    }
    private static Dictionary<string, byte[]> MovedImage(AttackFixture f, Dictionary<string, byte[]> before, long x)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string path = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Actor));
        var text = CharacterSnapshotFileEncoding.Decode(result[path], out var encoding);
        const string token = "X=830\r\n";
        Assert.Equal(1, text.Split(token, StringSplitOptions.None).Length - 1);
        text = text.Replace(token, "X=" + x.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        result[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); return result;
    }
    // Independent tag-varint oracles; no production packet writer generates expected bytes.
    // The genuine native ten-field repeat descriptor drops Unknown; the
    // initial client request retains19. No expected bytes come from that
    // descriptor or the production serializer.
    private static byte[] InteractionGolden(bool repeat = false) => Frame(2135,
        [(4, Actor), (5, Target), (7, 120), (8, 121), (13, 2), (15, 244), (17, 0), (21, 17), (25, 0), (26, repeat ? 0UL : 19UL)]);
    private static byte[] HpGolden(uint uid, long hp)
    {
        var body = new List<byte>(); Varint(body, 8); Varint(body, uid);
        AddMaximum(body);
        var entry = new List<byte>();
        if (hp != 0) { Varint(entry, 16); Varint(entry, checked((ulong)hp)); }
        Varint(body, 18); Varint(body, checked((ulong)entry.Count)); body.AddRange(entry);
        return Frame(2176, body);
    }
    private static byte[] MaximumGolden(uint uid)
    {
        var body = new List<byte>(); Varint(body, 8); Varint(body, uid); AddMaximum(body); return Frame(2176, body);
    }
    private static void AddMaximum(List<byte> body)
    {
        var entry = new List<byte>(); Varint(entry, 8); Varint(entry, 1); Varint(entry, 16); Varint(entry, TargetMaximum);
        Varint(body, 18); Varint(body, checked((ulong)entry.Count)); body.AddRange(entry);
    }
    private static byte[] Frame(ushort opcode, (int Field, ulong Value)[] fields)
    {
        var payload = new List<byte>();
        foreach (var (field, value) in fields) { Varint(payload, checked((ulong)field << 3)); Varint(payload, value); }
        return Frame(opcode, payload);
    }
    private static byte[] Frame(ushort opcode, List<byte> payload)
    {
        var result = new byte[payload.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(result, checked((ushort)result.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), opcode); payload.CopyTo(result, 4); return result;
    }
    private static void Varint(List<byte> bytes, ulong value)
    { do { byte current = (byte)(value & 127); value >>= 7; bytes.Add((byte)(current | (value == 0 ? 0 : 128))); } while (value != 0); }
}
