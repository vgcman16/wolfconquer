using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DefensiveStanceAttachStatusSpellId = 11_160;
    private const int DefensiveStanceStatusFlag = 0x7E;
    private const int DefensiveStanceRemovedRideStatusFlag = 0x32;
    private const uint DefensiveStanceDuration = 41;
    private const uint DefensiveStanceDamage = 77;

    [Fact]
    public async Task HandlerDefensiveStance11160SelfCastRemovesRideAppliesStatusCountdownAndSelfAnimation()
    {
        var worldData = CreateDefensiveStanceCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateDefensiveStanceSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DefensiveStanceRemovedRideStatusFlag),
            });
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
            ReadFrame(new MsgAttackPacket(CreateDefensiveStanceQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(492, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, DefensiveStanceStatusFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, DefensiveStanceRemovedRideStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyDefensiveStanceRemoveOnDead"));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyDefensiveStanceExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)DefensiveStanceDuration));

        Assert.Empty(ParseDefensiveStanceAttackPackets(sent));

        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, DefensiveStanceStatusFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, DefensiveStanceRemovedRideStatusFlag)));

        var defensiveUpdate = Assert.Single(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.DefensiveStance);
        Assert.Equal(checked((uint)DefensiveStanceStatusFlag), defensiveUpdate.Flag);
        Assert.Equal(DefensiveStanceDuration, defensiveUpdate.Time);
        Assert.Equal(DefensiveStanceDamage, defensiveUpdate.Damage);
        Assert.Equal(3U, defensiveUpdate.SpellLevel);

        AssertDefensiveStanceAnimation(Assert.Single(ParseSpellAnimations(sent)));
        Assert.Empty(ParseLegacySpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DefensiveStance AttachStatus magic", output);
        Assert.Contains("statusFlag=126", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("removedRide=True", output);
        Assert.Contains("source=AttachStatus.cs case 11160", output);
    }

    [Fact]
    public async Task HandlerDefensiveStance11160RecastRemovesStatusWithoutCountdownUpdate()
    {
        var worldData = CreateDefensiveStanceCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateDefensiveStanceSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DefensiveStanceStatusFlag),
                ["LegacyDefensiveStanceExpiresAtUtc"] = "2026-07-23T00:00:00.0000000+00:00",
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
            ReadFrame(new MsgAttackPacket(CreateDefensiveStanceQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, DefensiveStanceStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyDefensiveStanceExpiresAtUtc"));

        Assert.Empty(ParseDefensiveStanceAttackPackets(sent));
        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, DefensiveStanceStatusFlag)));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.DefensiveStance);
        AssertDefensiveStanceAnimation(Assert.Single(ParseSpellAnimations(sent)));

        var output = log.ToString();
        Assert.Contains("toggledOn=False", output);
        Assert.Contains("removedRide=False", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresDefensiveStance11160Status()
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
        var session = CreateDefensiveStanceSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DefensiveStanceStatusFlag),
                ["LegacyDefensiveStanceExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, DefensiveStanceStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyDefensiveStanceExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, DefensiveStanceStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, DefensiveStanceStatusFlag)));
            Assert.Contains("WorldServer expired legacy DefensiveStance flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateDefensiveStanceQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 832,
            Y: 588,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: DefensiveStanceAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateDefensiveStanceCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                DefensiveStanceAttachStatusSpellId,
                sort: 6,
                level: 3,
                useMana: 8,
                damage: DefensiveStanceDamage,
                experience: 50,
                useStamina: 3,
                duration: DefensiveStanceDuration,
                coldTime: 0));
    }

    private static WorldSession CreateDefensiveStanceSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(DefensiveStanceAttachStatusSpellId, level: 3),
            ],
            attributes: attributes);
    }

    private static void AssertDefensiveStanceAnimation(LegacySpellAnimationPacket spell)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(832, spell.X);
        Assert.Equal(588, spell.Y);
        Assert.Equal(DefensiveStanceAttachStatusSpellId, spell.SpellId);
        Assert.Equal(3, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseDefensiveStanceAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == DefensiveStanceAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
