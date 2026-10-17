using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyTimedStigmaSpellId = 1_095;
    private const int LegacyTimedStigmaFlag = 0x09;
    private const int LegacyTimedStatusUnrelatedFlag = 0x15;

    [Fact]
    public async Task HandlerAddStgmaFollowupStigmaCastRefreshesSharedTimer()
    {
        const uint duration = 60;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                LegacyTimedStigmaSpellId,
                sort: 6,
                useMana: 0,
                useStamina: 0,
                duration: duration,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [LegacyTimedStigmaSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyTimedStigmaFlag),
                ["LegacyStigmaExpiresAtUtc"] = DateTimeOffset.UtcNow.AddSeconds(5).ToString("O"),
                ["LegacyStigmaRemoveOnDead"] = bool.TrueString,
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: LegacyTimedStigmaSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyTimedStigmaFlag));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyStigmaExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));
        Assert.Equal(bool.TrueString, updatedSession.Character.Attributes["LegacyStigmaRemoveOnDead"]);
        Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is not null &&
            IsLegacyStatusFlagSet(entry.Values, LegacyTimedStigmaFlag)));
    }

    [Fact]
    public async Task RuntimeLoopExpiresAddStgmaNpcStigmaAndMagicShieldTogether()
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
        var session = CreateSession(attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(
                LegacyTimedStigmaFlag,
                LegacyMagicShieldFlag,
                LegacyTimedStatusUnrelatedFlag),
            ["LegacyStigmaExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            ["LegacyMagicShieldExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            ["LegacyStigmaRemoveOnDead"] = bool.TrueString,
            ["LegacyMagicShieldRemoveOnDead"] = bool.TrueString,
        });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(
                            current.Character.Attributes,
                            LegacyTimedStigmaFlag) &&
                        !HasLegacyStatusFlag(current.Character.Attributes, LegacyMagicShieldFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyStigmaExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyMagicShieldExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expired = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(
                expired.Character.Attributes,
                LegacyTimedStigmaFlag));
            Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, LegacyMagicShieldFlag));
            Assert.True(HasLegacyStatusFlag(
                expired.Character.Attributes,
                LegacyTimedStatusUnrelatedFlag));
            Assert.False(expired.Character.Attributes.ContainsKey("LegacyStigmaRemoveOnDead"));
            Assert.False(expired.Character.Attributes.ContainsKey("LegacyMagicShieldRemoveOnDead"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyTimedStigmaFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyMagicShieldFlag) &&
                IsLegacyStatusFlagSet(entry.Values, LegacyTimedStatusUnrelatedFlag)));
            Assert.Contains(
                "WorldServer expired legacy Stigma/MagicShield flags",
                log.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public void DeathClearsAddStgmaNpcStigmaMagicShieldAndTheirTimers()
    {
        var result = LegacyPlayerDeathState.ApplyDeath(
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyTimedStigmaFlag,
                    LegacyMagicShieldFlag),
                ["LegacyStigmaExpiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                ["LegacyMagicShieldExpiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                ["LegacyStigmaRemoveOnDead"] = bool.TrueString,
                ["LegacyMagicShieldRemoveOnDead"] = bool.TrueString,
            },
            DateTimeOffset.UtcNow,
            deadX: 430,
            deadY: 378);

        Assert.True(HasLegacyStatusFlag(result.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(result.Attributes, LegacyTimedStigmaFlag));
        Assert.False(HasLegacyStatusFlag(result.Attributes, LegacyMagicShieldFlag));
        Assert.False(result.Attributes.ContainsKey("LegacyStigmaExpiresAtUtc"));
        Assert.False(result.Attributes.ContainsKey("LegacyMagicShieldExpiresAtUtc"));
        Assert.False(result.Attributes.ContainsKey("LegacyStigmaRemoveOnDead"));
        Assert.False(result.Attributes.ContainsKey("LegacyMagicShieldRemoveOnDead"));
    }
}
