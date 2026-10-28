using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort AutoHuntAttachStatusSpellId = 12_270;
    private const int AutoHuntStatusFlag = 0x94;
    private const uint AutoHuntDurationSeconds = 2_592_000;
    private const int AutoHuntStampSeconds = 8;

    [Fact]
    public async Task HandlerAutoHunt12270SelfCastAppliesStatusStampAnimationThenProgress()
    {
        var worldData = CreateAutoHuntAttachStatusCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateAutoHuntAttachStatusSession(stamina: 100);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateAutoHuntAttachStatusQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(496, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        AssertAutoHuntAttachStatusApplied(updatedSession, beforeCast, afterCast);
        Assert.False(updatedSession.Character.Attributes.ContainsKey("OnAutoHunt"));

        var attack = Assert.Single(ParseAutoHuntAttachStatusAttackPackets(sent));
        Assert.Equal(PlayerUid, attack.Query.Uid);
        Assert.Equal(PlayerUid, attack.Query.OpponentUid);

        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, AutoHuntStatusFlag)));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertAutoHuntAttachStatusAnimation(spell, expectedTargetCount: 1);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(sent));
        Assert.Equal(AutoHuntAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(35, spellUpdate.Experience);
        Assert.Equal(1, spellUpdate.Level);
        Assert.Empty(ParseAutoHuntPackets(sent));

        var interactionIndex = SingleFrameIndex(sent, frame =>
            MsgAttackPacket.TryParse(frame, out var packet) &&
            packet.Query.SpellId == AutoHuntAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var animationIndex = SingleFrameIndex(sent, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var spellUpdateIndex = SingleFrameIndex(sent, frame => LegacySpellPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);
        Assert.True(animationIndex < spellUpdateIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AutoHunt AttachStatus magic", output);
        Assert.Contains("selfTarget=True", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("stampSeconds=8", output);
        Assert.Contains("source=AttachStatus.cs case 12270", output);
    }

    [Fact]
    public async Task HandlerAutoHunt12270ActiveCastRemovesStatusAfterInteractionOnly()
    {
        var worldData = CreateAutoHuntAttachStatusCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateAutoHuntAttachStatusSession(
            stamina: 100,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(AutoHuntStatusFlag),
                [WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute] =
                    DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute] =
                    AutoHuntStampSeconds.ToString(CultureInfo.InvariantCulture),
                ["OnAutoHunt"] = "1",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateAutoHuntAttachStatusQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(496, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, AutoHuntStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute));
        Assert.Equal("1", updatedSession.Character.Attributes["OnAutoHunt"]);

        Assert.Single(ParseAutoHuntAttachStatusAttackPackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.Empty(ParseAutoHuntPackets(sent));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, AutoHuntStatusFlag)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AutoHunt AttachStatus toggle", output);
        Assert.Contains("removedStatusFlag=148", output);
    }

    [Fact]
    public async Task HandlerAutoHunt12270NonSelfTargetSendsEmptyAnimationThenProgressOnly()
    {
        var worldData = CreateAutoHuntAttachStatusCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateAutoHuntAttachStatusSession(stamina: 100);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 829,
            y: 585);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateAutoHuntAttachStatusQuery(TargetPlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, AutoHuntStatusFlag));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("OnAutoHunt"));
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, AutoHuntStatusFlag));

        Assert.Single(ParseAutoHuntAttachStatusAttackPackets(casterSent));
        Assert.Empty(ParseAutoHuntAttachStatusAttackPackets(targetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent.Concat(targetSent)).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertAutoHuntAttachStatusAnimation(casterAnimation, expectedTargetCount: 0);
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        AssertAutoHuntAttachStatusAnimation(targetAnimation, expectedTargetCount: 0);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(AutoHuntAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(35, spellUpdate.Experience);
        Assert.Empty(ParseLegacySpellPackets(targetSent));
        Assert.Empty(ParseAutoHuntPackets(casterSent.Concat(targetSent)));

        var interactionIndex = SingleFrameIndex(casterSent, frame =>
            MsgAttackPacket.TryParse(frame, out var packet) &&
            packet.Query.SpellId == AutoHuntAttachStatusSpellId);
        var animationIndex = SingleFrameIndex(casterSent, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var spellUpdateIndex = SingleFrameIndex(casterSent, frame => LegacySpellPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < animationIndex);
        Assert.True(animationIndex < spellUpdateIndex);

        var output = log.ToString();
        Assert.Contains("selfTarget=False", output);
        Assert.Contains("toggledOn=False", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresAutoHunt12270StatusWithoutTouchingPacketMode()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log);
        var session = CreateAutoHuntAttachStatusSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(AutoHuntStatusFlag),
                [WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute] =
                    now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute] =
                    AutoHuntStampSeconds.ToString(CultureInfo.InvariantCulture),
                ["OnAutoHunt"] = "1",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, AutoHuntStatusFlag) &&
                        !current.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute) &&
                        !current.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute);
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, AutoHuntStatusFlag));
            Assert.Equal("1", expiredSession.Character.Attributes["OnAutoHunt"]);
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, AutoHuntStatusFlag)));
            Assert.Contains("WorldServer expired legacy AutoHunt AttachStatus flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateAutoHuntAttachStatusQuery(uint opponentUid = PlayerUid)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: 829,
            Y: 585,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: AutoHuntAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateAutoHuntAttachStatusCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(AutoHuntAttachStatusSpellId, sort: 6, level: 0, experience: 50, duration: 35, coldTime: 0),
            CreateMagicRow(
                AutoHuntAttachStatusSpellId,
                sort: 6,
                level: 1,
                useMana: 4,
                experience: 50,
                useStamina: 3,
                duration: 35,
                coldTime: 0),
            CreateMagicRow(AutoHuntAttachStatusSpellId, sort: 6, level: 2, experience: 50, duration: 35, coldTime: 0));
    }

    private static WorldSession CreateAutoHuntAttachStatusSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(AutoHuntAttachStatusSpellId, level: 1),
            ],
            attributes: attributes);
    }

    private static void AssertAutoHuntAttachStatusApplied(
        WorldSession session,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, AutoHuntStatusFlag));
        Assert.Equal(
            AutoHuntStampSeconds.ToString(CultureInfo.InvariantCulture),
            session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyAutoHuntStampSecondsAttribute]);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyAutoHuntExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            checked((int)AutoHuntDurationSeconds));
    }

    private static void AssertAutoHuntAttachStatusAnimation(
        LegacySpellAnimationPacket spell,
        int expectedTargetCount)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(829, spell.X);
        Assert.Equal(585, spell.Y);
        Assert.Equal(AutoHuntAttachStatusSpellId, spell.SpellId);
        Assert.Equal(1, spell.SpellLevel);
        Assert.Equal(expectedTargetCount, spell.Targets.Count);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseAutoHuntAttachStatusAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == AutoHuntAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyAutoHuntPacket> ParseAutoHuntPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyAutoHuntPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyAutoHuntPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
