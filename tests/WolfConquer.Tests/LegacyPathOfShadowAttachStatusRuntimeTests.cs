using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort PathOfShadowAttachStatusSpellId = 11_620;
    private const int PathOfShadowStatusFlag = 0x91;
    private const int PathOfShadowDurationSeconds = 2_592_000;
    private const int PathOfShadowClearedKineticSparkStatusFlag = 0x93;

    [Fact]
    public async Task HandlerPathOfShadow11620SelfCastAppliesLongStatusAndSelfAnimation()
    {
        var worldData = CreatePathOfShadowCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreatePathOfShadowSession(stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreatePathOfShadowQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(495, updatedSession.Character.MinMana);
        Assert.Equal("98", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, PathOfShadowStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyPathOfShadowRemoveOnDead"));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyPathOfShadowExpiresAtUtc",
            beforeCast,
            afterCast,
            PathOfShadowDurationSeconds);

        Assert.Empty(ParsePathOfShadowAttackPackets(sent));

        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, PathOfShadowStatusFlag)));

        AssertPathOfShadowAnimation(Assert.Single(ParseSpellAnimations(sent)));
        Assert.Empty(ParseLegacySpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PathOfShadow AttachStatus magic", output);
        Assert.Contains("statusFlag=145", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("duration=2592000", output);
        Assert.Contains("removeOnDead=False", output);
        Assert.Contains("source=AttachStatus.cs case 11620", output);
    }

    [Fact]
    public async Task HandlerPathOfShadow11620RecastRemovesPathAndKineticSpark()
    {
        var worldData = CreatePathOfShadowCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreatePathOfShadowSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(PathOfShadowStatusFlag, PathOfShadowClearedKineticSparkStatusFlag),
                ["LegacyPathOfShadowExpiresAtUtc"] = "2026-07-23T00:00:00.0000000+00:00",
                ["LegacyKineticSparkExpiresAtUtc"] = "2026-07-23T00:00:00.0000000+00:00",
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
            ReadFrame(new MsgAttackPacket(CreatePathOfShadowQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, PathOfShadowStatusFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, PathOfShadowClearedKineticSparkStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyPathOfShadowExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyKineticSparkExpiresAtUtc"));

        Assert.Empty(ParsePathOfShadowAttackPackets(sent));
        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, PathOfShadowStatusFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, PathOfShadowClearedKineticSparkStatusFlag)));
        AssertPathOfShadowAnimation(Assert.Single(ParseSpellAnimations(sent)));

        var output = log.ToString();
        Assert.Contains("toggledOn=False", output);
        Assert.Contains("clearedKineticSpark=True", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresPathOfShadow11620Status()
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
        var session = CreatePathOfShadowSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(PathOfShadowStatusFlag),
                ["LegacyPathOfShadowExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, PathOfShadowStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyPathOfShadowExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, PathOfShadowStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, PathOfShadowStatusFlag)));
            Assert.Contains("WorldServer expired legacy PathOfShadow flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreatePathOfShadowQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 840,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: PathOfShadowAttachStatusSpellId);
    }

    private static WorldDataCatalog CreatePathOfShadowCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                PathOfShadowAttachStatusSpellId,
                sort: 6,
                level: 1,
                useMana: 5,
                experience: 50,
                useStamina: 2,
                duration: 88,
                coldTime: 0));
    }

    private static WorldSession CreatePathOfShadowSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(PathOfShadowAttachStatusSpellId, level: 1),
            ],
            attributes: attributes);
    }

    private static void AssertPathOfShadowAnimation(LegacySpellAnimationPacket spell)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(PathOfShadowAttachStatusSpellId, spell.SpellId);
        Assert.Equal(1, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static IReadOnlyList<MsgAttackPacket> ParsePathOfShadowAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == PathOfShadowAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
