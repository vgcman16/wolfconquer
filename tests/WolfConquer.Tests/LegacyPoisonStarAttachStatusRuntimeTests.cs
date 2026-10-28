using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort PoisonStarAttachStatusSpellId = 6_002;
    private const int PoisonStarStatusFlag = 0x77;
    private const string PoisonStarSystemMessage = "Poison star activated. You will not be able to use drugs for 30 seconds.";

    [Fact]
    public async Task HandlerPoisonStar6002VisiblePlayerSuccessAppliesTargetStatusMessageAndProgress()
    {
        var worldData = CreatePoisonStarCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreatePoisonStarSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyPoisonStarRateRollAttribute] = "0",
            });
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePoisonStarQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(497, updatedCasterSession.Character.MinMana);
        Assert.Equal("98", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, PoisonStarStatusFlag));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        AssertPoisonStarApplied(updatedTargetSession, beforeCast, afterCast);

        Assert.Contains(ParseMsgUpdates(casterSent), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, PoisonStarStatusFlag)));
        Assert.Contains(ParseMsgUpdates(targetSent), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, PoisonStarStatusFlag)));

        Assert.Contains(
            ParsePoisonStarMessages(targetSent),
            message =>
                message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content.Contains(PoisonStarSystemMessage));
        Assert.DoesNotContain(
            ParsePoisonStarMessages(casterSent),
            message => message.Content.Contains(PoisonStarSystemMessage));

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            AssertPoisonStarAnimation(spell, expectedTargetCount: 1);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(PoisonStarAttachStatusSpellId, spellUpdate.Id);
        Assert.Empty(ParseLegacySpellPackets(targetSent));
        Assert.Empty(ParsePoisonStarAttackPackets(casterSent));
        Assert.DoesNotContain(targetSent, frame => LegacyShowEquipmentPacket.TryParse(ReadFrame(frame), out _));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PoisonStar AttachStatus magic", output);
        Assert.Contains("targetApplied=True", output);
        Assert.Contains("successRollPassed=True", output);
        Assert.Contains("spellProgressDamage=250", output);
        Assert.Contains("source=AttachStatus.cs case 6002", output);
    }

    [Fact]
    public async Task HandlerPoisonStar6002VisiblePlayerMissSendsMissAnimationAndProgressOnly()
    {
        var worldData = CreatePoisonStarCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreatePoisonStarSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyPoisonStarRateRollAttribute] = "99",
            });
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePoisonStarQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, PoisonStarStatusFlag));
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey("LegacyPoisonStarExpiresAtUtc"));
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey("LegacyPoisonStarRemoveOnDead"));
        Assert.Empty(ParsePoisonStarMessages(targetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent.Concat(targetSent)),
            update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, PoisonStarStatusFlag)));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        AssertPoisonStarAnimation(spell, expectedTargetCount: 1);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(PoisonStarAttachStatusSpellId, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(PoisonStarAttachStatusSpellId, spellUpdate.Id);
        Assert.Empty(ParseLegacySpellPackets(targetSent));

        var output = log.ToString();
        Assert.Contains("targetApplied=False", output);
        Assert.Contains("successRollPassed=False", output);
        Assert.Contains("spellProgressDamage=250", output);
    }

    [Fact]
    public async Task HandlerPoisonStar6002SturdinessAvoidanceSendsEmptyAnimationWithoutProgress()
    {
        const uint sturdinessRuneItemId = 300_109;
        var worldData = CreatePoisonStarCatalog(
            [CreateItemType((int)sturdinessRuneItemId, "Sturdiness(+9)")]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreatePoisonStarSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyPoisonStarRateRollAttribute] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = sturdinessRuneItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyPoisonStarSturdinessRollAttribute] = "0",
            });
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
            ReadFrame(new MsgAttackPacket(CreatePoisonStarQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(497, updatedCasterSession.Character.MinMana);
        Assert.Equal("98", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, PoisonStarStatusFlag));
        Assert.Empty(ParsePoisonStarMessages(targetSent));
        Assert.Empty(ParseLegacySpellPackets(casterSent));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        AssertPoisonStarAnimation(spell, expectedTargetCount: 0);
        Assert.Empty(ParseSpellAnimations(targetSent).Single().Targets);

        var output = log.ToString();
        Assert.Contains("sturdinessAvoided=True", output);
        Assert.Contains("targetApplied=False", output);
        Assert.Contains("spellProgressDamage=0", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresPoisonStar6002StatusAndRemoveOnDeadAttribute()
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
        var session = CreatePoisonStarSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(PoisonStarStatusFlag),
                ["LegacyPoisonStarExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyPoisonStarRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, PoisonStarStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyPoisonStarExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyPoisonStarRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, PoisonStarStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, PoisonStarStatusFlag)));
            Assert.Contains("WorldServer expired legacy PoisonStar flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreatePoisonStarQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: TargetPlayerUid,
            X: 831,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: PoisonStarAttachStatusSpellId);
    }

    private static WorldDataCatalog CreatePoisonStarCatalog(IReadOnlyList<ItemType>? items = null)
    {
        var magicRows = new[]
        {
            CreateMagicRow(PoisonStarAttachStatusSpellId, sort: 6, level: 0, experience: 50, duration: 30, coldTime: 0),
            CreateMagicRow(PoisonStarAttachStatusSpellId, sort: 6, level: 1, experience: 50, duration: 30, coldTime: 0),
            CreateMagicRow(
                PoisonStarAttachStatusSpellId,
                sort: 6,
                level: 2,
                useMana: 3,
                experience: 50,
                useStamina: 2,
                duration: 30,
                coldTime: 0),
            CreateMagicRow(PoisonStarAttachStatusSpellId, sort: 6, level: 3, experience: 50, duration: 30, coldTime: 0),
        };

        return items is null
            ? CreateMagicCatalog(
                CreateMonsterFamily(),
                [],
                magicRows)
            : CreateMagicCatalogWithItems(
                CreateMonsterFamily(),
                [],
                items,
                magicRows);
    }

    private static WorldSession CreatePoisonStarSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(PoisonStarAttachStatusSpellId, level: 2),
            ],
            attributes: attributes);
    }

    private static void AssertPoisonStarApplied(
        WorldSession session,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, PoisonStarStatusFlag));
        Assert.Equal("true", session.Character.Attributes["LegacyPoisonStarRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            "LegacyPoisonStarExpiresAtUtc",
            beforeCast,
            afterCast,
            30);
    }

    private static void AssertPoisonStarAnimation(
        LegacySpellAnimationPacket spell,
        int expectedTargetCount)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(831, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(PoisonStarAttachStatusSpellId, spell.SpellId);
        Assert.Equal(2, spell.SpellLevel);
        Assert.Equal(expectedTargetCount, spell.Targets.Count);
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParsePoisonStarMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgAttackPacket> ParsePoisonStarAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == PoisonStarAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
