using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ImmortalForceAttachStatusSpellId = 14_710;

    [Fact]
    public async Task HandlerImmortalForce14710SelfCastEchoesInteractionAppliesStatusAndSelfAnimation()
    {
        const uint duration = 44;
        var worldData = CreateImmortalForceCatalog(duration);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateImmortalForceSession(stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreateImmortalForceQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(497, updatedSession.Character.MinMana);
        Assert.Equal("98", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyImmortalForceFlag));
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyImmortalForceRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyImmortalForceExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == ImmortalForceAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyImmortalForceFlag)));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == ImmortalForceAttachStatusSpellId);
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill ||
                    entry.Type == LegacyMsgUpdateDataType.SkillCountdown));
        Assert.DoesNotContain(sentSnapshot, frame => LegacyShowEquipmentPacket.TryParse(ReadFrame(frame), out _));
        Assert.Empty(ParseLegacySpellPackets(sentSnapshot));

        AssertImmortalForceAnimation(Assert.Single(ParseSpellAnimations(sentSnapshot)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ImmortalForce AttachStatus magic", output);
        Assert.Contains("statusFlag=220", output);
        Assert.Contains("duration=44", output);
        Assert.Contains("removeOnDead=True", output);
        Assert.Contains("source=AttachStatus.cs case 14710", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresImmortalForce14710StatusAndRemoveOnDeadAttribute()
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
        var session = CreateImmortalForceSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyImmortalForceFlag),
                ["LegacyImmortalForceExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyImmortalForceRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, LegacyImmortalForceFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyImmortalForceExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyImmortalForceRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, LegacyImmortalForceFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyImmortalForceFlag)));
            Assert.Contains("WorldServer expired legacy ImmortalForce flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateImmortalForceQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 838,
            Y: 592,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: ImmortalForceAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateImmortalForceCatalog(uint duration)
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                ImmortalForceAttachStatusSpellId,
                sort: 6,
                level: 2,
                useMana: 3,
                experience: 50,
                useStamina: 2,
                duration: duration,
                coldTime: 0));
    }

    private static WorldSession CreateImmortalForceSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(ImmortalForceAttachStatusSpellId, level: 2),
            ],
            attributes: attributes);
    }

    private static void AssertImmortalForceAnimation(LegacySpellAnimationPacket spell)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(838, spell.X);
        Assert.Equal(592, spell.Y);
        Assert.Equal(ImmortalForceAttachStatusSpellId, spell.SpellId);
        Assert.Equal(2, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }
}
