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
    private const ushort DirectRegistrationReplaceDragonSwing = 12_200;
    private const ushort DirectRegistrationReplaceThunder = 1_000;
    private const ushort DirectRegistrationReplaceUseRune = 21_730;
    private const int DirectRegistrationReplaceFlag = 160;
    private const string DirectRegistrationReplacePolicy = "LegacyStatusFlagRemoveOnDead.160";
    private const string DirectRegistrationReplacePower = "BlackJadePolicyReplace";

    [Fact]
    public async Task DirectNormalDeathRealBlackJadeAddSpellFlagReplacesFalseRegistrationWithTrueBeforeClearing()
    {
        using var fixture = new DirectRegistrationReplaceFixture();
        var initialOwnerItems = fixture.Owner.PlayerItems;
        var initialPeerItems = fixture.Peer.PlayerItems;
        // Native Status2301 reads existing cached MA; it does not perform an equipment query.
        Assert.Equal(0U, (await fixture.StatusAsync(PlayerUid)).MagicAttack);
        Assert.Equal(500U, (await fixture.StatusAsync(PlayerUid)).MaxHP);
        Assert.Equal(0U, (await fixture.StatusAsync(TargetPlayerUid)).MagicAttack);
        Assert.Equal(500U, (await fixture.StatusAsync(TargetPlayerUid)).MaxHP);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectRegistrationReplaceFlag));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey(DirectRegistrationReplacePolicy));
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        await fixture.PrepareNativeQueryInputsAsync();

        // Preserve the positive MA1 assertions AFTER the existing real251 acquisition.
        Assert.Equal(1U, (await fixture.StatusAsync(PlayerUid)).MagicAttack);
        Assert.Equal(500U, (await fixture.StatusAsync(PlayerUid)).MaxHP);
        Assert.Equal(1U, (await fixture.StatusAsync(TargetPlayerUid)).MagicAttack);
        Assert.Equal(500U, (await fixture.StatusAsync(TargetPlayerUid)).MaxHP);

        var attachStart = fixture.Order.Count;
        await fixture.CastAsync(PlayerUid, PlayerUid, DirectRegistrationReplaceDragonSwing);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectRegistrationReplaceFlag));
        Assert.Equal("false", fixture.Owner.Character.Attributes[DirectRegistrationReplacePolicy]);
        Assert.Equal("1", fixture.Owner.Character.Attributes["LegacyDragonSwingChance"]);
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(fixture.OwnerFramesSince(attachStart)),
            packet => packet.SpellId == DirectRegistrationReplaceDragonSwing);

        // One real outgoing magic receive reaches the attacker's Pirate Update.
        // This is NOT another12200 cast: that source handler would toggle OFF.
        var replacementStart = fixture.Order.Count;
        await fixture.CastAsync(PlayerUid, TargetPlayerUid, DirectRegistrationReplaceThunder);
        Assert.Equal(100, fixture.Peer.Character.MinHitPoints); // (QueriedMagic1 + raw799)/2 =400.
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectRegistrationReplaceFlag));
        Assert.Equal("true", fixture.Owner.Character.Attributes[DirectRegistrationReplacePolicy]);
        Assert.Equal("310", fixture.Owner.Character.Attributes[DirectRegistrationReplacePower]);

        var replacementFrames = fixture.OwnerFramesSince(replacementStart);
        var interactionIndex = SingleFrameIndex(replacementFrames, frame =>
            MsgAttackPacket.TryParse(frame, out var packet) &&
            packet.Query.Uid == PlayerUid && packet.Query.OpponentUid == TargetPlayerUid &&
            packet.Query.AttackType == MsgAttackType.None && packet.Query.SpellId == DirectRegistrationReplaceUseRune);
        var archiveIndex = SingleFrameIndex(replacementFrames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) && packet.Uid == PlayerUid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == DirectRegistrationReplaceFlag && entry.Time == 11 && entry.Damage == 310));
        var statusIndices = replacementFrames.Select((bytes, index) => (bytes, index))
            .Where(item => item.index > interactionIndex && item.index < archiveIndex &&
                DirectRegistrationReplaceVector(ReadFrame(item.bytes), flag: true, dead: false)).ToArray();
        Assert.Single(statusIndices); // Earlier FlashingName publication is outside this UseRune interval.
        var animationIndex = SingleFrameIndex(replacementFrames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == DirectRegistrationReplaceUseRune);
        var refreshIndex = SingleFrameIndex(replacementFrames, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndices[0].index && statusIndices[0].index < archiveIndex);
        Assert.True(archiveIndex < animationIndex && animationIndex < refreshIndex);
        var replacementAnimation = Assert.Single(ParseSpellAnimations(replacementFrames),
            packet => packet.SpellId == DirectRegistrationReplaceUseRune);
        Assert.Equal(PlayerUid, replacementAnimation.AttackerUid);
        Assert.Equal((ushort)0, replacementAnimation.X);
        Assert.Equal((ushort)0, replacementAnimation.Y);
        var replacementTarget = Assert.Single(replacementAnimation.Targets);
        Assert.Equal(PlayerUid, replacementTarget.Uid);
        Assert.Equal(0U, replacementTarget.Damage);
        Assert.True(replacementTarget.Hit); // Nonzero duration never takes the zero-duration toggle branch.
        Assert.Equal(MsgAttackEffect.None, replacementTarget.Effect);

        var deathStart = fixture.Order.Count;
        await fixture.CastAsync(TargetPlayerUid, PlayerUid, DirectRegistrationReplaceThunder);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectRegistrationReplaceFlag));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey(DirectRegistrationReplacePolicy));
        // Source ClearFlags removes registration, not unrelated chance/power storage.
        Assert.Equal("1", fixture.Owner.Character.Attributes["LegacyDragonSwingChance"]);
        Assert.Equal("310", fixture.Owner.Character.Attributes[DirectRegistrationReplacePower]);
        var deathFrames = fixture.OwnerFramesSince(deathStart);
        var clearIndex = SingleFrameIndex(deathFrames, frame =>
            DirectRegistrationReplaceVector(frame, flag: false, dead: false));
        var queryIndex = SingleFrameIndex(deathFrames, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var deadIndex = SingleFrameIndex(deathFrames, frame =>
            DirectRegistrationReplaceVector(frame, flag: false, dead: true));
        Assert.True(clearIndex < queryIndex && queryIndex < deadIndex);
        Assert.Contains(ParseMsgUpdates(deathFrames), packet => packet.Uid == PlayerUid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 0));
        Assert.Contains(deathFrames.Skip(deadIndex + 1), bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Death);
        Assert.Same(initialOwnerItems, fixture.Owner.PlayerItems);
        Assert.Same(initialPeerItems, fixture.Peer.PlayerItems);
        Assert.Equal(500, fixture.Owner.Character.MinMana);
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
    }

    private static bool DirectRegistrationReplaceVector(PacketFrame frame, bool flag, bool dead) =>
        LegacyMsgUpdatePacket.TryParse(frame, out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, DirectRegistrationReplaceFlag) == flag &&
            IsLegacyStatusFlagSet(values, 5) == dead);

    private sealed class DirectRegistrationReplaceFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly StringWriter _log = new();
        private readonly WorldPacketHandlerContext _context;

        public DirectRegistrationReplaceFixture()
        {
            const uint helmetId = 111_096;
            const int blackJadeId = 21_730_901;
            var helmet = DirectOverlayItem(helmetId, "RegistrationReplacementHelmet");
            var helmetFields = helmet.Fields.ToArray();
            helmetFields[18] = "500";
            helmetFields[19] = "500"; // Legal raw ItemMP preserves existing current500MP after real251.
            var useRune = CreateMagicRow(DirectRegistrationReplaceUseRune, sort: 6, useMana: 0,
                useStamina: 0, damage: 310, duration: 11, gDamage: 100, range: 18, coolDown: 0, coldTime: 0);
            var useRuneFields = useRune.Fields.ToArray();
            useRuneFields[3] = DirectRegistrationReplacePower;
            useRuneFields[16] = DirectRegistrationReplaceFlag.ToString(CultureInfo.InvariantCulture);
            useRune = useRune with { Name = DirectRegistrationReplacePower, Fields = useRuneFields };
            string[] jadeFields =
            [
                blackJadeId.ToString(CultureInfo.InvariantCulture), "2", "24",
                (DirectRegistrationReplaceUseRune * 1_000).ToString(CultureInfo.InvariantCulture),
                "0", "0", "600", "0", "0", "0", "10", "0", "0", "10", "701", "",
            ];
            var jade = new DaoqiDictType(blackJadeId, Type: 2, Level: 24,
                Values: jadeFields.Skip(3).Take(12).Select(value =>
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0).ToArray(),
                SourceFile: "fixture://registration-replacement-daoqi", LineNumber: 1,
                RawLine: string.Join("@@", jadeFields), Fields: jadeFields);
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(DirectRegistrationReplaceDragonSwing, sort: 6, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(DirectRegistrationReplaceThunder, sort: 1, useMana: 0, useStamina: 0,
                    damage: 799, range: 18, coolDown: 0, coldTime: 0),
                useRune,
            ], items: [helmet with { Fields = helmetFields }], daoqiDictTypes: [jade],
                combatGear: [CreateNativeOrdinaryMagicNeutralLoadedGear(701)]);
            WorldSession Session(uint uid, int job, ushort x, long hp, ushort[] spells) =>
                CreateSession(uid: uid, name: "Registration" + uid.ToString(CultureInfo.InvariantCulture),
                    mapId: 1_000, x: x, y: 584, classCode: job, reborn: 2, strength: 0, spirit: 0,
                    minHitPoints: hp, stamina: 100, spellIds: spells,
                    attributes: new Dictionary<string, string>
                    {
                        ["PkMode"] = "0", ["BattlePower"] = "100", ["LegacyCombatGearAttackSpellIndex"] = "0",
                    },
                    playerItems: PlayerItemSnapshot.Empty with
                    {
                        EquipmentItems = [new PlayerItemSnapshotItem
                        {
                            Uid = uid + 100, ItemId = helmetId, Position = 1,
                            Durability = 100, MaximumDurability = 100,
                        }],
                    });
            var owner = Session(PlayerUid, 7_005, 830, 300,
                [DirectRegistrationReplaceDragonSwing, DirectRegistrationReplaceThunder, DirectRegistrationReplaceUseRune]) with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1,
                    Items = [new CombatGearSnapshotItem(701, 1, 0, 1, 0, 0, [[], []], [(uint)blackJadeId])],
                },
            };
            var peer = Session(TargetPlayerUid, 2_005, 832, 500, [DirectRegistrationReplaceThunder]);
            Assert.Equal("500", helmetFields[18]);
            Assert.Equal("100", useRune.Fields[12]);
            Assert.Equal("160", useRune.Fields[16]);
            Assert.Equal("11", useRune.Fields[13]);
            Assert.Equal(DirectRegistrationReplaceUseRune * 1_000, jade.Values[0]);
            Assert.Equal(7, owner.Character.Class / 1_000);
            Assert.Equal(701U, Assert.Single(Assert.IsType<CombatGearSnapshot>(owner.CombatGear).Items).ItemID);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: (memory, token) =>
                    {
                        Order.Enqueue((uid, memory.ToArray()));
                        return ValueTask.CompletedTask;
                    });
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], peer);
            var time = new DirectOverlayFrozenClock();
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: time,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(time, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => Slot(PlayerUid);
        public WorldSession Peer => Slot(TargetPlayerUid);
        private WorldSession Slot(uint uid)
        {
            Assert.True(_sessions.TryGet(_connections[uid], out var session));
            return Assert.IsType<WorldSession>(session);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            _context with { Connection = _connections[uid], Session = Slot(uid) };
        public byte[][] OwnerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == PlayerUid).Select(frame => frame.Bytes).ToArray();
        public async Task<LegacyPlayerStatusPacket> StatusAsync(uint uid)
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(uid), ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == uid &&
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }
        public async Task PrepareNativeQueryInputsAsync()
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PrepareNativeOrdinaryMagicInputQueryAsync(Context(uid),
                    () => Order.ToArray().Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray(),
                    expectedMagicAttack: 1, expectedMaximumHitpoints: 500, expectedMaximumMana: 500);
        }
        public async Task CastAsync(uint caster, uint target, ushort spell) =>
            await PacketHandlers.HandleAsync(Context(caster), ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: caster, OpponentUid: target, X: checked((ushort)Slot(target).Character.X),
                Y: checked((ushort)Slot(target).Character.Y), AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: spell)).ToFrameBytes()));
        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
