using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CrackStarAttachStatusSpellId = 15_380;
    private const int CrackStarStatusFlag = 0xE6;
    private const ulong WindWalkerOnMeleeAttackMainFlag = 8;

    [Fact]
    public async Task HandlerCrackStar15380WindWalkerMeleeSelfCastAppliesTimedStatusAndSelfAnimation()
    {
        const uint duration = 23;
        var worldData = CreateCrackStarCatalog(duration);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateCrackStarSession(
            classCode: 16_005,
            mainFlag: WindWalkerOnMeleeAttackMainFlag,
            stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(CrackStarAttachStatusSpellId, session)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CrackStarStatusFlag));
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyCrackStarRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyCrackStarExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));

        var attack = Assert.Single(ParseCrackStarAttackPackets(sent));
        Assert.Equal(PlayerUid, attack.Query.Uid);
        Assert.Equal(PlayerUid, attack.Query.OpponentUid);
        Assert.Equal(CrackStarAttachStatusSpellId, attack.Query.SpellId);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CrackStarStatusFlag)));
        Assert.Single(statusUpdate.Updates);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(0, spell.X);
        Assert.Equal(0, spell.Y);
        Assert.Equal(CrackStarAttachStatusSpellId, spell.SpellId);
        Assert.Equal(1, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.Empty(ParseLegacySpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CrackStar AttachStatus magic", output);
        Assert.Contains("statusFlag=230", output);
        Assert.Contains("source=AttachStatus.cs case 15380", output);
    }

    [Theory]
    [InlineData(142, WindWalkerOnMeleeAttackMainFlag, "isWindWalker=False")]
    [InlineData(16_005, 0UL, "onMeleeAttack=False")]
    public async Task HandlerCrackStar15380RequiresWindWalkerMeleeMainFlag(
        int classCode,
        ulong mainFlag,
        string expectedLog)
    {
        var worldData = CreateCrackStarCatalog(duration: 23);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateCrackStarSession(
            classCode,
            mainFlag,
            stamina: 100);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(CrackStarAttachStatusSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, CrackStarStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyCrackStarExpiresAtUtc"));
        Assert.Empty(ParseCrackStarAttackPackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains("WorldServer skipped legacy CrackStar AttachStatus magic", log.ToString());
        Assert.Contains(expectedLog, log.ToString());
    }

    [Fact]
    public async Task RuntimeLoopExpiresCrackStar15380Status()
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
        var session = CreateCrackStarSession(
            classCode: 16_005,
            mainFlag: WindWalkerOnMeleeAttackMainFlag,
            attributes: new Dictionary<string, string>
            {
                ["MainFlag"] = WindWalkerOnMeleeAttackMainFlag.ToString(),
                ["StatusFlags"] = BuildStatusFlags(CrackStarStatusFlag),
                ["LegacyCrackStarExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyCrackStarRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, CrackStarStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyCrackStarExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyCrackStarRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, CrackStarStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, CrackStarStatusFlag)));
            Assert.Contains("WorldServer expired legacy CrackStar flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static WorldDataCatalog CreateCrackStarCatalog(uint duration)
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CrackStarAttachStatusSpellId,
                sort: 6,
                level: 1,
                useMana: 7,
                experience: 50,
                useStamina: 4,
                duration: duration,
                coldTime: 0));
    }

    private static WorldSession CreateCrackStarSession(
        int classCode,
        ulong mainFlag,
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var sessionAttributes = attributes is null
            ? new Dictionary<string, string>
            {
                ["MainFlag"] = mainFlag.ToString(),
            }
            : new Dictionary<string, string>(attributes);
        return CreateSession(
            classCode: classCode,
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(CrackStarAttachStatusSpellId, level: 1),
            ],
            attributes: sessionAttributes);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseCrackStarAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == CrackStarAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
