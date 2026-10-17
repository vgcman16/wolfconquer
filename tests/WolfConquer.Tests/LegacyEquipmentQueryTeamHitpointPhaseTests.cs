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
    [Fact]
    public async Task DirectQueryTeamHpRealMembershipClampsConstructorWrapsSetterAndKeepsMemberMeshAcross1350()
    {
        using (var ordinary = new DirectQueryTeamHpFixture())
        {
            var rawMaximum = await ordinary.JoinTeamAsync();
            Assert.Equal(500U, rawMaximum.MaxHitpoints); // Raw Status max, not merely currentHP300.
            Assert.Equal(300U, rawMaximum.MinHitpoints);
        }
        using var fixture = new DirectQueryTeamHpFixture(largeOwner: true);
        var constructed = await fixture.JoinTeamAsync();
        Assert.Equal("TeamPhaseOwner", constructed.Name);
        Assert.Equal(65_535U, constructed.MaxHitpoints);
        Assert.Equal(65_535U, constructed.MinHitpoints);
        var start = fixture.Order.Count;
        await fixture.QueryAsync();
        var query = fixture.Order.ToArray().Skip(start).ToArray();
        var team = query.Where(frame => DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out _)).ToArray();
        Assert.Equal(4, team.Length); // AddRune setter + clamp setter; explicit pair has no team side effect.
        foreach (var frame in team)
        {
            Assert.True(DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out var entry));
            Assert.Equal(34_464U, entry.MaxHitpoints); // unchecked ushort100000, NOT constructor clamp65535.
            Assert.Equal(34_464U, entry.MinHitpoints);
        }
        DirectQueryTeamHpAssertPairs(query, PlayerUid,
            [(100_000U, 100_000U), (100_000U, 100_000U), (100_000U, 100_000U)]);
        start = fixture.Order.Count;
        await fixture.TransformAsync();
        var transformed = fixture.Order.ToArray().Skip(start).ToArray();
        var spawnFrame = Assert.Single(transformed, frame => frame.Recipient == TargetPlayerUid &&
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame.Bytes), out var spawn) &&
            spawn.GetNumericField(2) == PlayerUid);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(spawnFrame.Bytes), out var spawn));
        Assert.NotEqual((ulong)constructed.Mesh, spawn.GetNumericField(1));
        var transformTeam = transformed.Where(frame => DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out _)).ToArray();
        Assert.Equal(2, transformTeam.Length); // UpdateStatus setter only; explicit pair does not repeat team info.
        foreach (var frame in transformTeam)
        {
            Assert.True(DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out var entry));
            Assert.Equal(constructed.Name, entry.Name);
            Assert.Equal(constructed.Mesh, entry.Mesh); // Cached MemberInfo, NOT current transformed mesh.
            Assert.Equal(34_464U, entry.MaxHitpoints); // Setter runs before Status.Max becomes3000.
            Assert.Equal(3_000U, entry.MinHitpoints);
        }
        DirectQueryTeamHpAssertPairs(transformed, PlayerUid, [(100_000U, 3_000U), (3_000U, 3_000U)]);
    }

    [Fact]
    public async Task DirectQueryTeamHpPeerReboundBeforeSetterIsNotAdoptedAsTeamRecipient()
    {
        var armed = false;
        var mutated = 0;
        WorldSession? replacement = null;
        using var fixture = new DirectQueryTeamHpFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) &&
                Interlocked.CompareExchange(ref mutated, 1, 0) == 0)
                replacement = current.Rebind(TargetPlayerUid);
            return ValueTask.CompletedTask;
        });
        await fixture.JoinTeamAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.QueryAsync();
        Assert.Equal(1, mutated);
        Assert.NotNull(replacement);
        Assert.Same(replacement, fixture.Peer);
        DirectQueryTeamHpAssertReplacement(fixture.Peer);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        Assert.DoesNotContain(frames, frame => frame.Recipient == TargetPlayerUid &&
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame.Bytes), out _));
        Assert.Equal(2, frames.Count(frame => frame.Recipient == PlayerUid &&
            DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out _)));
        DirectQueryTeamHpAssertPairs(frames, PlayerUid, [(500U, 300U), (500U, 300U), (500U, 300U)]);
    }

    [Fact]
    public async Task DirectQueryTeamHpCapturedTeamEntrySurvivesCallbackButSelfPairCapturesCurrentMaximumAfterTeam()
    {
        var armed = false;
        var phase = 0;
        uint firstRecipient = 0;
        LegacyMsgUpdatePacket? resumedPair = null;
        using var fixture = new DirectQueryTeamHpFixture(onSend: async (current, recipient, bytes) =>
        {
            if (!armed) return;
            if (DirectQueryTeamHpIsInfo(bytes, PlayerUid, out var member))
            {
                if (Interlocked.CompareExchange(ref phase, 1, 0) == 0)
                {
                    firstRecipient = recipient;
                    Assert.Equal(500U, member.MaxHitpoints);
                    Assert.Equal(300U, member.MinHitpoints);
                    await current.TransformAsync(); // Real1350 changes cache/current, not a desired-state Set.
                    Assert.Equal((3_000U, 3_000L), DirectQueryTeamHpObserve(current.Owner));
                    Volatile.Write(ref phase, 2);
                    return;
                }
                if (Volatile.Read(ref phase) == 2)
                {
                    Assert.NotEqual(firstRecipient, recipient); // No assumption which team member was first.
                    Assert.Equal(500U, member.MaxHitpoints);
                    Assert.Equal(300U, member.MinHitpoints); // Captured before nested transform, not rebuilt.
                    Assert.Equal((3_000U, 3_000L), DirectQueryTeamHpObserve(current.Owner));
                    Volatile.Write(ref phase, 3);
                }
            }
            if (recipient != PlayerUid || !LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
                update.Uid != PlayerUid) return;
            if (Volatile.Read(ref phase) == 3 && update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints)
            {
                Assert.Equal(3_000UL, update.Updates[0].Value); // Pair capture is AFTER all team callbacks.
                Volatile.Write(ref phase, 4);
            }
            else if (Volatile.Read(ref phase) == 4 && update.Updates.Count == 2 &&
                update.Updates[1].Type == LegacyMsgUpdateDataType.Hitpoints)
            {
                resumedPair = update;
                Volatile.Write(ref phase, 5);
            }
        });
        await fixture.JoinTeamAsync();
        armed = true;
        await fixture.QueryAsync();
        Assert.Equal(5, Volatile.Read(ref phase));
        Assert.NotNull(resumedPair);
        Assert.Collection(resumedPair.Updates,
            max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal(3_000UL, max.Value); },
            hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal(3_000UL, hp.Value); });
        Assert.Equal((3_000U, 3_000L), DirectQueryTeamHpObserve(fixture.Owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectQueryTeamHpFineRainMemberSetterFencesQueryOwnerAndRequiredCasterAfterEveryTeamSend(
        bool loseRequiredCaster)
    {
        var armed = false;
        var mutated = 0;
        var mutationIndex = -1;
        WorldSession? replacement = null;
        using var fixture = new DirectQueryTeamHpFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && DirectQueryTeamHpIsInfo(bytes, TargetPlayerUid, out _) &&
                Interlocked.CompareExchange(ref mutated, 1, 0) == 0)
            {
                Assert.Equal((500U, 300L), DirectQueryTeamHpObserve(current.Peer));
                mutationIndex = current.Order.Count - 1;
                replacement = current.Rebind(loseRequiredCaster ? PlayerUid : TargetPlayerUid);
            }
            return ValueTask.CompletedTask;
        });
        await fixture.JoinTeamAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.FineRainAsync();
        Assert.Equal(1, mutated);
        Assert.True(mutationIndex >= start);
        Assert.NotNull(replacement);
        var replaced = loseRequiredCaster ? fixture.Owner : fixture.Peer;
        Assert.Same(replacement, replaced);
        DirectQueryTeamHpAssertReplacement(replaced);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        Assert.Single(frames, frame => DirectQueryTeamHpIsInfo(frame.Bytes, TargetPlayerUid, out _));
        Assert.DoesNotContain(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectQueryTeamHpIsHp(frame.Bytes, TargetPlayerUid));
        var after = fixture.Order.ToArray().Skip(mutationIndex + 1).ToArray();
        Assert.DoesNotContain(after, frame => frame.Recipient == TargetPlayerUid &&
            LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out var status) && status.Uid == TargetPlayerUid);
        if (loseRequiredCaster)
        {
            Assert.Equal((500U, 300L), DirectQueryTeamHpObserve(fixture.Peer));
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Peer).StampMilliseconds);
            Assert.DoesNotContain(after, frame => DirectQueryTeamHpIsHp(frame.Bytes, PlayerUid) ||
                DirectQueryTeamHpIsHp(frame.Bytes, TargetPlayerUid));
            Assert.DoesNotContain(after, frame => LegacySpellAnimationPacket.TryParse(ReadFrame(frame.Bytes), out var animation) &&
                animation.SpellId == FineRainAttachStatusSpellId);
        }
        else
        {
            // Losing one member skips that member, not the still-valid caster's source tail.
            Assert.Equal((600U, 600L), DirectQueryTeamHpObserve(fixture.Owner));
            var animation = Assert.Single(ParseSpellAnimations(after.Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes)), packet => packet.SpellId == FineRainAttachStatusSpellId);
            Assert.Equal(PlayerUid, Assert.Single(animation.Targets).Uid);
            Assert.DoesNotContain(after, frame => frame.Recipient == TargetPlayerUid &&
                LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame.Bytes), out _));
        }
    }

    [Fact]
    public async Task DirectQueryTeamHpFineRain100HasFourDistinctPairsAndTeamBeforeEachOfThreeSetters()
    {
        var armed = false;
        var observations = new ConcurrentQueue<(uint Uid, uint Maximum, long Hitpoints)>();
        using var fixture = new DirectQueryTeamHpFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                recipient == update.Uid && update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints)
            {
                var value = DirectQueryTeamHpObserve(current.Slot(recipient));
                observations.Enqueue((recipient, value.Maximum, value.Hitpoints));
            }
            return ValueTask.CompletedTask;
        });
        await fixture.JoinTeamAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.FineRainAsync();
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var teamFrames = frames.Where(frame => LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame.Bytes), out _)).ToArray();
        Assert.Equal(12, teamFrames.Length); // No invented final batched team refresh after the three real setters.
        foreach (var frame in teamFrames)
        {
            Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame.Bytes), out var packet));
            Assert.Equal(1, packet.Count);
            Assert.Single(packet.Entries);
        }
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            DirectQueryTeamHpAssertFineRainTimeline(frames, uid);
            Assert.Equal(new[] { (500U, 300L), (600U, 300L), (600U, 300L), (600U, 600L) },
                observations.Where(value => value.Uid == uid).Select(value => (value.Maximum, value.Hitpoints)).ToArray());
            Assert.Equal((600U, 600L), DirectQueryTeamHpObserve(fixture.Slot(uid)));
            Assert.Equal("100", fixture.Slot(uid).Character.Attributes["FineRainPower"]);
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Slot(uid)).StampMilliseconds);
        }
        var memberLastHp = Array.FindLastIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectQueryTeamHpIsHp(frame.Bytes, TargetPlayerUid));
        var casterFirstHp = Array.FindIndex(frames, frame => frame.Recipient == PlayerUid &&
            DirectQueryTeamHpIsHp(frame.Bytes, PlayerUid));
        Assert.True(memberLastHp >= 0 && casterFirstHp > memberLastHp);
        var animation = Assert.Single(ParseSpellAnimations(frames.Where(frame => frame.Recipient == PlayerUid)
            .Select(frame => frame.Bytes)), packet => packet.SpellId == FineRainAttachStatusSpellId);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), animation.Targets.Select(target => target.Uid).Order());
    }

    private static void DirectQueryTeamHpAssertFineRainTimeline((uint Recipient, byte[] Bytes)[] frames, uint uid)
    {
        var relevant = frames.Where(frame => DirectQueryTeamHpIsInfo(frame.Bytes, uid, out _) ||
            (frame.Recipient == uid && DirectQueryTeamHpIsHp(frame.Bytes, uid))).ToArray();
        Assert.Equal(14, relevant.Length); // 3 assignments *2 team deliveries +4 HP pairs.
        var phases = new[] { (500U, 300U), (600U, 300U), (600U, 300U), (600U, 600U) };
        var offset = 0;
        for (var index = 0; index < phases.Length; index++)
        {
            var phase = phases[index];
            if (index != 2) // Explicit SendUpdateHP has NO team side effect.
            {
                var team = relevant.Skip(offset).Take(2).ToArray();
                Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), team.Select(frame => frame.Recipient).Order());
                foreach (var frame in team)
                {
                    Assert.True(DirectQueryTeamHpIsInfo(frame.Bytes, uid, out var member));
                    Assert.Equal(phase.Item1, member.MaxHitpoints);
                    Assert.Equal(phase.Item2, member.MinHitpoints);
                }
                offset += 2;
            }
            DirectQueryTeamHpAssertPairs(relevant.Skip(offset).Take(2), uid, [phase]);
            offset += 2;
        }
        Assert.Equal(relevant.Length, offset);
    }

    private static void DirectQueryTeamHpAssertPairs(IEnumerable<(uint Recipient, byte[] Bytes)> frames,
        uint uid, (uint Maximum, uint Hitpoints)[] phases)
    {
        var packets = ParseMsgUpdates(frames.Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes))
            .Where(packet => packet.Uid == uid && packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(phases.Length * 2, packets.Length);
        for (var index = 0; index < phases.Length; index++)
        {
            var maximum = Assert.Single(packets[index * 2].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
            Assert.Equal((ulong)phases[index].Maximum, maximum.Value);
            var phase = phases[index];
            Assert.Collection(packets[index * 2 + 1].Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)phase.Hitpoints, hp.Value); });
        }
    }

    private static bool DirectQueryTeamHpIsInfo(byte[] bytes, uint uid, out LegacyTeamMemberInfoEntry member)
    {
        member = default!;
        if (!LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var packet) ||
            packet.Count != 1 || packet.Entries.Count != 1 || packet.Entries[0].Uid != uid) return false;
        member = packet.Entries[0];
        return true;
    }

    private static bool DirectQueryTeamHpIsHp(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == uid &&
        update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);

    private static (uint Maximum, long Hitpoints) DirectQueryTeamHpObserve(WorldSession session)
    {
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(session, out var maximum));
        return (maximum, session.Character.MinHitPoints);
    }

    private static void DirectQueryTeamHpAssertReplacement(WorldSession session)
    {
        Assert.Equal(77_777, session.Character.MinHitPoints);
        Assert.Equal(77, session.Character.MinMana);
        Assert.Equal("33", session.Character.Attributes["Stamina"]);
    }

    private sealed class DirectQueryTeamHpFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly TeamSessionStateStore _teams = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly StringWriter _log = new();

        public DirectQueryTeamHpFixture(bool largeOwner = false,
            Func<DirectQueryTeamHpFixture, uint, byte[], ValueTask>? onSend = null,
            bool includeThirdJoin = false)
        {
            const uint helmetId = 111_097, armorId = 130_097;
            ItemType HpItem(uint id, string name, ushort hp)
            {
                var row = DirectOverlayItem(id, name);
                var fields = row.Fields.ToArray();
                fields[18] = hp.ToString(CultureInfo.InvariantCulture);
                return row with { Fields = fields };
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, useStamina: 0,
                    damage: 0, duration: 60, coolDown: 0, coldTime: 0),
                ..(includeThirdJoin
                    ? new MagicType[] { CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 119,
                        experience: 1_000, coolDown: 0, coldTime: 0) }
                    : Array.Empty<MagicType>()),
            ], items:
            [
                HpItem(helmetId, "TeamPhaseHelmet", largeOwner ? (ushort)50_000 : (ushort)500),
                HpItem(armorId, "TeamPhaseArmor", 49_100),
            ], transformInfo:
            [
                new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                    "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]);
            PlayerItemSnapshot Items(bool large, uint itemUid) => PlayerItemSnapshot.Empty with
            {
                EquipmentItems = large
                    ? [new PlayerItemSnapshotItem { Uid = itemUid, ItemId = helmetId, Position = 1,
                           Durability = 100, MaximumDurability = 100 },
                       new PlayerItemSnapshotItem { Uid = itemUid + 1, ItemId = armorId, Position = 3,
                           Durability = 100, MaximumDurability = 100 }]
                    : [new PlayerItemSnapshotItem { Uid = itemUid, ItemId = helmetId, Position = 1,
                           Durability = 100, MaximumDurability = 100 }],
            };
            var owner = CreateSession(name: "TeamPhaseOwner", mapId: 1_000, classCode: 2_005, reborn: 2,
                strength: largeOwner ? 300 : 0, spirit: 0, minHitPoints: largeOwner ? 100_000 : 300,
                stamina: 100, spellIds: [TransformSpellId, FineRainAttachStatusSpellId],
                playerItems: Items(largeOwner, 98_301),
                attributes: includeThirdJoin ? new Dictionary<string, string>
                    { ["PkMode"] = "0", ["BattlePower"] = "120" } : null);
            // In the large-only fixture this peer shares the legal50000 helmet row;
            // its exact HP/max never supplies the large owner's literal assertion.
            var peer = CreateSession(uid: TargetPlayerUid, name: "TeamPhasePeer", mapId: 1_000, x: 832, y: 584,
                classCode: 2_005, reborn: 2, strength: 0, spirit: 0, minHitPoints: 300, stamina: 100,
                spellIds: includeThirdJoin ? [TransformSpellId, ThunderSpellId] : [TransformSpellId],
                playerItems: Items(false, 98_401),
                attributes: includeThirdJoin ? new Dictionary<string, string>
                    { ["PkMode"] = "0", ["BattlePower"] = "120" } : null);
            Assert.Equal(largeOwner ? 100_000U : 500U, LegacyMaxHitpointsCalculator.Calculate(
                owner.Character, owner.PlayerItems, CombatGearSnapshot.Empty, world).Total);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Null(peer.EquipmentHitpointsStatus);
            Assert.True(LegacyMagicRuntime.TryResolveSpell(world, FineRainAttachStatusSpellId, 0, out var rain));
            Assert.Equal(100U, rain.Damage); // Exact raw0 normalization, not a zero-power fiction.
            var actors = new Dictionary<uint, WorldSession> { [PlayerUid] = owner, [TargetPlayerUid] = peer };
            if (includeThirdJoin)
                actors[TargetPlayerUid + 1] = CreateSession(uid: TargetPlayerUid + 1, name: "TeamPhaseThird",
                    mapId: 1_000, x: 834, y: 584, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                    minHitPoints: 300, stamina: 100, spellIds: [], playerItems: Items(false, 98_501));
            foreach (var uid in actors.Keys)
            {
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes)); // Recipient is the original connection slot, including after rebind.
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            }
            foreach (var actor in actors)
                _sessions.Set(_connections[actor.Key], actor.Value);
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TeamStates: _teams, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => Slot(PlayerUid);
        public WorldSession Peer => Slot(TargetPlayerUid);
        public WorldSession Slot(uint uid)
        {
            Assert.True(_sessions.TryGet(_connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            _context with { Connection = _connections[uid], Session = Slot(uid) };

        public async Task<LegacyTeamMemberInfoEntry> JoinTeamAsync()
        {
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(new MsgTeamPacket(MsgTeamType.Create, PlayerUid).ToFrameBytes()));
            var constructor = Assert.Single(Order.ToArray(), frame => frame.Recipient == PlayerUid &&
                DirectQueryTeamHpIsInfo(frame.Bytes, PlayerUid, out _));
            Assert.True(DirectQueryTeamHpIsInfo(constructor.Bytes, PlayerUid, out var member));
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            Assert.Equal(2U, _teams.GetMemberCount(_connections[PlayerUid]));
            Assert.True(_teams.IsTeamMember(_connections[PlayerUid], TargetPlayerUid));
            return member;
        }

        public async Task WoundOwnerAsync()
        {
            var world = _context.WorldData!;
            var attacker = LegacyPlayerStatusCalculator.CalculateFields(Peer.Character, Peer.PlayerItems, world,
                checked((uint)Peer.Character.MinHitPoints), prestigeLevel: 0);
            var target = LegacyPlayerStatusCalculator.CalculateFields(Owner.Character, Owner.PlayerItems, world,
                checked((uint)Owner.Character.MinHitPoints), prestigeLevel: 0);
            Assert.Equal(1U, attacker[LegacyPlayerStatusPacket.MagicAttackIndex]);
            Assert.Equal(0U, attacker[LegacyPlayerStatusPacket.SkillCStrikeIndex]);
            Assert.Equal(0U, attacker[LegacyPlayerStatusPacket.MagicPercentIndex]);
            Assert.Equal(0U, attacker[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
            Assert.Equal(0U, target[LegacyPlayerStatusPacket.MagicDefenceIndex]);
            Assert.Equal(0U, target[LegacyPlayerStatusPacket.ItemBlessIndex]);
            Assert.Equal(0U, target[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: ThunderSpellId)).ToFrameBytes()));
        }

        public async Task JoinThirdMemberAsync()
        {
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid + 1),
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            Assert.Equal(3U, _teams.GetMemberCount(_connections[PlayerUid]));
        }

        public async Task QueryAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        public async Task TransformAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(CreateTransformAttack().ToFrameBytes()));
        public async Task FineRainAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
        public WorldSession Rebind(uint slot)
        {
            var replacement = CreateSession(uid: slot + 900, name: "TeamPhaseReplacement", mapId: 1_000,
                x: slot == PlayerUid ? 830 : 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: []);
            _sessions.Set(_connections[slot], replacement);
            return replacement;
        }
        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
