using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DragonSwingAttachStatusSpellId = 12_200;
    private const int DragonSwingStatusFlag = 0xA0;
    private const uint DragonSwingDurationSeconds = 2_592_000;
    private const uint DragonSwingUpdateDamage = 33;

    [Fact]
    public async Task HandlerDragonSwing12200SelfCastAppliesStatusUpdateChanceProgressAndAnimation()
    {
        var worldData = CreateDragonSwingCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonSwingSession(stamina: 100);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateDragonSwingQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(497, updatedSession.Character.MinMana);
        Assert.Equal("98", updatedSession.Character.Attributes["Stamina"]);
        AssertDragonSwingApplied(updatedSession, beforeCast, afterCast);

        var attack = Assert.Single(ParseDragonSwingAttackPackets(sent));
        Assert.Equal(PlayerUid, attack.Query.Uid);
        Assert.Equal(PlayerUid, attack.Query.OpponentUid);

        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, DragonSwingStatusFlag)));

        var dragonSwingUpdate = Assert.Single(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.DragonSwing);
        Assert.Equal(0UL, dragonSwingUpdate.Value);
        Assert.Equal((uint)DragonSwingStatusFlag, dragonSwingUpdate.Flag);
        Assert.Equal(45U, dragonSwingUpdate.Time);
        Assert.Equal(DragonSwingUpdateDamage, dragonSwingUpdate.Damage);
        Assert.Equal(1U, dragonSwingUpdate.SpellLevel);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(sent));
        Assert.Equal(DragonSwingAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(45, spellUpdate.Experience);
        Assert.Equal(1, spellUpdate.Level);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertDragonSwingAnimation(spell, expectedTargetCount: 1);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var interactionIndex = SingleFrameIndex(sent, frame =>
            MsgAttackPacket.TryParse(frame, out var packet) &&
            packet.Query.SpellId == DragonSwingAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var dragonSwingUpdateIndex = SingleFrameIndex(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.DragonSwing));
        var spellUpdateIndex = SingleFrameIndex(sent, frame => LegacySpellPacket.TryParse(frame, out _));
        var animationIndex = SingleFrameIndex(sent, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < dragonSwingUpdateIndex);
        Assert.True(dragonSwingUpdateIndex < spellUpdateIndex);
        Assert.True(spellUpdateIndex < animationIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DragonSwing AttachStatus magic", output);
        Assert.Contains("selfTarget=True", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("chance=30", output);
        Assert.Contains("source=AttachStatus.cs case 12200", output);
    }

    [Fact]
    public async Task HandlerDragonSwing12200ActiveCastRemovesStatusAfterInteractionWithoutProgressOrAnimation()
    {
        var worldData = CreateDragonSwingCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonSwingSession(
            stamina: 100,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(DragonSwingStatusFlag),
                ["LegacyDragonSwingExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute] = "30",
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
            ReadFrame(new MsgAttackPacket(CreateDragonSwingQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(497, updatedSession.Character.MinMana);
        Assert.Equal("98", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, DragonSwingStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute));

        Assert.Single(ParseDragonSwingAttackPackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.DragonSwing));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, DragonSwingStatusFlag)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DragonSwing AttachStatus toggle", output);
        Assert.Contains("removedStatusFlag=160", output);
    }

    [Fact]
    public async Task HandlerDragonSwing12200AliveDragonWarParticipantOnlyEchoesInteraction()
    {
        var worldData = CreateDragonSwingCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonSwingSession(
            stamina: 100,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyTournamentInTournament"] = "1",
                ["LegacyTournamentType"] = "DragonWar",
                ["LegacyTournamentProcess"] = "Alive",
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
            ReadFrame(new MsgAttackPacket(CreateDragonSwingQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(497, updatedSession.Character.MinMana);
        Assert.Equal("98", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, DragonSwingStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute));

        Assert.Single(ParseDragonSwingAttackPackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.StatusFlag or LegacyMsgUpdateDataType.DragonSwing));
        Assert.Contains("activeDragonWarParticipant=True", log.ToString());
    }

    [Fact]
    public async Task HandlerDragonSwing12200NonSelfTargetSendsEmptyAnimationAndProgressOnly()
    {
        var worldData = CreateDragonSwingCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateDragonSwingSession(stamina: 100);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateDragonSwingQuery(TargetPlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, DragonSwingStatusFlag));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute));
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DragonSwingStatusFlag));

        Assert.Single(ParseDragonSwingAttackPackets(casterSent));
        Assert.Empty(ParseDragonSwingAttackPackets(targetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent.Concat(targetSent)).SelectMany(update => update.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.StatusFlag or LegacyMsgUpdateDataType.DragonSwing);

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertDragonSwingAnimation(casterAnimation, expectedTargetCount: 0);
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        AssertDragonSwingAnimation(targetAnimation, expectedTargetCount: 0);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(DragonSwingAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(45, spellUpdate.Experience);
        Assert.Empty(ParseLegacySpellPackets(targetSent));

        var output = log.ToString();
        Assert.Contains("selfTarget=False", output);
        Assert.Contains("toggledOn=False", output);
    }

    private static MsgAttackQuery CreateDragonSwingQuery(uint opponentUid = PlayerUid)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: 831,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: DragonSwingAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateDragonSwingCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(DragonSwingAttachStatusSpellId, sort: 6, level: 0, experience: 50, duration: 45, coldTime: 0),
            CreateMagicRow(
                DragonSwingAttachStatusSpellId,
                sort: 6,
                level: 1,
                useMana: 3,
                damage: 3_050,
                experience: 50,
                useStamina: 2,
                duration: 45,
                coldTime: 0),
            CreateMagicRow(DragonSwingAttachStatusSpellId, sort: 6, level: 2, experience: 50, duration: 45, coldTime: 0));
    }

    private static WorldSession CreateDragonSwingSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(DragonSwingAttachStatusSpellId, level: 1),
            ],
            attributes: attributes);
    }

    private static void AssertDragonSwingApplied(
        WorldSession session,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, DragonSwingStatusFlag));
        Assert.Equal("30", session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute]);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            "LegacyDragonSwingExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)DragonSwingDurationSeconds));
    }

    private static void AssertDragonSwingAnimation(
        LegacySpellAnimationPacket spell,
        int expectedTargetCount)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(831, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(DragonSwingAttachStatusSpellId, spell.SpellId);
        Assert.Equal(1, spell.SpellLevel);
        Assert.Equal(expectedTargetCount, spell.Targets.Count);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseDragonSwingAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == DragonSwingAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
