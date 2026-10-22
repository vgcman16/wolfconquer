using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort NestedProcMagnetic = 20_220;
    private const ushort NestedProcAblaze = 20_360;

    [Theory]
    [InlineData(1_015)]
    [InlineData(1_039)]
    public async Task HandlerNestedMagneticAdmissionCommitsBothNaturalCooldownsBeforeReentrantReceive(int map)
    {
        var sawNestedStamina = false;
        var sawNestedAnimation = false;
        using var fixture = new NestedProcAdmissionFixture(twoSided: true, map: map,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == PlayerUid && NestedProcStamina(bytes, PlayerUid) == 90)
                {
                    Assert.False(sawNestedStamina);
                    sawNestedStamina = true;
                    Assert.Equal(current.Now.AddSeconds(1), current.Target.MagicCooldowns![NestedProcMagnetic]);
                    Assert.Equal(100_000, current.Caster.Character.MinHitPoints);
                    Assert.Equal(100_000, current.Target.Character.MinHitPoints);
                }
                if (recipient == PlayerUid &&
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                    animation.SpellId == NestedProcMagnetic && animation.AttackerUid == PlayerUid)
                {
                    sawNestedAnimation = true;
                    Assert.Equal(current.Now.AddSeconds(1), current.Caster.MagicCooldowns![NestedProcMagnetic]);
                    Assert.Equal(current.Now.AddSeconds(1), current.Target.MagicCooldowns![NestedProcMagnetic]);
                }
                return ValueTask.CompletedTask;
            });
        if (map == 1_039)
            Assert.All(new[] { fixture.Caster, fixture.Target }, session =>
                Assert.Equal("1", session.Character.Attributes["DynamicID"]));
        await fixture.CastAsync();

        Assert.Equal(map != 1_039, sawNestedStamina);
        Assert.True(sawNestedAnimation);
        Assert.Equal(map == 1_039 ? "100" : "80", fixture.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(map == 1_039 ? "100" : "60", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Caster.Character.MinMana);
        Assert.Equal(500, fixture.Target.Character.MinMana);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Caster.MagicCooldowns![NestedProcMagnetic]);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcMagnetic]);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var casts = fixture.Animations(uid).Where(packet => packet.SpellId == NestedProcMagnetic).ToArray();
            Assert.Equal(2, casts.Length);
            Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), casts.Select(packet => packet.AttackerUid).Order());
            Assert.All(casts, packet => Assert.Equal(1, packet.BombFlag));
        }
        Assert.Equal(map == 1_039 ? Array.Empty<ulong>() : new ulong[] { 90, 80 },
            fixture.StaminaValues(PlayerUid));
        Assert.Equal(map == 1_039 ? Array.Empty<ulong>() : new ulong[] { 90, 80, 70, 60 },
            fixture.StaminaValues(TargetPlayerUid));
        fixture.AssertNoManaOrColdtimePackets();
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task HandlerNestedActualAblazeAdmissionUsesSourceAttackRangeBeforeItsZeroCosts(
        int distance, bool admitted)
    {
        using var fixture = new NestedProcAdmissionFixture(proc: NestedProcAblaze, distance: distance);
        await fixture.CastAsync();
        var casts = fixture.Animations(TargetPlayerUid).Where(packet => packet.SpellId == NestedProcAblaze).ToArray();
        Assert.Equal(admitted ? 1 : 0, casts.Length);
        if (admitted)
        {
            Assert.Equal(TargetPlayerUid, casts[0].AttackerUid);
            Assert.Equal(PlayerUid, Assert.Single(casts[0].Targets).Uid);
            Assert.True(Assert.Single(casts[0].Targets).Damage > 0);
            Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcAblaze]);
        }
        else
        {
            Assert.False(fixture.Target.MagicCooldowns?.ContainsKey(NestedProcAblaze) == true);
            Assert.Equal(100_000, fixture.Caster.Character.MinHitPoints);
        }
        Assert.Equal("100", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Target.Character.MinMana);
        Assert.Empty(fixture.StaminaValues(TargetPlayerUid));
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task HandlerNestedActualMagneticAdmissionRejectsInsufficientStaminaBeforeSpending(
        byte stamina, bool admitted)
    {
        using var fixture = new NestedProcAdmissionFixture(stamina: stamina);
        await fixture.CastAsync();
        Assert.Equal(admitted ? "0" : "9", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(admitted ? 1 : 0,
            fixture.Animations(TargetPlayerUid).Count(packet => packet.SpellId == NestedProcMagnetic));
        Assert.Equal(admitted, fixture.Target.MagicCooldowns?.ContainsKey(NestedProcMagnetic) == true);
        Assert.Equal(admitted ? new ulong[] { 0 } : Array.Empty<ulong>(), fixture.StaminaValues(TargetPlayerUid));
        Assert.Equal(500, fixture.Target.Character.MinMana);
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Fact]
    public async Task HandlerNestedSyntheticManaRejectionKeepsSourceSpentStaminaAndDoesNotUseGrace()
    {
        // Supplied20220/20360 rows have mana0. This explicitly synthetic mana50
        // branch proves CanUseSpell's ordering, not a claimed nonzero source row.
        using var fixture = new NestedProcAdmissionFixture(manaCost: 50, mana: 49, graceShape: true);
        await fixture.CastAsync();
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(new ulong[] { 90, 80 }, fixture.StaminaValues(TargetPlayerUid));
        Assert.Equal(49, fixture.Target.Character.MinMana);
        Assert.False(fixture.Target.MagicCooldowns?.ContainsKey(NestedProcMagnetic) == true);
        Assert.DoesNotContain(fixture.Animations(TargetPlayerUid), packet => packet.SpellId == NestedProcMagnetic);
        Assert.Equal(100_000, fixture.Caster.Character.MinHitPoints);
        Assert.True(fixture.Target.PlayerSpells.ContainsSpell(12_560));
        Assert.Equal(new ushort[] { 4, 5 }, fixture.Target.PlayerItems.EquipmentItems.Select(item => item.Position));
        Assert.All(fixture.Target.PlayerItems.EquipmentItems, item => Assert.Equal(622_000U, item.ItemId));
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Fact]
    public async Task HandlerNestedSyntheticManaReadsCurrentMpMasterAfterItsStaminaPublication()
    {
        var mutated = false;
        using var fixture = new NestedProcAdmissionFixture(manaCost: 100, mana: 80,
            onSend: (current, recipient, bytes) =>
            {
                if (!mutated && recipient == TargetPlayerUid && NestedProcStamina(bytes, TargetPlayerUid) == 90)
                {
                    Assert.Equal(80, current.Target.Character.MinMana);
                    Assert.False(current.Target.MagicCooldowns?.ContainsKey(NestedProcMagnetic) == true);
                    current.EquipMpMasterAfterStamina();
                    mutated = true;
                }
                return ValueTask.CompletedTask;
            });
        await fixture.CastAsync();
        Assert.True(mutated);
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(5, fixture.Target.Character.MinMana); // 100 - floor(100*25/100) =75; second attempt fails mana.
        Assert.Equal("preserved", fixture.Target.Character.Attributes["NestedProcSameUidMutation"]);
        var rune = Assert.Single(fixture.Target.PlayerItems.RuneItems);
        Assert.Equal(4_030_803U, rune.ItemId);
        Assert.Equal((ushort)101, rune.Position);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcMagnetic]);
        Assert.Single(fixture.Animations(TargetPlayerUid), packet => packet.SpellId == NestedProcMagnetic);
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Fact]
    public async Task HandlerNestedAdmissionDoesNotInheritGenericHeavensWrathCooldownException()
    {
        using var fixture = new NestedProcAdmissionFixture(heavensWrath: true);
        await fixture.CastAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xEC));
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcMagnetic]);
        Assert.Single(fixture.Animations(TargetPlayerUid), packet => packet.SpellId == NestedProcMagnetic);
        Assert.Equal(new ulong[] { 90, 80 }, fixture.StaminaValues(TargetPlayerUid));
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task HandlerNestedStaminaPublicationStopsForEitherOriginalIdentityLossWithoutRestoration(
        bool loseAttacker, bool replace)
    {
        var mutated = false;
        WorldSession? replacement = null;
        using var fixture = new NestedProcAdmissionFixture(onSend: (current, recipient, bytes) =>
        {
            if (!mutated && recipient == TargetPlayerUid && NestedProcStamina(bytes, TargetPlayerUid) == 90)
            {
                Assert.Equal(100_000, current.Caster.Character.MinHitPoints);
                Assert.Equal(100_000, current.Target.Character.MinHitPoints);
                replacement = current.Lose(loseAttacker ? PlayerUid : TargetPlayerUid, replace);
                mutated = true;
            }
            return ValueTask.CompletedTask;
        });
        await fixture.CastAsync();
        Assert.True(mutated);
        var lostUid = loseAttacker ? PlayerUid : TargetPlayerUid;
        if (replace)
        {
            Assert.Same(replacement, fixture.SessionOnConnection(lostUid));
            Assert.Equal(43_210, replacement!.Character.MinHitPoints);
            Assert.Null(replacement.MagicCooldowns);
        }
        else
        {
            Assert.Null(fixture.SessionOnConnection(lostUid));
        }
        var survivor = loseAttacker ? fixture.Target : fixture.Caster;
        Assert.Equal(100_000, survivor.Character.MinHitPoints);
        Assert.Equal(loseAttacker ? "90" : "100", survivor.Character.Attributes["Stamina"]);
        Assert.False(survivor.MagicCooldowns?.ContainsKey(NestedProcMagnetic) == true);
        Assert.Equal(new ulong[] { 90 }, fixture.StaminaValues(TargetPlayerUid));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            Assert.Empty(fixture.Animations(uid)); // Generic magic's existing nonempty-target guard.
            Assert.Empty(ParseDeathAttacks(fixture.Frames(uid)));
            Assert.DoesNotContain(ParseMsgUpdates(fixture.Frames(uid)), update => update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        }
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Theory]
    [InlineData(NestedProcMagnetic)]
    [InlineData(NestedProcAblaze)]
    public async Task HandlerNestedProcRechecksItsOwnDeadTargetWithoutRegatingTheOriginalIncomingReceive(ushort proc)
    {
        var changed = false;
        // Ablaze's supplied cost is0: synthetic stamina10 creates the same
        // documented CanUseSpell await. Magnetic uses its unchanged actual10.
        using var fixture = new NestedProcAdmissionFixture(proc: proc, useStamina: 10,
            onSend: (current, recipient, bytes) =>
            {
                if (!changed && recipient == TargetPlayerUid && NestedProcStamina(bytes, TargetPlayerUid) == 90)
                {
                    current.SetIncomingAttackerHitpoints(0);
                    changed = true;
                }
                return ValueTask.CompletedTask;
            });
        await fixture.CastAsync();
        Assert.True(changed);
        Assert.Equal(0, fixture.Caster.Character.MinHitPoints);
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![proc]);
        var nested = Assert.Single(fixture.Animations(TargetPlayerUid), packet => packet.SpellId == proc);
        Assert.Equal(TargetPlayerUid, nested.AttackerUid);
        Assert.Empty(nested.Targets); // Accepted proc still sends its source empty animation.
        if (proc == NestedProcMagnetic) AssertNestedMagneticSourcePrefix(fixture);
        var outer = Assert.Single(fixture.Animations(PlayerUid), packet => packet.SpellId == ThunderSpellId);
        Assert.Equal(TargetPlayerUid, Assert.Single(outer.Targets).Uid);
        Assert.True(Assert.Single(outer.Targets).Damage > 0);
        Assert.True(fixture.Target.Character.MinHitPoints < 100_000);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            Assert.Empty(ParseDeathAttacks(fixture.Frames(uid)));
            Assert.DoesNotContain(ParseMsgUpdates(fixture.Frames(uid)), update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        }
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Fact]
    public async Task HandlerNestedMagneticOwnBattlePowerZeroChanceStillPublishesAnEmptyAdmittedProc()
    {
        using var fixture = new NestedProcAdmissionFixture(attackerBattlePower: 170);
        await fixture.CastAsync();
        Assert.Equal(100_000, fixture.Caster.Character.MinHitPoints);
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcMagnetic]);
        var nested = Assert.Single(fixture.Animations(TargetPlayerUid), packet => packet.SpellId == NestedProcMagnetic);
        Assert.Empty(nested.Targets); // 100 - 2*(170-120) =0; no nested Receive/HP/push.
        AssertNestedMagneticSourcePrefix(fixture);
        Assert.True(fixture.Target.Character.MinHitPoints < 100_000);
        Assert.Equal((830L, 584L), (fixture.Caster.Character.X, fixture.Caster.Character.Y));
        var outer = Assert.Single(fixture.Animations(PlayerUid), packet => packet.SpellId == ThunderSpellId);
        Assert.Equal(TargetPlayerUid, Assert.Single(outer.Targets).Uid);
        fixture.AssertNoManaOrColdtimePackets();
    }

    [Theory]
    [InlineData(49U, true)]
    [InlineData(50U, false)]
    public async Task HandlerNestedMagneticUsesStrictIntegerBattlePowerBoundaryAfterAdmission(
        uint roll, bool hits)
    {
        // Source Core.Rate(int): 100 - 2*(145 - 120) =50, then50 > integer roll.
        using var fixture = new NestedProcAdmissionFixture(attackerBattlePower: 145,
            magneticBattlePowerRoll: roll);
        Assert.Equal("145", fixture.Caster.Character.Attributes["BattlePower"]);
        Assert.Equal("120", fixture.Target.Character.Attributes["BattlePower"]);
        Assert.Equal(roll.ToString(CultureInfo.InvariantCulture),
            fixture.Target.Character.Attributes["LegacyCombatGearMagneticBattlePowerRoll"]);
        Assert.False(fixture.Caster.Character.Attributes.ContainsKey("LegacyCombatGearMagneticBattlePowerRoll"));

        await fixture.CastAsync();

        Assert.Equal(new ulong[] { 90, 80 }, fixture.StaminaValues(TargetPlayerUid));
        Assert.Empty(fixture.StaminaValues(PlayerUid));
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Target.Character.MinMana);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![NestedProcMagnetic]);
        Assert.Null(fixture.Caster.MagicCooldowns);
        AssertNestedMagneticSourcePrefix(fixture);

        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var nested = Assert.Single(fixture.Animations(uid), packet => packet.SpellId == NestedProcMagnetic);
            Assert.Equal(TargetPlayerUid, nested.AttackerUid);
            Assert.Equal(1, nested.BombFlag);
            if (hits)
            {
                var hit = Assert.Single(nested.Targets);
                Assert.Equal(PlayerUid, hit.Uid);
                Assert.Equal(15_000U, hit.Damage); // Source replaces only the animation carrier after Receive.
                Assert.True(hit.Hit);
                Assert.Equal((fixture.Caster.Character.X, fixture.Caster.Character.Y),
                    ((long)hit.NewX, (long)hit.NewY));
                if (uid == PlayerUid)
                    Assert.Contains(ParseMsgUpdates(fixture.Frames(uid)), update => update.Uid == PlayerUid &&
                        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
                else
                    Assert.DoesNotContain(ParseMsgUpdates(fixture.Frames(uid)), update => update.Uid == PlayerUid &&
                        update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
            }
            else
            {
                Assert.Empty(nested.Targets); // Admitted source handler still sends its empty bomb1 animation.
                Assert.DoesNotContain(ParseMsgUpdates(fixture.Frames(uid)), update => update.Uid == PlayerUid &&
                    update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
            }
            Assert.Empty(ParseDeathAttacks(fixture.Frames(uid)));
        }
        if (hits)
            Assert.True(fixture.Caster.Character.MinHitPoints < 100_000);
        else
        {
            Assert.Equal(100_000, fixture.Caster.Character.MinHitPoints);
            Assert.Equal((830L, 584L), (fixture.Caster.Character.X, fixture.Caster.Character.Y));
        }
        var outer = Assert.Single(fixture.Animations(PlayerUid), packet => packet.SpellId == ThunderSpellId);
        Assert.Equal(TargetPlayerUid, Assert.Single(outer.Targets).Uid);
        Assert.True(Assert.Single(outer.Targets).Damage > 0);
        Assert.True(fixture.Target.Character.MinHitPoints < 100_000);
        fixture.AssertNoManaOrColdtimePackets();
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static void AssertNestedMagneticSourcePrefix(NestedProcAdmissionFixture fixture)
    {
        const int flag = 0x16B;
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, flag));
        var frames = fixture.Frames(TargetPlayerUid);
        var vector = SingleFrameIndex(frames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is { } values && IsLegacyStatusFlagSet(values, flag)));
        var archive = SingleFrameIndex(frames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == flag));
        Assert.Equal((ushort)2176, ReadFrame(frames[archive]).Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[archive]), out var packet));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ArchiveSkill, Value: 0,
            Flag: flag, Time: 10)], packet.Updates);
        var animation = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var spell) && spell.SpellId == NestedProcMagnetic);
        Assert.True(vector < archive && archive < animation);
    }

    private static ulong? NestedProcStamina(byte[] bytes, uint uid)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) || update.Uid != uid) return null;
        foreach (var entry in update.Updates)
            if (entry.Type == LegacyMsgUpdateDataType.Stamina) return entry.Value;
        return null;
    }

    private sealed class NestedProcAdmissionFixture : IDisposable
    {
        private const uint MpMaster = 4_030_803;
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> _order = new();
        private readonly StringWriter _log = new();
        private readonly ManualRuntimeTimeProvider _clock = new(
            new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldDataCatalog _world;
        private readonly WorldSession _originalCaster;

        public NestedProcAdmissionFixture(
            ushort proc = NestedProcMagnetic, bool twoSided = false, int map = 1_015,
            int distance = 1, byte stamina = 100, ushort? manaCost = null, long mana = 500,
            byte? useStamina = null, long attackerBattlePower = 120, uint? magneticBattlePowerRoll = null,
            bool graceShape = false, bool heavensWrath = false,
            Func<NestedProcAdmissionFixture, uint, byte[], ValueTask>? onSend = null)
        {
            var row = NestedProcSourceRow(proc);
            if (manaCost is not null || useStamina is not null)
            {
                var fields = row.Fields.ToArray();
                if (manaCost is { } syntheticMana) fields[9] = syntheticMana.ToString(CultureInfo.InvariantCulture);
                if (useStamina is { } syntheticStamina) fields[29] = syntheticStamina.ToString(CultureInfo.InvariantCulture);
                row = row with { Fields = fields };
            }
            _world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0,
                    damage: 1, range: 18, coldTime: 0, coolDown: 0), row],
                items: [new ItemType((int)MpMaster, "MPMaster(+3)", [MpMaster.ToString(), "MPMaster(+3)"])]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                _connections[uid] = new ConquerTcpConnection(null, null, sendAsync: (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    _order.Enqueue((uid, bytes));
                    return onSend?.Invoke(this, uid, bytes) ?? ValueTask.CompletedTask;
                });
                var ownsProc = uid == TargetPlayerUid || twoSided;
                var spells = new List<PlayerSpellSnapshotEntry> { CreatePlayerSpell(ThunderSpellId) };
                if (ownsProc) spells.Add(CreatePlayerSpell(proc));
                if (graceShape && uid == TargetPlayerUid) spells.Add(CreatePlayerSpell(12_560));
                var flags = heavensWrath && uid == TargetPlayerUid ? new[] { 0xEC } : Array.Empty<int>();
                var session = CreateSession(uid: uid, name: "NestedProc" + uid,
                    x: uid == PlayerUid ? 830 : 830 + distance, y: 584, mapId: map,
                    classCode: proc == NestedProcAblaze ? 14_005 : 13_005, reborn: 2,
                    strength: 0, spirit: 0, minHitPoints: 100_000, minMana: uid == TargetPlayerUid ? mana : 500,
                    stamina: uid == TargetPlayerUid ? stamina : 100UL, spellEntries: spells,
                    attributes: new Dictionary<string, string>
                    {
                        ["DynamicID"] = map == 1_039 ? "1" : "0", // Source BlockAttackMap permits this shared instance.
                        ["PkMode"] = "0", ["BattlePower"] = (uid == PlayerUid ? attackerBattlePower : 120).ToString(CultureInfo.InvariantCulture),
                        ["MaxHitpoints"] = "100000",
                        ["Reborn"] = "2", ["Level"] = "120", ["Angle"] = "0",
                        ["StatusFlags"] = BuildStatusFlags(flags),
                        ["LegacyCombatGearPassiveRoll." + proc] = "0",
                    },
                    playerItems: graceShape && uid == TargetPlayerUid
                        ? PlayerItemSnapshot.Empty with
                        {
                            EquipmentItems =
                            [
                                new PlayerItemSnapshotItem { Uid = uid + 101, ItemId = 622_000, Position = 4,
                                    Durability = 100, MaximumDurability = 100 },
                                new PlayerItemSnapshotItem { Uid = uid + 102, ItemId = 622_000, Position = 5,
                                    Durability = 100, MaximumDurability = 100 },
                            ],
                        }
                        : PlayerItemSnapshot.Empty);
                if (uid == TargetPlayerUid && magneticBattlePowerRoll is { } forcedBattlePowerRoll)
                    session = session with
                    {
                        Character = session.Character with
                        {
                            Attributes = new Dictionary<string, string>(session.Character.Attributes)
                            {
                                ["LegacyCombatGearMagneticBattlePowerRoll"] = forcedBattlePowerRoll.ToString(CultureInfo.InvariantCulture),
                            },
                        },
                    };
                if (ownsProc)
                    session = session with
                    {
                        CombatGear = CombatGearSnapshot.Empty with
                        {
                            Count = 1, Items = [new CombatGearSnapshotItem(
                                proc == NestedProcAblaze ? 1_305U : 1_304U, 1, 0, 1, 0, 0, [[], []], [])],
                        },
                    };
                _sessions.Set(_connections[uid], session);
            }
            _originalCaster = Caster;
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(Target.PlayerSpells, _world, proc, out _, out var selected));
            Assert.Equal(proc == NestedProcAblaze ? 1 : 105, (int)selected.Sort);
            Assert.Equal(proc == NestedProcAblaze ? 1 : 2, selected.Range);
            Assert.Equal(useStamina ?? (proc == NestedProcAblaze ? 0 : 10), selected.UseStamina);
            Assert.Equal(manaCost ?? 0, selected.UseMana);
            Assert.Equal(1_000, selected.ColdTime);
            Assert.Null(Caster.MagicCooldowns);
            Assert.Null(Target.MagicCooldowns);
        }

        public WorldSession Caster => SessionOnConnection(PlayerUid)!;
        public WorldSession Target => SessionOnConnection(TargetPlayerUid)!;
        public DateTimeOffset Now => _clock.GetUtcNow();
        public int ScheduledTimers => _clock.ScheduledTimerCount;
        public byte[][] Frames(uint uid) => _order.ToArray().Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray();
        public IReadOnlyList<LegacySpellAnimationPacket> Animations(uint uid) => ParseSpellAnimations(Frames(uid));
        public ulong[] StaminaValues(uint uid) => Frames(uid).Select(bytes => NestedProcStamina(bytes, uid))
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        public WorldSession? SessionOnConnection(uint uid) =>
            _sessions.TryGet(_connections[uid], out var session) ? session : null;

        public async Task CastAsync()
        {
            var context = new WorldPacketHandlerContext(
                    _connections[PlayerUid], TextWriter.Synchronized(_log), _originalCaster,
                    WorldData: _world, Sessions: _sessions, TimeProvider: _clock,
                    ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var beforeCaster = Caster;
            var beforeTarget = Target;
            var beforeQueryAt = Now;
            // Native1000 halves AFTER Magic.OnPlayer: freshMA0 + row1 becomes0.
            // Earn the source's unconditionalMA1 through real251, without changing raw inputs.
            await PacketHandlers.HandleAsync(context,
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var queryFrames = Frames(PlayerUid);
            var statusBytes = Assert.Single(queryFrames,
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            Assert.Equal(1U, status.MagicAttack);
            Assert.Equal(0U, status.MagicPercent);
            Assert.Equal(0U, status.SkillCStrike);
            Assert.Equal(0U, status.MagicDamageIncrease);
            var echoBytes = Assert.Single(queryFrames, bytes =>
                MsgActionPacket.TryParse(ReadFrame(bytes), out var echo) &&
                echo.Type == (MsgActionType)251 && echo.ObjId == PlayerUid);
            Assert.True(Array.IndexOf(queryFrames, echoBytes) > Array.IndexOf(queryFrames, statusBytes));
            Assert.True(_sessions.TryGet(_connections[PlayerUid], out var queriedCaster));
            Assert.NotNull(queriedCaster);
            Assert.Equal(beforeCaster.Character.Spirit, queriedCaster.Character.Spirit);
            Assert.Equal(beforeCaster.Character.Strength, queriedCaster.Character.Strength);
            Assert.Equal(beforeCaster.Character.MinHitPoints, queriedCaster.Character.MinHitPoints);
            Assert.Equal(beforeCaster.Character.MinMana, queriedCaster.Character.MinMana);
            Assert.Equal(beforeCaster.Character.Attributes["Stamina"], queriedCaster.Character.Attributes["Stamina"]);
            Assert.Equal(beforeCaster.Character.Attributes["BattlePower"], queriedCaster.Character.Attributes["BattlePower"]);
            Assert.Equal(beforeCaster.PlayerSpells, queriedCaster.PlayerSpells);
            Assert.Same(beforeTarget, Target);
            Assert.Equal(beforeQueryAt, Now);
            Assert.Empty(Animations(PlayerUid));
            Assert.Empty(Animations(TargetPlayerUid));
            context = context with { Session = queriedCaster };
            _order.Clear(); // Exclude only the positively proved, completed setup query.
            await PacketHandlers.HandleAsync(context,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 831, Y: 584,
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                    Effect: MsgAttackEffect.None, Unknown: 0, SpellId: ThunderSpellId)).ToFrameBytes()));
        }

        public void SetIncomingAttackerHitpoints(long hitpoints) =>
            _sessions.Set(_connections[PlayerUid], Caster with { Character = Caster.Character with { MinHitPoints = hitpoints } });

        public void EquipMpMasterAfterStamina()
        {
            var current = Target;
            _sessions.Set(_connections[TargetPlayerUid], current with
            {
                PlayerItems = current.PlayerItems with
                {
                    RuneItems = [new PlayerItemSnapshotItem
                        { Uid = 78_001, ItemId = MpMaster, Position = 101, Durability = 100, MaximumDurability = 100 }],
                },
                Character = current.Character with
                {
                    Attributes = new Dictionary<string, string>(current.Character.Attributes)
                        { ["NestedProcSameUidMutation"] = "preserved" },
                },
            });
        }

        public WorldSession? Lose(uint uid, bool replace)
        {
            if (!replace)
            {
                Assert.True(_sessions.TryRemove(_connections[uid], out _));
                return null;
            }
            var replacement = CreateSession(uid: uid + 80, name: "NestedProcReplacement", x: 831,
                minHitPoints: 43_210, attributes: new Dictionary<string, string> { ["Untouched"] = "yes" });
            _sessions.Set(_connections[uid], replacement);
            return replacement;
        }

        public void AssertNoManaOrColdtimePackets()
        {
            foreach (var (_, bytes) in _order.ToArray())
            {
                Assert.False(LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
                Assert.False(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                    update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.MaxMana),
                    "CanUseSpell nested mana deduction is silent; incoming spell has no mana cost.");
            }
        }

        public void Dispose() => _log.Dispose();

        private static MagicType NestedProcSourceRow(ushort spell)
        {
            var raw = spell == NestedProcMagnetic
                ? "20220@@20220@@105@@MagneticLight@@1@@0@@0@@4@@0@@0@@15000@@0@@500@@1@@0@@2@@363@@0@@0@@0@@0@@0@@0@@0@@264@@0@@0@@0@@0@@10@@1@@0@@0@@0@@1@@5@@1@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@"
                : "20360@@20360@@1@@AblazeBlade@@1@@0@@0@@0@@0@@0@@600@@0@@1@@0@@0@@1@@-1@@0@@0@@0@@0@@0@@0@@0@@288@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@";
            var fields = raw.Split("@@", StringSplitOptions.None);
            return new MagicType(spell, spell, fields[3], fields);
        }
    }
}
