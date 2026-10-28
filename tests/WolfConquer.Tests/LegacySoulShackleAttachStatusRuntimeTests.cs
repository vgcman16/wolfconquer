using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SoulShackleAttachStatusSpellId = 10_405;
    private const ushort SoulShackleAttachStatusSpellLevel = 2;
    private const uint SoulShackleAttachStatusDuration = 80;
    private const uint SoulShackleUpdateDataType = 0x36;
    private const int SoulShackleStatusFlag = 0x6F;
    private const int SoulShackleSacredBlessingStatusFlag = 0xFF;
    private const string SoulShackleSturdinessRollAttribute = "LegacySoulShackleSturdinessRoll";
    private const string SoulShackleBattlePowerMessage =
        "Your BattlePower is lower than your opponent get a better BattlePower to be able to use this skill on him.";

    [Fact]
    public async Task HandlerSoulShackle10405VisibleAliveTargetPreparesResourcesThenAbortsBeforeProgressAndAnimation()
    {
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 5_000,
            targetAttributes: new Dictionary<string, string>());

        AssertSoulShackleResourcesPreparedFirst(result);
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.Empty(ParseSpellAnimations(result.CasterSent.Concat(result.TargetSent)));
        Assert.Empty(ParseSoulShackleMessages(result.CasterSent.Concat(result.TargetSent)));
        Assert.Empty(result.TargetSent);
    }

    [Fact]
    public async Task HandlerSoulShackle10405DeadTargetSendsSourceMsgUpdateStatusProgressThenTargetAnimation()
    {
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes());

        AssertSoulShackleResourcesPreparedFirst(result);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));

        var sourceUpdate = Assert.Single(
            ParseMsgUpdates(result.TargetSent),
            IsSoulShackleSourceUpdate);
        AssertSoulShackleSourceUpdate(sourceUpdate, SoulShackleAttachStatusDuration);
        AssertNoSoulShackleSourceUpdate(result.CasterSent);

        var targetStatusUpdate = Assert.Single(
            ParseMsgUpdates(result.TargetSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusValues = AssertSoulShackleStatusFlagVector(targetStatusUpdate);
        Assert.True(IsLegacyStatusFlagSet(statusValues, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(IsLegacyStatusFlagSet(statusValues, SoulShackleStatusFlag));

        var casterStatusUpdate = Assert.Single(
            ParseMsgUpdates(result.CasterSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.True(IsLegacyStatusFlagSet(
            AssertSoulShackleStatusFlagVector(casterStatusUpdate),
            SoulShackleStatusFlag));

        var casterAnimation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        AssertSoulShackleAnimation(casterAnimation, expectedTargetCount: 1);
        var animationTarget = Assert.Single(casterAnimation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.TargetSent)), expectedTargetCount: 1);

        AssertSoulShackleProgress(result.CasterSent, SoulShackleAttachStatusDuration);
        Assert.Empty(ParseLegacySpellPackets(result.TargetSent));
        AssertSoulShackleTargetPacketOrder(result.TargetSent);
        AssertSoulShackleProgressBeforeAnimation(result.CasterSent);
    }

    [Theory]
    [InlineData(SoulShackleStatusFlag)]
    [InlineData(SoulShackleSacredBlessingStatusFlag)]
    public async Task HandlerSoulShackle10405ExistingBlockingStatusStillSendsProgressAndEmptyAnimation(int blockingStatusFlag)
    {
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(blockingStatusFlag));

        AssertSoulShackleResourcesPreparedFirst(result);
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent.Concat(result.TargetSent)),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        AssertSoulShackleProgress(result.CasterSent, SoulShackleAttachStatusDuration);
        Assert.Empty(ParseLegacySpellPackets(result.TargetSent));

        var casterAnimation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        AssertSoulShackleAnimation(casterAnimation, expectedTargetCount: 0);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.TargetSent)), expectedTargetCount: 0);
        AssertSoulShackleProgressBeforeAnimation(result.CasterSent);

        Assert.Equal(
            blockingStatusFlag == SoulShackleStatusFlag,
            HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, blockingStatusFlag));
    }

    [Fact]
    public async Task HandlerSoulShackle10405BattlePowerGapTwentySendsExactMessageWithoutProgressOrAnimation()
    {
        var result = await RunSoulShackleCastAsync(
            casterAttributes: new Dictionary<string, string>
            {
                ["BattlePower"] = "100",
            },
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(
                additional: new Dictionary<string, string>
                {
                    ["BattlePower"] = "120",
                }));

        AssertSoulShackleResourcesPreparedFirst(result);
        var message = Assert.Single(
            ParseSoulShackleMessages(result.CasterSent),
            packet => packet.Content.Any(content =>
                string.Equals(content, SoulShackleBattlePowerMessage, StringComparison.Ordinal)));
        Assert.Contains(SoulShackleBattlePowerMessage, message.Content);
        Assert.Empty(ParseSoulShackleMessages(result.TargetSent));
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.Empty(ParseSpellAnimations(result.CasterSent.Concat(result.TargetSent)));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));
    }

    [Fact]
    public async Task HandlerSoulShackle10405SturdinessAvoidanceSendsEmptyAnimationWithoutProgress()
    {
        const uint sturdinessRuneItemId = 300_109;
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(
                additional: new Dictionary<string, string>
                {
                    [SoulShackleSturdinessRollAttribute] = "0",
                }),
            targetItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = sturdinessRuneItemId,
                Position = 101,
            }),
            itemTypes: [CreateItemType((int)sturdinessRuneItemId, "Sturdiness(+9)")]);

        AssertSoulShackleResourcesPreparedFirst(result);
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent.Concat(result.TargetSent)),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));

        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.CasterSent)), expectedTargetCount: 0);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.TargetSent)), expectedTargetCount: 0);
    }

    [Fact]
    public async Task HandlerSoulShackle10405EvocationUsesUncheckedUintDurationAndOriginalDurationForProgress()
    {
        const uint evocationRuneItemId = 300_109;
        const uint sourceDuration = 40;
        const byte evocationReduction = 65;
        var expectedDuration = unchecked(sourceDuration - evocationReduction);
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(),
            targetItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = evocationRuneItemId,
                Position = 101,
            }),
            itemTypes: [CreateItemType((int)evocationRuneItemId, "Evocation(+9)")],
            duration: sourceDuration);

        AssertSoulShackleResourcesPreparedFirst(result);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));
        var sourceUpdate = Assert.Single(
            ParseMsgUpdates(result.TargetSent),
            IsSoulShackleSourceUpdate);
        AssertSoulShackleSourceUpdate(sourceUpdate, expectedDuration);
        AssertSoulShackleProgress(result.CasterSent, sourceDuration);

        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        AssertSoulShackleAnimation(animation, expectedTargetCount: 1);
        Assert.Equal(0U, Assert.Single(animation.Targets).Damage);
        AssertSoulShackleTargetPacketOrder(result.TargetSent);
        AssertSoulShackleProgressBeforeAnimation(result.CasterSent);
    }

    [Fact]
    public async Task HandlerSoulShackle10405FreeSoulAvoidanceBroadcastsRefineEffectAndEmptyAnimationWithoutProgress()
    {
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(
                additional: new Dictionary<string, string>
                {
                    ["PerfectionStatus.FreeSoul"] = "100",
                }));

        AssertSoulShackleResourcesPreparedFirst(result);
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent.Concat(result.TargetSent)),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));

        AssertSoulShackleFreeSoulEffect(Assert.Single(ParseSoulShackleRefineEffects(result.CasterSent)));
        AssertSoulShackleFreeSoulEffect(Assert.Single(ParseSoulShackleRefineEffects(result.TargetSent)));
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.CasterSent)), expectedTargetCount: 0);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.TargetSent)), expectedTargetCount: 0);
    }

    [Theory]
    [InlineData(0UL, 42UL, "", false)]
    [InlineData(41UL, 42UL, "", false)]
    [InlineData(42UL, 42UL, "", true)]
    [InlineData(41UL, 42UL, "42", true)]
    public async Task HandlerSoulShackle10405GuildWarRequiresOwnerGuildOrAlly(
        ulong casterGuildId,
        ulong ownerGuildId,
        string allyGuildIds,
        bool expectedAllowed)
    {
        var casterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyNewGuildWarExorcimPillarGuildID"] = ownerGuildId.ToString(),
        };
        if (casterGuildId != 0)
        {
            casterAttributes["GuildID"] = casterGuildId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(allyGuildIds))
        {
            casterAttributes["GuildAllyIDs"] = allyGuildIds;
        }

        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        guildWarStates.Start();
        var result = await RunSoulShackleCastAsync(
            casterAttributes,
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes(),
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            guildWarStates: guildWarStates);

        AssertSoulShackleResourcesPreparedFirst(result);
        Assert.Equal(
            expectedAllowed,
            HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));
        if (expectedAllowed)
        {
            Assert.Single(ParseMsgUpdates(result.TargetSent), IsSoulShackleSourceUpdate);
            AssertSoulShackleProgress(result.CasterSent, SoulShackleAttachStatusDuration);
            AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.CasterSent)), expectedTargetCount: 1);
            Assert.DoesNotContain("owner-guild authorization failed", result.Log);
        }
        else
        {
            AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
            Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
            Assert.Empty(ParseSpellAnimations(result.CasterSent.Concat(result.TargetSent)));
            Assert.Contains("owner-guild authorization failed", result.Log);
        }
    }

    [Fact]
    public async Task HandlerSoulShackle10405MissingVisibleTargetStillSendsOriginalDurationProgressAndEmptyAnimation()
    {
        var result = await RunSoulShackleCastAsync(opponentUid: TargetPlayerUid + 1);

        AssertSoulShackleResourcesPreparedFirst(result);
        AssertNoSoulShackleSourceUpdate(result.CasterSent.Concat(result.TargetSent));
        AssertSoulShackleProgress(result.CasterSent, SoulShackleAttachStatusDuration);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.CasterSent)), expectedTargetCount: 0);
        AssertSoulShackleAnimation(Assert.Single(ParseSpellAnimations(result.TargetSent)), expectedTargetCount: 0);
        AssertSoulShackleProgressBeforeAnimation(result.CasterSent);
    }

    [Fact]
    public async Task RuntimeLoopExpiresSoulShackle10405StatusAndRemoveOnDeadMetadata()
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
        var session = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            minHitPoints: 0,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyPlayerDeathState.DeadStatusFlag,
                    SoulShackleStatusFlag),
                ["LegacySoulShackleExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacySoulShackleRemoveOnDead"] = bool.TrueString,
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, SoulShackleStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacySoulShackleExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacySoulShackleRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.True(HasLegacyStatusFlag(
                expiredSession.Character.Attributes,
                LegacyPlayerDeathState.DeadStatusFlag));
            Assert.Contains(
                ParseMsgUpdates(sent),
                update => update.Uid == TargetPlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        !IsLegacyStatusFlagSet(entry.Values, SoulShackleStatusFlag) &&
                        IsLegacyStatusFlagSet(entry.Values, LegacyPlayerDeathState.DeadStatusFlag)));
            Assert.Contains("WorldServer expired legacy SoulShackle flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task PlayerDeathClearsSoulShackle10405FlagAndRuntimeMetadata()
    {
        var result = await RunSoulShackleCastAsync(
            targetMinHitPoints: 0,
            targetAttributes: CreateDeadSoulShackleTargetAttributes());
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, SoulShackleStatusFlag));

        var death = LegacyPlayerDeathState.ApplyDeath(
            result.TargetSession.Character,
            DateTimeOffset.UtcNow,
            deadX: 831,
            deadY: 584);

        Assert.True(HasLegacyStatusFlag(death.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(death.Character.Attributes, SoulShackleStatusFlag));
        Assert.DoesNotContain("LegacySoulShackleExpiresAtUtc", death.Character.Attributes.Keys);
        Assert.DoesNotContain("LegacySoulShackleRemoveOnDead", death.Character.Attributes.Keys);
    }

    private static async Task<SoulShackleCastResult> RunSoulShackleCastAsync(
        IReadOnlyDictionary<string, string>? casterAttributes = null,
        long targetMinHitPoints = 0,
        IReadOnlyDictionary<string, string>? targetAttributes = null,
        PlayerItemSnapshot? targetItems = null,
        IReadOnlyList<ItemType>? itemTypes = null,
        uint duration = SoulShackleAttachStatusDuration,
        long mapId = 1_015,
        LegacyGuildWarRuntimeStateStore? guildWarStates = null,
        uint opponentUid = TargetPlayerUid)
    {
        var worldData = CreateSoulShackleCatalog(duration, itemTypes);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(SoulShackleAttachStatusSpellId, SoulShackleAttachStatusSpellLevel),
            ],
            attributes: casterAttributes,
            mapId: mapId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: targetMinHitPoints,
            attributes: targetAttributes,
            playerItems: targetItems,
            mapId: mapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            GuildWarStates: guildWarStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
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
                SpellId: SoulShackleAttachStatusSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        return new SoulShackleCastResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static WorldDataCatalog CreateSoulShackleCatalog(
        uint duration,
        IReadOnlyList<ItemType>? itemTypes)
    {
        var magicRows = Enumerable
            .Range(0, 5)
            .Select(level => CreateMagicRow(
                SoulShackleAttachStatusSpellId,
                sort: 6,
                level: checked((byte)level),
                useMana: 3,
                experience: 1_000,
                range: 15,
                useStamina: 2,
                coolDown: 0,
                coldTime: 0,
                duration: duration))
            .ToArray();

        return itemTypes is null
            ? CreateMagicCatalog(CreateMonsterFamily(), [], magicRows)
            : CreateMagicCatalogWithItems(CreateMonsterFamily(), [], itemTypes, magicRows);
    }

    private static IReadOnlyDictionary<string, string> CreateDeadSoulShackleTargetAttributes(
        int? statusFlag = null,
        IReadOnlyDictionary<string, string>? additional = null)
    {
        var flags = statusFlag is { } value
            ? BuildStatusFlags(LegacyPlayerDeathState.DeadStatusFlag, value)
            : BuildStatusFlags(LegacyPlayerDeathState.DeadStatusFlag);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = flags,
        };
        if (additional is not null)
        {
            foreach (var pair in additional)
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        return attributes;
    }

    private static void AssertSoulShackleResourcesPreparedFirst(SoulShackleCastResult result)
    {
        Assert.Equal(497, result.CasterSession.Character.MinMana);
        Assert.Equal("98", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.True(result.CasterSent.Count >= 2);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.CasterSent[0]), out var staminaUpdate));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 98)],
            staminaUpdate.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.CasterSent[1]), out var manaUpdate));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 497)],
            manaUpdate.Updates);
    }

    private static void AssertSoulShackleSourceUpdate(
        LegacyMsgUpdatePacket update,
        uint expectedDuration)
    {
        Assert.Equal(TargetPlayerUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(SoulShackleUpdateDataType, (uint)entry.Type);
        Assert.Equal((uint)SoulShackleStatusFlag, entry.Flag);
        Assert.Equal(expectedDuration, entry.Time);
        Assert.Equal(0U, entry.Damage);
        Assert.Equal((uint)SoulShackleAttachStatusSpellLevel, entry.SpellLevel);
        Assert.Equal(0UL, entry.Value);
        Assert.Null(entry.Values);
    }

    private static IReadOnlyList<ulong> AssertSoulShackleStatusFlagVector(
        LegacyMsgUpdatePacket update,
        uint expectedUid = TargetPlayerUid)
    {
        Assert.Equal(expectedUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, entry.Values!.Count);
        return entry.Values;
    }

    private static bool IsSoulShackleSourceUpdate(LegacyMsgUpdatePacket update)
    {
        return update.Updates.Any(entry =>
            (uint)entry.Type == SoulShackleUpdateDataType &&
            entry.Flag == SoulShackleStatusFlag);
    }

    private static void AssertNoSoulShackleSourceUpdate(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(ParseMsgUpdates(frames), IsSoulShackleSourceUpdate);
    }

    private static void AssertSoulShackleProgress(
        IEnumerable<byte[]> frames,
        uint expectedOriginalDuration)
    {
        var spellUpdate = Assert.Single(ParseLegacySpellPackets(frames));
        Assert.Equal(SoulShackleAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(SoulShackleAttachStatusSpellLevel, spellUpdate.Level);
        Assert.Equal(checked((int)expectedOriginalDuration), spellUpdate.Experience);
    }

    private static void AssertSoulShackleAnimation(
        LegacySpellAnimationPacket animation,
        int expectedTargetCount)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(831, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(SoulShackleAttachStatusSpellId, animation.SpellId);
        Assert.Equal(SoulShackleAttachStatusSpellLevel, animation.SpellLevel);
        Assert.Equal(expectedTargetCount, animation.Targets.Count);
    }

    private static void AssertSoulShackleTargetPacketOrder(IReadOnlyList<byte[]> targetFrames)
    {
        var sourceUpdateIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                IsSoulShackleSourceUpdate(update));
        var statusIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, SoulShackleStatusFlag)));
        var animationIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == SoulShackleAttachStatusSpellId);

        Assert.True(sourceUpdateIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);
    }

    private static void AssertSoulShackleProgressBeforeAnimation(IReadOnlyList<byte[]> casterFrames)
    {
        var progressIndex = SingleFrameIndex(
            casterFrames,
            frame => LegacySpellPacket.TryParse(frame, out var spell) &&
                spell.Id == SoulShackleAttachStatusSpellId);
        var animationIndex = SingleFrameIndex(
            casterFrames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == SoulShackleAttachStatusSpellId);

        Assert.True(progressIndex < animationIndex);
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseSoulShackleMessages(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgRefineEffectPacket> ParseSoulShackleRefineEffects(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgRefineEffectPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertSoulShackleFreeSoulEffect(LegacyMsgRefineEffectPacket packet)
    {
        Assert.Equal(TargetPlayerUid, packet.Id);
        Assert.Equal(TargetPlayerUid, packet.Param);
        Assert.Equal(LegacyRefineEffect.FreeSoul, packet.Effect);
    }

    private sealed record SoulShackleCastResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Log);
}
