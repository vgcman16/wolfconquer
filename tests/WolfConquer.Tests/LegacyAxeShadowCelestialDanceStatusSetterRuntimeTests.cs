using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort AxeShadowStatusSetterSpellId = 15_940;
    private const ushort CelestialDanceStatusSetterSpellId = 7_030;
    private const int AxeShadowStatusSetterFlag = 0xFB;
    private const int CelestialDanceStatusSetterFlag = 0xFA;
    private const LegacyMsgUpdateDataType CelestialDanceStatusSetterUpdateType = LegacyMsgUpdateDataType.CelestialDance;

    [Fact]
    public async Task HandlerAxeShadow15940SelfStatusSetsFlagPowerArchiveAndSourceAnimation()
    {
        const uint duration = 41;
        const int damage3Power = 87;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                AxeShadowStatusSetterSpellId,
                sort: 90,
                useMana: 0,
                coldTime: 0,
                duration: duration,
                damage3: damage3Power));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [AxeShadowStatusSetterSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(AxeShadowStatusSetterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, AxeShadowStatusSetterFlag));
        Assert.Equal(
            damage3Power.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["AxeShadowPower"]);
        Assert.Equal(
            damage3Power.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["LegacyAxeShadowPower"]);
        Assert.True(updatedSession.Character.Attributes.ContainsKey("AxeShadowExpiresAtUtc"));
        Assert.True(updatedSession.Character.Attributes.ContainsKey("LegacyAxeShadowExpiresAtUtc"));

        var sentSnapshot = sent.ToArray();
        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(checked((ushort)session.Character.X), spell.X);
        Assert.Equal(checked((ushort)session.Character.Y), spell.Y);
        Assert.Equal(AxeShadowStatusSetterSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, AxeShadowStatusSetterFlag));

        var archiveUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AxeShadowStatusSetterFlag));
        var archiveEntry = Assert.Single(archiveUpdate.Updates);
        Assert.Equal(duration, archiveEntry.Time);
        Assert.Equal(0U, archiveEntry.Damage);
        Assert.Equal(0U, archiveEntry.SpellLevel);

        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == AxeShadowStatusSetterSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AxeShadowStatusSetterFlag)));
        var archiveIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AxeShadowStatusSetterFlag));
        Assert.True(animationIndex < statusIndex);
        Assert.True(statusIndex < archiveIndex);
    }

    [Fact]
    public async Task HandlerCelestialDance7030SelfStatusSetsFlagPowerDanceUpdateAndSourceAnimation()
    {
        const uint duration = 52;
        const int damageOnHuman = 2_345;
        const uint expectedPower = 345;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CelestialDanceStatusSetterSpellId,
                sort: 5,
                useMana: 0,
                coldTime: 0,
                duration: duration,
                damageOnHuman: damageOnHuman));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [CelestialDanceStatusSetterSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(CelestialDanceStatusSetterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CelestialDanceStatusSetterFlag));
        Assert.Equal(
            expectedPower.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["CelestialDancePower"]);
        Assert.Equal(
            expectedPower.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["LegacyCelestialDancePower"]);
        Assert.True(updatedSession.Character.Attributes.ContainsKey("CelestialDanceExpiresAtUtc"));
        Assert.True(updatedSession.Character.Attributes.ContainsKey("LegacyCelestialDanceExpiresAtUtc"));

        var sentSnapshot = sent.ToArray();
        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(checked((ushort)session.Character.X), spell.X);
        Assert.Equal(checked((ushort)session.Character.Y), spell.Y);
        Assert.Equal(CelestialDanceStatusSetterSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var danceUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == CelestialDanceStatusSetterUpdateType &&
                    entry.Flag == CelestialDanceStatusSetterFlag));
        var danceEntry = Assert.Single(danceUpdate.Updates);
        Assert.Equal(duration, danceEntry.Time);
        Assert.Equal(0U, danceEntry.Damage);
        Assert.Equal(0U, danceEntry.SpellLevel);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, CelestialDanceStatusSetterFlag));

        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CelestialDanceStatusSetterSpellId);
        var danceIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == CelestialDanceStatusSetterUpdateType &&
                    entry.Flag == CelestialDanceStatusSetterFlag));
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CelestialDanceStatusSetterFlag)));
        Assert.True(animationIndex < danceIndex);
        Assert.True(danceIndex < statusIndex);
    }

    private static MsgAttackQuery CreateSelfStatusSetterQuery(ushort spellId, WorldSession session)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: checked((ushort)session.Character.X),
            Y: checked((ushort)session.Character.Y),
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }
}
