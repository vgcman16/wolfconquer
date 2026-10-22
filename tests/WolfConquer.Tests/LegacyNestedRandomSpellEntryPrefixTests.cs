using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(20_220, "failed-cost")]
    [InlineData(20_360, "failed-cost")]
    [InlineData(20_360, "direct-range")]
    public async Task NativeNestedRandomPrefixIsAssignedBeforeFailedCostOrDirectRange(
        ushort procId, string rejection)
    {
        using var fixture = new NestedRandomPrefixFixture(procId,
            directRangeDenial: rejection == "direct-range");
        await fixture.PrepareAsync(mount: false);
        Assert.Equal((ushort)0, fixture.Target.LegacyRandomSpell); // WHITE-BOX READ ONLY actual default.
        await fixture.CastIncomingAsync();
        Assert.Equal(procId, fixture.Target.LegacyRandomSpell); // Actual stored field; never seeded.
        Assert.Empty(fixture.ProcAnimations());
        Assert.Empty(fixture.TargetStamina());
        Assert.Null(fixture.Target.MagicCooldowns);
        Assert.Equal("5", fixture.Target.Character.Attributes["Stamina"]);
    }

    [Theory]
    [InlineData(20_220, "missing-book")]
    [InlineData(20_220, "rate-denial")]
    [InlineData(20_360, "missing-book")]
    [InlineData(20_360, "rate-denial")]
    public async Task NativeNestedRandomPrefixEarlierBookOrRateDenialRetainsGenuinelyEarnedOtherId(
        ushort wantedId, string denial)
    {
        var otherId = wantedId == 20_220 ? (ushort)20_360 : (ushort)20_220;
        using var fixture = new NestedRandomPrefixFixture(otherId);
        await fixture.PrepareAsync(mount: false);
        await fixture.CastIncomingAsync(); // Real failed-cost proc entry EARNS a different RandomSpell.
        Assert.Equal(otherId, fixture.Target.LegacyRandomSpell);
        fixture.SelectProc(wantedId, denial); // Declared current book/gear/draw input, not field mutation.
        await fixture.CastIncomingAsync();
        Assert.Equal(otherId, fixture.Target.LegacyRandomSpell);
        Assert.Empty(fixture.ProcAnimations());
        Assert.Empty(fixture.TargetStamina());
        Assert.Null(fixture.Target.MagicCooldowns);
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("same-client-copy")]
    [InlineData("removed")]
    [InlineData("fresh-same-uid")]
    public async Task NativeNestedRandomPrefixPrecedesActualSteedSendAndDoesNotRetargetFreshOwner(
        string transition)
    {
        var witnessed = 0;
        WorldSession? replacement = null;
        using var fixture = new NestedRandomPrefixFixture(20_220,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == TargetPlayerUid && NestedRandomPrefixDemount(bytes) &&
                    Interlocked.CompareExchange(ref witnessed, 1, 0) == 0)
                {
                    Assert.Equal((ushort)20_220, current.Target.LegacyRandomSpell); // READ ONLY before send tail.
                    Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 0x32));
                    Assert.Equal("5", current.Target.Character.Attributes["Stamina"]);
                    Assert.Null(current.Target.MagicCooldowns);
                    if (transition == "same-client-copy") current.CopyOwner("demount");
                    else if (transition is "removed" or "fresh-same-uid")
                        replacement = current.RetireOwner(transition == "fresh-same-uid");
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrepareAsync(mount: true); // Real7001; requires the separately HELD53A2 steed prerequisite.
        Assert.Equal((ushort)0, fixture.Target.LegacyRandomSpell);
        await fixture.CastIncomingAsync();
        Assert.Equal(1, witnessed);
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.ProcAnimations());
        if (transition is "ordinary" or "same-client-copy")
        {
            Assert.Equal((ushort)20_220, fixture.Target.LegacyRandomSpell);
            if (transition == "same-client-copy")
                Assert.Equal("demount", fixture.Target.Character.Attributes["NestedRandomCopy"]);
        }
        else if (transition == "removed") Assert.Null(fixture.TryTarget);
        else
        {
            Assert.Same(replacement, fixture.TryTarget);
            Assert.Equal((ushort)0, fixture.TryTarget!.LegacyRandomSpell); // Fresh input's native default, not a write.
            Assert.Equal("fresh", fixture.TryTarget.Character.Attributes["NestedRandomGeneration"]);
            Assert.Equal(43_210, fixture.TryTarget.Character.MinHitPoints);
            await fixture.CastIncomingAsync(); // The FRESH generation must earn its OWN prefix.
            Assert.Equal((ushort)20_220, fixture.Target.LegacyRandomSpell);
            Assert.Equal("fresh", fixture.Target.Character.Attributes["NestedRandomGeneration"]);
        }
    }

    [Theory]
    [InlineData(20_220)]
    [InlineData(20_360)]
    public async Task NativeNestedRandomPrefixIsVisibleBeforeRealCostSendAndSurvivesSameClientCopy(
        ushort procId)
    {
        var witnessed = 0;
        using var fixture = new NestedRandomPrefixFixture(procId, stamina: 100,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == TargetPlayerUid && NestedProcStamina(bytes, TargetPlayerUid).HasValue &&
                    Interlocked.CompareExchange(ref witnessed, 1, 0) == 0)
                {
                    Assert.Equal(procId, current.Target.LegacyRandomSpell);
                    current.CopyOwner("cost-send"); // No field/book/resource result is assigned here.
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrepareAsync(mount: false);
        await fixture.CastIncomingAsync();
        Assert.Equal(1, witnessed);
        Assert.Equal(procId, fixture.Target.LegacyRandomSpell);
        Assert.Equal("cost-send", fixture.Target.Character.Attributes["NestedRandomCopy"]);
        Assert.Equal(procId == 20_220 ? new ulong[] { 90, 80 } : new ulong[] { 94, 88 },
            fixture.TargetStamina()); // Native Scurvy+6 supplies the otherwise zero-cost20360 witness.
        Assert.Single(fixture.ProcAnimations());
    }

    private static bool NestedRandomPrefixDemount(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && !IsLegacyStatusFlagSet(values, 0x32) && !IsLegacyStatusFlagSet(values, 0x16B));

    private sealed class NestedRandomPrefixFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> frames = new();
        private readonly StringWriter log = new();
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldDataCatalog data;
        private ushort selectedId;

        public NestedRandomPrefixFixture(ushort procId, byte stamina = 5, bool directRangeDenial = false,
            Func<NestedRandomPrefixFixture, uint, byte[], ValueTask>? onSend = null)
        {
            selectedId = procId;
            const string magnetic = "20220@@20220@@105@@MagneticLight@@1@@0@@0@@4@@0@@0@@15000@@0@@500@@1@@0@@2@@363@@0@@0@@0@@0@@0@@0@@0@@264@@0@@0@@0@@0@@10@@1@@0@@0@@0@@1@@5@@1@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@";
            const string ablaze = "20360@@20360@@1@@AblazeBlade@@1@@0@@0@@0@@0@@0@@600@@0@@1@@0@@0@@1@@-1@@0@@0@@0@@0@@0@@0@@0@@288@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@";
            var magneticFields = magnetic.Split("@@", StringSplitOptions.None);
            var ablazeFields = ablaze.Split("@@", StringSplitOptions.None);
            data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0,
                    damage: 1, range: 18, coldTime: 0, coolDown: 0),
                 CreateMagicRow(7_001, sort: 32, useMana: 0, useStamina: 0,
                    damage: 0, range: 18, coldTime: 0, coolDown: 0),
                 new MagicType(20_220, 20_220, magneticFields[3], magneticFields),
                 new MagicType(20_360, 20_360, ablazeFields[3], ablazeFields)]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, null, sendAsync: (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    frames.Enqueue((uid, bytes));
                    return onSend?.Invoke(this, uid, bytes) ?? ValueTask.CompletedTask;
                });
                var attrs = new Dictionary<string, string>
                {
                    ["DynamicID"] = "0", ["PkMode"] = "0", ["BattlePower"] = "120",
                    ["MaxHitpoints"] = "100000", ["Reborn"] = "2", ["Level"] = "120",
                    ["LegacyCombatGearPassiveRoll.20220"] = "0",
                    ["LegacyCombatGearPassiveRoll.20360"] = "0",
                };
                // Declared source ScurvyBomb raw input, never RandomSpell/cached status/result input.
                if (uid == TargetPlayerUid && procId == 20_360)
                    attrs["StatusFlags"] = "0/0/0/1/0/0/0/0/0/0/0/0/";
                var session = CreateSession(uid: uid, name: "NestedRandom" + uid,
                    x: uid == PlayerUid ? (directRangeDenial ? 829 : 830) : 831, y: 584,
                    mapId: 1_015, classCode: 13_005, reborn: 2, strength: 0, spirit: 0,
                    minHitPoints: 100_000, minMana: 500,
                    stamina: uid == TargetPlayerUid ? stamina : 100UL,
                    spellIds: uid == TargetPlayerUid ? [ThunderSpellId, 7_001, procId] : [ThunderSpellId],
                    attributes: attrs, playerItems: PlayerItemSnapshot.Empty);
                if (uid == TargetPlayerUid) session = session with { CombatGear = Gear(procId) };
                Assert.Equal((ushort)0, session.LegacyRandomSpell); // Actual constructor default, read only.
                sessions.Set(connections[uid], session);
            }
        }
        private static CombatGearSnapshot Gear(ushort id) => CombatGearSnapshot.Empty with
        {
            Count = 1,
            Items = [new CombatGearSnapshotItem(id == 20_220 ? 1_304U : 1_305U, 1, 0, 1, 0, 0, [[], []], [])],
        };
        public WorldSession Incoming => Current(PlayerUid);
        public WorldSession Target => Current(TargetPlayerUid);
        public WorldSession? TryTarget => sessions.TryGet(connections[TargetPlayerUid], out var owner) ? owner : null;
        private WorldSession Current(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var owner));
            Assert.NotNull(owner);
            return owner;
        }
        private WorldPacketHandlerContext Context(uint uid) => new(connections[uid],
            TextWriter.Synchronized(log), Current(uid), WorldData: data, Sessions: sessions,
            TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        private Task CastAsync(uint actor, ushort spell, uint target) => PacketHandlers.HandleAsync(Context(actor),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: actor, OpponentUid: target, X: 831, Y: 584,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spell)).ToFrameBytes())).AsTask();
        public async Task PrepareAsync(bool mount)
        {
            var start = frames.Count;
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(new MsgActionPacket
                { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var own = frames.ToArray().Skip(start).Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes).ToArray();
            var statusBytes = Assert.Single(own, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(1U, status.MagicAttack);
            var echo = Assert.Single(own, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.Type == (MsgActionType)251 && action.ObjId == PlayerUid);
            Assert.True(Array.IndexOf(own, echo) > Array.IndexOf(own, statusBytes));
            Assert.Equal(100_000, Incoming.Character.MinHitPoints);
            Assert.Empty(Target.PlayerItems.EquipmentItems);
            Assert.Equal(0U, Target.PhysicalStatusOverlay?.RightWeaponItemEffect ?? 0U);
            Assert.Equal(0U, Target.PhysicalStatusOverlay?.RidingCropItemId ?? 0U);
            if (mount)
            {
                Assert.Equal((ushort)20_220, selectedId);
                await CastAsync(TargetPlayerUid, 7_001, TargetPlayerUid);
                Assert.True(HasLegacyStatusFlag(Target.Character.Attributes, 0x32));
                Assert.True(Target.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
                Assert.Contains(ParseSpellAnimations(frames.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
                    .Select(frame => frame.Bytes).ToArray()), animation => animation.SpellId == 7_001);
            }
            else Assert.False(HasLegacyStatusFlag(Target.Character.Attributes, 0x32));
            frames.Clear(); // Positively completed public query/mount setup only.
        }
        public async Task CastIncomingAsync()
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await CastAsync(PlayerUid, ThunderSpellId, TargetPlayerUid);
        }
        public void SelectProc(ushort id, string denial)
        {
            var owner = Target;
            var books = owner.PlayerSpells.Spells.Where(book => book.Id is not (20_220 or 20_360)).ToList();
            if (denial != "missing-book")
                books.Add(new PlayerSpellSnapshotEntry(id, 0, 0, 0, 0, 0));
            var attrs = new Dictionary<string, string>(owner.Character.Attributes)
            {
                ["LegacyCombatGearPassiveRoll." + id] = denial == "rate-denial"
                    ? (id == 20_220 ? "9999" : "99") : "0",
            };
            selectedId = id;
            sessions.Set(connections[TargetPlayerUid], owner with
                { PlayerSpells = new PlayerSpellSnapshot(books), CombatGear = Gear(id),
                  Character = owner.Character with { Attributes = attrs } });
        }
        public void CopyOwner(string phase)
        {
            var owner = Target;
            sessions.Set(connections[TargetPlayerUid], owner with
                { Character = owner.Character with
                    { Attributes = new Dictionary<string, string>(owner.Character.Attributes)
                        { ["NestedRandomCopy"] = phase } } });
        }
        public WorldSession? RetireOwner(bool freshSameUid)
        {
            Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
            if (!freshSameUid) return null;
            // NEW raw native-player input, not a copied old record or a field reset.
            var fresh = CreateSession(uid: TargetPlayerUid, name: "FreshNestedRandom", x: 831, y: 584,
                classCode: 13_005, reborn: 2, minHitPoints: 43_210, minMana: 500, stamina: 5,
                spirit: 0, spellIds: [ThunderSpellId, 7_001, 20_220],
                attributes: new Dictionary<string, string>
                    { ["NestedRandomGeneration"] = "fresh", ["MaxHitpoints"] = "100000",
                      ["BattlePower"] = "120", ["PkMode"] = "0", ["LegacyCombatGearPassiveRoll.20220"] = "0" });
            fresh = fresh with { CombatGear = Gear(20_220) };
            sessions.Set(connections[TargetPlayerUid], fresh);
            return fresh;
        }
        public ulong[] TargetStamina() => frames.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
            .Select(frame => NestedProcStamina(frame.Bytes, TargetPlayerUid)).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        public LegacySpellAnimationPacket[] ProcAnimations() => ParseSpellAnimations(frames.ToArray()
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray())
            .Where(animation => animation.SpellId is 20_220 or 20_360).ToArray();
        public void Dispose() => log.Dispose();
    }
}
