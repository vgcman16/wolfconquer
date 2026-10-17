using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort AttachStatusXpAccuracySpellId = 1_015;
    private const ushort AttachStatusXpShieldSpellId = 1_020;
    private const ushort AttachStatusXpFlySpellId = 8_002;
    private const int AttachStatusXpAccuracyDurationSeconds = 120;
    private const int AttachStatusXpShieldDurationSeconds = 120;
    private const int AttachStatusXpFlyDurationSeconds = 40;
    private const int AttachStatusXpUnrelatedFlag = 0x1FF;
    private const string AttachStatusXpAccuracyExpiresAtUtcAttribute = "LegacyAccuracyExpiresAtUtc";
    private const string AttachStatusXpAccuracyRemoveOnDeadAttribute = "LegacyAccuracyRemoveOnDead";
    private const string AttachStatusXpShieldExpiresAtUtcAttribute = "LegacyMagicShieldExpiresAtUtc";
    private const string AttachStatusXpShieldRemoveOnDeadAttribute = "LegacyMagicShieldRemoveOnDead";
    private const string AttachStatusXpFlyExpiresAtUtcAttribute = "LegacyFlyExpiresAtUtc";
    private const string AttachStatusXpFlyRemoveOnDeadAttribute = "LegacyFlyRemoveOnDead";

    [Theory]
    [InlineData(AttachStatusXpAccuracySpellId)]
    [InlineData(AttachStatusXpShieldSpellId)]
    [InlineData(AttachStatusXpFlySpellId)]
    public void ClassifyAttachStatusXpBuffAsFullyModeledD13XpSkill(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.AttachStatus, spellId);

        Assert.True(info.Supported);
        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.Contains("without resetting XPCount", info.Reason);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
    }

    [Theory]
    [InlineData(AttachStatusXpAccuracySpellId, LegacyStarOfAccuracyFlag, AttachStatusXpAccuracyExpiresAtUtcAttribute, AttachStatusXpAccuracyRemoveOnDeadAttribute)]
    [InlineData(AttachStatusXpShieldSpellId, LegacyMagicShieldFlag, AttachStatusXpShieldExpiresAtUtcAttribute, AttachStatusXpShieldRemoveOnDeadAttribute)]
    [InlineData(AttachStatusXpFlySpellId, FlyStatusFlag, AttachStatusXpFlyExpiresAtUtcAttribute, AttachStatusXpFlyRemoveOnDeadAttribute)]
    public async Task HandlerAttachStatusXpBuffWithoutXpListPreparesResourcesThenRunsSourceNoOp(
        ushort spellId,
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute)
    {
        var result = await RunAttachStatusXpBuffCastAsync(
            spellId,
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(AttachStatusXpUnrelatedFlag),
                ["XPCount"] = "37",
            },
            observableResourceCosts: true);

        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.Equal("96", result.Session.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, statusFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, AttachStatusXpUnrelatedFlag));
        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.False(result.Session.Character.Attributes.ContainsKey(expiresAtAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(removeOnDeadAttribute));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Collection(
            updates,
            update => Assert.Equal(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 96)],
                update.Updates),
            update => Assert.Equal(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)],
                update.Updates));
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or
                LegacyMsgUpdateDataType.XPCircle or
                LegacyMsgUpdateDataType.StatusFlag));
        AssertNoAttachStatusXpBuffD13(result.Sent);
        AssertAttachStatusXpBuffCooldown(result, spellId);
    }

    [Theory]
    [InlineData(AttachStatusXpAccuracySpellId, LegacyStarOfAccuracyFlag, AttachStatusXpAccuracyDurationSeconds, 0, AttachStatusXpAccuracyExpiresAtUtcAttribute, AttachStatusXpAccuracyRemoveOnDeadAttribute)]
    [InlineData(AttachStatusXpShieldSpellId, LegacyMagicShieldFlag, AttachStatusXpShieldDurationSeconds, AttachStatusXpShieldDurationSeconds, AttachStatusXpShieldExpiresAtUtcAttribute, AttachStatusXpShieldRemoveOnDeadAttribute)]
    [InlineData(AttachStatusXpFlySpellId, FlyStatusFlag, AttachStatusXpFlyDurationSeconds, AttachStatusXpFlyDurationSeconds, AttachStatusXpFlyExpiresAtUtcAttribute, AttachStatusXpFlyRemoveOnDeadAttribute)]
    public async Task HandlerAttachStatusXpBuffWithXpListConsumesListOpensSourceDurationStatusAndSendsD13(
        ushort spellId,
        int statusFlag,
        int durationSeconds,
        int animationDamage,
        string expiresAtAttribute,
        string removeOnDeadAttribute)
    {
        var result = await RunAttachStatusXpBuffCastAsync(
            spellId,
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, AttachStatusXpUnrelatedFlag),
                ["XPCount"] = "37",
            });

        Assert.Equal(500, result.Session.Character.MinMana);
        Assert.Equal("100", result.Session.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, statusFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, AttachStatusXpUnrelatedFlag));
        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            expiresAtAttribute,
            result.BeforeCast,
            result.AfterCast,
            durationSeconds);
        Assert.True(bool.Parse(result.Session.Character.Attributes[removeOnDeadAttribute]));

        AssertAttachStatusXpBuffSourceClientUpdates(
            result.Sent,
            result.Session,
            spellId,
            statusFlag,
            flyWasAlreadyActive: false);
        AssertAttachStatusXpBuffD13(result.Sent, spellId, checked((uint)animationDamage));
        AssertAttachStatusXpBuffCooldown(result, spellId);
    }

    [Fact]
    public async Task HandlerAttachStatusXpBuffFly8002RefreshesSourceDurationBeforeConsumingXpList()
    {
        var oldExpiration = DateTimeOffset.UtcNow.AddSeconds(5).ToString("O", CultureInfo.InvariantCulture);
        var result = await RunAttachStatusXpBuffCastAsync(
            AttachStatusXpFlySpellId,
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyXpListFlag,
                    FlyStatusFlag,
                    AttachStatusXpUnrelatedFlag),
                ["XPCount"] = "37",
                [AttachStatusXpFlyExpiresAtUtcAttribute] = oldExpiration,
                [AttachStatusXpFlyRemoveOnDeadAttribute] = bool.TrueString,
            });

        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, FlyStatusFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, AttachStatusXpUnrelatedFlag));
        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.NotEqual(oldExpiration, result.Session.Character.Attributes[AttachStatusXpFlyExpiresAtUtcAttribute]);
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            AttachStatusXpFlyExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            AttachStatusXpFlyDurationSeconds);
        Assert.True(bool.Parse(result.Session.Character.Attributes[AttachStatusXpFlyRemoveOnDeadAttribute]));

        AssertAttachStatusXpBuffSourceClientUpdates(
            result.Sent,
            result.Session,
            AttachStatusXpFlySpellId,
            FlyStatusFlag,
            flyWasAlreadyActive: true);
        AssertAttachStatusXpBuffD13(
            result.Sent,
            AttachStatusXpFlySpellId,
            AttachStatusXpFlyDurationSeconds);
        AssertAttachStatusXpBuffCooldown(result, AttachStatusXpFlySpellId);
    }

    [Theory]
    [InlineData(LegacyStarOfAccuracyFlag, AttachStatusXpAccuracyExpiresAtUtcAttribute, AttachStatusXpAccuracyRemoveOnDeadAttribute)]
    [InlineData(LegacyMagicShieldFlag, AttachStatusXpShieldExpiresAtUtcAttribute, AttachStatusXpShieldRemoveOnDeadAttribute)]
    [InlineData(FlyStatusFlag, AttachStatusXpFlyExpiresAtUtcAttribute, AttachStatusXpFlyRemoveOnDeadAttribute)]
    public async Task RuntimeLoopExpiresAttachStatusXpBuffAndSendsRuntimeConsistentStatusVector(
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute)
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
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(statusFlag, AttachStatusXpUnrelatedFlag),
                [expiresAtAttribute] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
                [removeOnDeadAttribute] = bool.TrueString,
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            LegacyMsgUpdatePacket? statusUpdate = null;
            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    if (HasLegacyStatusFlag(current.Character.Attributes, statusFlag) ||
                        current.Character.Attributes.ContainsKey(expiresAtAttribute) ||
                        current.Character.Attributes.ContainsKey(removeOnDeadAttribute))
                    {
                        return false;
                    }

                    // Expiry commits the session before publishing its status frame.
                    // Wait for both, using the thread-safe parsed frame snapshot.
                    var expectedValues = LegacyPlayerDeathState.CreateStatusFlagVector(
                        current.Character.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
                    statusUpdate = ParseMsgUpdates(sent).LastOrDefault(update =>
                        update.Uid == PlayerUid &&
                        update.Updates.Count == 1 &&
                        update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag &&
                        update.Updates[0].Values is { } publishedValues &&
                        publishedValues.SequenceEqual(expectedValues));
                    return statusUpdate is not null;
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, statusFlag));
            Assert.True(HasLegacyStatusFlag(expiredSession.Character.Attributes, AttachStatusXpUnrelatedFlag));

            Assert.NotNull(statusUpdate);
            var values = AssertStatusFlagVector(statusUpdate);
            Assert.False(IsLegacyStatusFlagSet(values, statusFlag));
            Assert.True(IsLegacyStatusFlagSet(values, AttachStatusXpUnrelatedFlag));
            Assert.Equal(
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    expiredSession.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength),
                values.ToArray());
            AssertNoAttachStatusXpBuffD13(sent);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static async Task<AttachStatusXpBuffCastResult> RunAttachStatusXpBuffCastAsync(
        ushort spellId,
        IReadOnlyDictionary<string, string> attributes,
        bool observableResourceCosts = false)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAttachStatusXpBuffSourceRow(spellId, observableResourceCosts));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [spellId],
            attributes: attributes);
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
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 832,
                Y: 587,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new AttachStatusXpBuffCastResult(updatedSession, sent, beforeCast, afterCast);
    }

    private static MagicType CreateAttachStatusXpBuffSourceRow(
        ushort spellId,
        bool observableResourceCosts)
    {
        var useMana = observableResourceCosts ? (ushort)7 : (ushort)0;
        var useStamina = observableResourceCosts ? (byte)4 : (byte)0;
        return spellId switch
        {
            AttachStatusXpAccuracySpellId => CreateAttachStatusXpBuffSourceRow(
                rowId: 1_026,
                spellId,
                name: "Accuracy",
                rawDamage: 30_300,
                duration: AttachStatusXpAccuracyDurationSeconds,
                range: 0,
                needLevel: 15,
                weaponType: 0,
                coldTime: 0,
                sourceField16: 5,
                sourceField34: 1,
                useMana,
                useStamina),
            AttachStatusXpShieldSpellId => CreateAttachStatusXpBuffSourceRow(
                rowId: 1_027,
                spellId,
                name: "Shield",
                rawDamage: 30_300,
                duration: AttachStatusXpShieldDurationSeconds,
                range: 0,
                needLevel: 15,
                weaponType: 0,
                coldTime: 0,
                sourceField16: 44,
                sourceField34: 1,
                useMana,
                useStamina,
                damageOnHuman: 183,
                damage2: 35,
                damage3: 30_400),
            AttachStatusXpFlySpellId => CreateAttachStatusXpBuffSourceRow(
                rowId: 8_020,
                spellId,
                name: "Fly",
                rawDamage: 1,
                duration: AttachStatusXpFlyDurationSeconds,
                range: 9,
                needLevel: 0,
                weaponType: 500,
                coldTime: 45_000,
                sourceField16: 22,
                sourceField34: 0,
                useMana,
                useStamina),
            _ => throw new ArgumentOutOfRangeException(nameof(spellId), spellId, "Unknown AttachStatus XP buff spell."),
        };
    }

    private static MagicType CreateAttachStatusXpBuffSourceRow(
        int rowId,
        ushort spellId,
        string name,
        uint rawDamage,
        uint duration,
        byte range,
        ushort needLevel,
        uint weaponType,
        int coldTime,
        int sourceField16,
        int sourceField34,
        ushort useMana,
        byte useStamina,
        int damageOnHuman = 0,
        int damage2 = 0,
        int damage3 = 0)
    {
        var runtimeRow = CreateMagicRow(
            spellId,
            sort: 6,
            level: 0,
            useMana,
            damage: rawDamage,
            experience: 0,
            maxTargets: 0,
            range,
            needLevel,
            weaponType,
            useStamina,
            coolDown: 0,
            coldTime,
            duration,
            damageOnHuman,
            damage2,
            damage3,
            gDamage: 100);
        var fields = runtimeRow.Fields.Concat([string.Empty]).ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[3] = name;
        fields[7] = "2";
        fields[16] = sourceField16.ToString(CultureInfo.InvariantCulture);
        fields[21] = "1";
        fields[34] = sourceField34.ToString(CultureInfo.InvariantCulture);
        return new MagicType(rowId, spellId, name, fields);
    }

    private static void AssertAttachStatusXpBuffSourceClientUpdates(
        IEnumerable<byte[]> frames,
        WorldSession session,
        ushort spellId,
        int statusFlag,
        bool flyWasAlreadyActive)
    {
        var updates = ParseMsgUpdates(frames);
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.XPCircle));

        IReadOnlyList<ulong> finalValues;
        if (spellId == AttachStatusXpFlySpellId && flyWasAlreadyActive)
        {
            Assert.Equal(2, updates.Count);
            AssertXpListConsumedUpdate(updates[0]);
            finalValues = AssertStatusFlagVector(updates[1]);
            AssertStatusFlags(finalValues, statusFlag, xpListExpected: false, statusExpected: true);
        }
        else if (spellId == AttachStatusXpFlySpellId)
        {
            Assert.Equal(3, updates.Count);
            var openedValues = AssertStatusFlagVector(updates[0]);
            AssertStatusFlags(openedValues, statusFlag, xpListExpected: true, statusExpected: true);
            AssertXpListConsumedUpdate(updates[1]);
            finalValues = AssertStatusFlagVector(updates[2]);
            AssertStatusFlags(finalValues, statusFlag, xpListExpected: false, statusExpected: true);
        }
        else
        {
            Assert.Equal(3, updates.Count);
            AssertXpListConsumedUpdate(updates[0]);
            var consumedValues = AssertStatusFlagVector(updates[1]);
            AssertStatusFlags(consumedValues, statusFlag, xpListExpected: false, statusExpected: false);
            finalValues = AssertStatusFlagVector(updates[2]);
            AssertStatusFlags(finalValues, statusFlag, xpListExpected: false, statusExpected: true);
        }

        Assert.Equal(
            LegacyPlayerDeathState.CreateStatusFlagVector(
                session.Character.Attributes,
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength),
            finalValues.ToArray());
    }

    private static void AssertXpListConsumedUpdate(LegacyMsgUpdatePacket update)
    {
        Assert.Equal(PlayerUid, update.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            update.Updates);
    }

    private static IReadOnlyList<ulong> AssertStatusFlagVector(LegacyMsgUpdatePacket update)
    {
        Assert.Equal(PlayerUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, entry.Values!.Count);
        return entry.Values;
    }

    private static void AssertStatusFlags(
        IReadOnlyList<ulong> values,
        int statusFlag,
        bool xpListExpected,
        bool statusExpected)
    {
        Assert.Equal(xpListExpected, IsLegacyStatusFlagSet(values, LegacyXpListFlag));
        Assert.Equal(statusExpected, IsLegacyStatusFlagSet(values, statusFlag));
        Assert.True(IsLegacyStatusFlagSet(values, AttachStatusXpUnrelatedFlag));
    }

    private static void AssertAttachStatusXpBuffD13(
        IEnumerable<byte[]> frames,
        ushort spellId,
        uint expectedDamage)
    {
        var parsedFrames = SnapshotFrames(frames).Select(ReadFrame).ToArray();
        var d13Frame = Assert.Single(
            parsedFrames,
            frame => frame.Header.Opcode == 2_239);
        Assert.Equal(WorldPacketOpcodes.LegacySpellUse, d13Frame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(d13Frame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(832, spell.X);
        Assert.Equal(587, spell.Y);
        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertNoAttachStatusXpBuffD13(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            SnapshotFrames(frames),
            bytes => ReadFrame(bytes).Header.Opcode == 2_239);
    }

    private static void AssertAttachStatusXpBuffCooldown(
        AttachStatusXpBuffCastResult result,
        ushort spellId)
    {
        if (spellId != AttachStatusXpFlySpellId)
        {
            Assert.True(result.Session.MagicCooldowns is null ||
                !result.Session.MagicCooldowns.ContainsKey(spellId));
            Assert.Empty(ParseMagicColdTimePackets(result.Sent));
            return;
        }

        Assert.NotNull(result.Session.MagicCooldowns);
        Assert.True(result.Session.MagicCooldowns.TryGetValue(spellId, out var readyAtUtc));
        Assert.InRange(
            readyAtUtc,
            result.BeforeCast.AddMilliseconds(45_000),
            result.AfterCast.AddMilliseconds(45_000));
        var packet = Assert.Single(ParseMagicColdTimePackets(result.Sent));
        Assert.Equal(
            [new LegacyMagicColdTimeSpell(AttachStatusXpFlySpellId, 45_000)],
            packet.Spells);
    }

    private sealed record AttachStatusXpBuffCastResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
