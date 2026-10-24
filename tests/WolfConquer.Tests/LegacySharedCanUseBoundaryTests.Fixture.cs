using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class SharedCanUseBoundaryFixture : IDisposable
    {
        public sealed record Before(WorldSession Owner, string Values, object Identity,
            ushort CoolDown, float Damage, string[] SourceFields);

        private readonly WorldSessionStore sessions = new();
        private readonly List<byte[]> frames = [];
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyNativeGameClientRuntime client = new();
        private readonly LegacyNativeMagicRow native;
        private readonly SharedBoundaryRuneFields? runeFields;

        public SharedCanUseBoundaryFixture(MagicType row, GouyuType? gouyu = null,
            int? exclusionFlag = null, long map = 1_000, DateTimeOffset? now = null,
            bool optionalRuneFault = false)
        {
            Id = checked((ushort)row.TypeId);
            runeFields = optionalRuneFault ? new SharedBoundaryRuneFields() : null;
            IReadOnlyList<ItemType> runeItems = runeFields is null ? Array.Empty<ItemType>() :
                [new ItemType(4_036_109, "MPMaster(+9)", runeFields)];
            var data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [row],
                items: runeItems, gouyuTypes: gouyu is null ? [] : [gouyu]);
            var attributes = new Dictionary<string, string>
            {
                ["Reborn"] = "2", ["Level"] = "120",
            };
            if (gouyu is not null)
            {
                Assert.Equal(SharedBoundarySigilType(Id), gouyu.Type);
                Assert.Equal(9, gouyu.Level);
                attributes["MsgNinja"] = SharedBoundaryMsgNinja(Id);
                Assert.Same(gouyu, Assert.Single(data.GouyuTypesByType[gouyu.Type]));
            }
            if (exclusionFlag is { } flag) attributes["StatusFlags"] = BuildStatusFlags(flag);
            var owner = CreateSession(mapId: map, classCode: 5_051, reborn: 2,
                minHitPoints: 5_000, minMana: 1_000, stamina: 100, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id)], attributes: attributes,
                playerItems: runeFields is null ? PlayerItemSnapshot.Empty :
                    CreatePlayerItems(new PlayerItemSnapshotItem
                    {
                        Uid = 98_603, ItemId = 4_036_109, Position = 103,
                        Durability = 100, MaximumDurability = 100,
                    })) with { NativeGameClient = client };
            connection = CreateCounterKillPhysicalModelConnection(frames, client);
            sessions.Set(connection, owner);
            Now = now ?? RootMagicEntryOrigin;
            context = new WorldPacketHandlerContext(connection, log, owner,
                WorldData: data, Sessions: sessions, TimeProvider: new SharedBoundaryClock(Now),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.True(LegacyMagicRuntime.TryResolveNativeLearnedSpell(owner.PlayerSpells, data, Id,
                out var learned, out var spell, out var resolvedNative));
            Spell = spell;
            native = resolvedNative;
            Assert.Equal(Id, learned.Id);
            Assert.Equal(Id, Spell.Id);
            Assert.Equal((byte)0, Spell.Level);
            Assert.Equal(byte.Parse(row.Fields[2], CultureInfo.InvariantCulture), (byte)Spell.Sort);
            Assert.Equal(int.Parse(row.Fields[47], CultureInfo.InvariantCulture), Spell.ColdTime);
            Assert.Null(owner.MagicCooldowns);
            Assert.Empty(frames);
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Equal((ushort)0, owner.LegacyRandomSpell);
            if (runeFields is not null) runeFields.Armed = true; // AFTER genuine finite catalog/owner construction.
        }

        public ushort Id { get; }
        public LegacyMagicRuntimeSpell Spell { get; }
        public DateTimeOffset Now { get; }
        public string Log => log.ToString();
        public int OptionalRuneReads => runeFields?.RequiredClassReads ?? 0;
        public long CompletedRateDraws => client.Player.CompletedRateDraws;
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(connection, out var current) ? current : null);

        public Before Capture()
        {
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection,
                Owner.Character.Uid, out var identity));
            return new(Owner, JsonSerializer.Serialize(Owner), identity,
                native.CoolDown, native.Damage, native.Source.Fields.ToArray());
        }

        public void AssertUnchanged(Before before)
        {
            Assert.Same(before.Owner, Owner);
            Assert.Equal(before.Values, JsonSerializer.Serialize(Owner));
            Assert.Same(before.Owner.PlayerItems, Owner.PlayerItems);
            Assert.Same(before.Owner.PlayerSpells, Owner.PlayerSpells);
            Assert.Same(before.Owner.MagicCooldowns, Owner.MagicCooldowns);
            Assert.True(LegacyNativeClientPublication.TryCurrent(sessions, connection,
                before.Owner.Character.Uid, before.Identity, out var current));
            Assert.Same(before.Owner, current);
            Assert.Same(client, Owner.NativeGameClient);
            Assert.Same(client, connection.NativeGameClient);
            Assert.Same(client.Player, Owner.NativePlayer);
            Assert.Equal(before.CoolDown, native.CoolDown);
            Assert.Equal(before.Damage, native.Damage);
            Assert.Equal(before.SourceFields, native.Source.Fields);
            Assert.Empty(frames);
        }

        public async Task SwitchRegisteredAsync()
        {
            Assert.Equal((ushort)6_003, Id);
            var query = new MsgAttackQuery(PlayerUid, PlayerUid, 830, 584,
                MsgAttackType.CounterKillSwitch, 0, 0, 0, MsgAttackEffect.None,
                Unknown: 0, SpellLevel: 99, SpellId: 0);
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        // PRIVATE COMPONENT proof only: no claim that the native sorts enter this
        // helper via RootFirstFamily's public dispatcher. Invoke the exact real body.
        public async Task<WorldSession?> InvokeExactCanUseComponentAsync()
        {
            var before = Capture();
            var method = Assert.IsAssignableFrom<MethodInfo>(typeof(WorldPacketHandlers).GetMethod(
                "TryPrepareLegacyRootFirstFamilyCanUseAsync", BindingFlags.Static | BindingFlags.NonPublic));
            ValueTask<WorldSession?> actual;
            try
            {
                actual = Assert.IsType<ValueTask<WorldSession?>>(method.Invoke(null,
                    [context with { Session = Owner }, Owner, Spell, CancellationToken.None, before.Identity]));
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            return await actual.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void AssertRegisteredSuccess(ulong stamina, long mana, DateTimeOffset? deadline)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, Owner.Character.MinMana);
            Assert.Equal("1", Owner.Character.Attributes["LegacyCounterKillActive"]);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Equal((ushort)0, Owner.LegacyRandomSpell);
            if (deadline is { } ready) Assert.Equal(ready, Owner.MagicCooldowns![Id]);
            else Assert.Null(Owner.MagicCooldowns);
            var expected = new List<byte[]>
            {
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(PlayerUid, stamina).ToFrameBytes(),
            };
            if (Spell.UseMana > 0)
                expected.Add(new LegacyMsgUpdatePacket(PlayerUid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, checked((ulong)mana))]).ToFrameBytes());
            expected.Add(new MsgAttackPacket(new MsgAttackQuery(PlayerUid, PlayerUid, 830, 584,
                MsgAttackType.CounterKillSwitch, 1, 0, 0, MsgAttackEffect.None,
                Unknown: 0, SpellLevel: 0, SpellId: Id)).ToFrameBytes());
            Assert.Equal(expected.Count, frames.Count);
            for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], frames[index]);
            Assert.Empty(ParseMagicColdTimePackets(frames));
            Assert.Empty(ParseSpellAnimations(frames));
            Assert.DoesNotContain("preparationInvalid", Log, StringComparison.Ordinal);
        }

        public void AssertComponentDeadline(Before before, WorldSession? result, int milliseconds)
        {
            Assert.Same(Owner, result);
            Assert.NotSame(before.Owner, Owner); // The genuine cold dictionary registration is committed.
            Assert.Equal(Now.AddMilliseconds(milliseconds), Owner.MagicCooldowns![Id]);
            Assert.True(Owner.MagicCooldowns[Id] >= Now);
            Assert.Equal(before.Owner.Character.MinMana, Owner.Character.MinMana);
            Assert.Equal(before.Owner.Character.Attributes["Stamina"], Owner.Character.Attributes["Stamina"]);
            Assert.Same(before.Owner.PlayerItems, Owner.PlayerItems);
            Assert.Same(before.Owner.PlayerSpells, Owner.PlayerSpells);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.Equal(before.CoolDown, native.CoolDown);
            Assert.Equal(before.Damage, native.Damage);
            Assert.Equal(before.SourceFields, native.Source.Fields);
            Assert.Empty(frames); // These real level0 rows have zero resource cost and silent ColdTime.
            Assert.Equal(0L, CompletedRateDraws); // This component owner contains no CounterKill Process call.
            Assert.DoesNotContain("coldSigilPowerInvalid", Log, StringComparison.Ordinal);
        }

        public void Dispose() => log.Dispose();
    }

    private sealed class SharedBoundaryClock(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
    }
}
