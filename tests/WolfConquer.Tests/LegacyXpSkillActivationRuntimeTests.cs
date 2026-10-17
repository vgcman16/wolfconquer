using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort TestBladeFlurrySpellId = 11_610;
    private const ushort TestChainBoltSpellId = 10_309;
    private const ushort TestFatalStrikeAttachStatusSpellId = 6_011;
    private const ushort TestDragonCycloneSpellId = 12_290;
    private const ushort TestOmnipotenceSpellId = 12_860;
    private const ushort TestThunderRampageSpellId = 15_780;
    private const uint MatchingDecompileChainBoltDatabaseDurationSeconds = 60;
    private const int TestChainBoltFlag = 0x5C;
    private const int TestFatalStrikeFlag = 0x2F;
    private const int TestBladeFlurryFlag = 0x92;
    private const int TestDragonCycloneFlag = 0x9F;
    private const int TestOmnipotenceFlag = 0xC0;
    private const int TestThunderRampageFlag = 0xEB;
    private const string RuntimeLegacyBladeFlurryExpiresAtUtcAttribute = "LegacyBladeFlurryExpiresAtUtc";
    private const string RuntimeLegacyChainBoltExpiresAtUtcAttribute = "LegacyChainBoltExpiresAtUtc";
    private const string RuntimeLegacyFatalStrikeExpiresAtUtcAttribute = "LegacyFatalStrikeExpiresAtUtc";
    private const string RuntimeLegacyDragonCycloneExpiresAtUtcAttribute = "LegacyDragonCycloneExpiresAtUtc";
    private const string RuntimeLegacyOmnipotenceExpiresAtUtcAttribute = "LegacyOmnipotenceExpiresAtUtc";
    private const string RuntimeLegacyThunderRampageExpiresAtUtcAttribute = "LegacyThunderRampageExpiresAtUtc";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerBladeFlurryWithXpListOpensOrRefreshesFiftySecondFlagSkipsAttackAnimation(
        bool alreadyActive)
    {
        var initialFlags = alreadyActive
            ? BuildStatusFlags(LegacyXpListFlag, TestBladeFlurryFlag, LegacySupermanFlag)
            : BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag);
        var result = await RunLegacyXpSkillActivationAsync(
            TestBladeFlurrySpellId,
            sort: 68,
            duration: 50,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = initialFlags,
                ["XPCount"] = "19",
            });

        Assert.Empty(ParseSpellAnimations(result.Sent));
        AssertLegacyXpSkillOpened(
            result,
            TestBladeFlurrySpellId,
            TestBladeFlurryFlag,
            RuntimeLegacyBladeFlurryExpiresAtUtcAttribute,
            expectedDurationSeconds: 50);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina));
        Assert.Contains("WorldServer handled legacy BladeFlurry XPList transition", result.Log);
        Assert.Contains($"refreshedBladeFlurry={alreadyActive}", result.Log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerChainBoltWithXpListOpensOrRefreshesFiftySecondFlagSkipsAttackAnimation(
        bool alreadyActive)
    {
        var initialFlags = alreadyActive
            ? BuildStatusFlags(LegacyXpListFlag, TestChainBoltFlag, LegacySupermanFlag)
            : BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag);
        var result = await RunLegacyXpSkillActivationAsync(
            TestChainBoltSpellId,
            sort: 37,
            duration: MatchingDecompileChainBoltDatabaseDurationSeconds,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = initialFlags,
                ["XPCount"] = "23",
            });

        Assert.Empty(ParseSpellAnimations(result.Sent));
        AssertLegacyXpSkillOpened(
            result,
            TestChainBoltSpellId,
            TestChainBoltFlag,
            RuntimeLegacyChainBoltExpiresAtUtcAttribute,
            expectedDurationSeconds: 50);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina));
        Assert.Contains("WorldServer handled legacy ChainBolt XPList transition", result.Log);
        Assert.Contains($"spellId={TestChainBoltSpellId} sort=37", result.Log);
        Assert.Contains("consumedXpList=True openedChainBolt=True", result.Log);
        Assert.Contains($"refreshedChainBolt={alreadyActive}", result.Log);
    }

    [Fact]
    public async Task HandlerFatalStrike6011WithXpListConsumesXpListOpensSixtySecondFlagAndSendsSelfAnimation()
    {
        var result = await RunLegacyXpSkillActivationAsync(
            TestFatalStrikeAttachStatusSpellId,
            sort: 6,
            duration: 99,
            x: 832,
            y: 586,
            opponentUid: PlayerUid,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag),
                ["XPCount"] = "29",
            });

        AssertLegacyXpSkillOpened(
            result,
            TestFatalStrikeAttachStatusSpellId,
            TestFatalStrikeFlag,
            RuntimeLegacyFatalStrikeExpiresAtUtcAttribute,
            expectedDurationSeconds: 60);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(TestFatalStrikeAttachStatusSpellId, spell.SpellId);
        Assert.Equal(832, spell.X);
        Assert.Equal(586, spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Contains("WorldServer handled legacy FatalStrike XPList transition", result.Log);
        Assert.Contains("refreshedFatalStrike=False", result.Log);
    }

    [Fact]
    public async Task HandlerFatalStrike6011WithoutXpListSpendsResourcesThenSkipsActivationAndAnimation()
    {
        var result = await RunLegacyXpSkillActivationAsync(
            TestFatalStrikeAttachStatusSpellId,
            sort: 6,
            duration: 99,
            useMana: 7,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacySupermanFlag),
                ["XPCount"] = "29",
            });

        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, TestFatalStrikeFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(RuntimeLegacyFatalStrikeExpiresAtUtcAttribute));
        Assert.Equal("29", result.Session.Character.Attributes["XPCount"]);

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or LegacyMsgUpdateDataType.XPCircle));
        Assert.Contains("WorldServer skipped legacy FatalStrike magic", result.Log);
        Assert.Contains("XPList status flag is not active", result.Log);
    }

    [Fact]
    public async Task HandlerDragonCycloneWithXpListConsumesXpListOpensDurationFlagAndSendsSelfAnimation()
    {
        var result = await RunLegacyXpSkillActivationAsync(
            TestDragonCycloneSpellId,
            sort: 75,
            duration: 27,
            x: 830,
            y: 584,
            opponentUid: PlayerUid,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag),
                ["XPCount"] = "31",
            });

        AssertLegacyXpSkillOpened(
            result,
            TestDragonCycloneSpellId,
            TestDragonCycloneFlag,
            RuntimeLegacyDragonCycloneExpiresAtUtcAttribute,
            expectedDurationSeconds: 27);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(TestDragonCycloneSpellId, spell.SpellId);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Contains("WorldServer handled legacy DragonCyclone XPList transition", result.Log);
    }

    [Fact]
    public async Task HandlerDragonCycloneWithoutXpListSkipsXpActivation()
    {
        var result = await RunLegacyXpSkillActivationAsync(
            TestDragonCycloneSpellId,
            sort: 75,
            duration: 27,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacySupermanFlag),
                ["XPCount"] = "31",
            });

        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, TestDragonCycloneFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(RuntimeLegacyDragonCycloneExpiresAtUtcAttribute));
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or LegacyMsgUpdateDataType.XPCircle));
        Assert.Contains("DragonCyclone status flags are not active", result.Log);
    }

    [Theory]
    [InlineData(75, false, 100, true)]
    [InlineData(140, true, 150, true)]
    [InlineData(150, true, 150, false)]
    public async Task HandlerOmnipotenceWithXpListRestoresStaminaConsumesXpListOpensFortySecondFlagAndSkipsAttackAnimation(
        ulong startingStamina,
        bool hasHeavenBlessing,
        ushort expectedStamina,
        bool expectsStaminaUpdate)
    {
        var statusFlags = hasHeavenBlessing
            ? BuildStatusFlags(LegacyXpListFlag, LegacyHeavenBlessingFlag, LegacySupermanFlag)
            : BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag);
        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = statusFlags,
            ["XPCount"] = "41",
        };
        if (hasHeavenBlessing)
        {
            attributes["HeavenBlessing"] = "3600";
        }

        var result = await RunLegacyXpSkillActivationAsync(
            TestOmnipotenceSpellId,
            sort: 85,
            duration: 40,
            startingStamina: startingStamina,
            initialAttributes: attributes);

        Assert.Empty(ParseSpellAnimations(result.Sent));
        AssertLegacyXpSkillOpened(
            result,
            TestOmnipotenceSpellId,
            TestOmnipotenceFlag,
            RuntimeLegacyOmnipotenceExpiresAtUtcAttribute,
            expectedDurationSeconds: 40);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.Equal(
            expectedStamina.ToString(CultureInfo.InvariantCulture),
            result.Session.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(result.Sent);
        if (expectsStaminaUpdate)
        {
            Assert.Contains(updates, update => update.Uid == PlayerUid &&
                update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, expectedStamina)]));
        }
        else
        {
            Assert.DoesNotContain(updates, update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        }

        Assert.Contains("WorldServer handled legacy Omnipotence XPList transition", result.Log);
    }

    [Theory]
    [InlineData(false, 75UL, false, 100, true)]
    [InlineData(true, 140UL, true, 150, true)]
    [InlineData(true, 150UL, true, 150, false)]
    public async Task HandlerThunderRampageWithXpListRestoresStaminaOpensOrRefreshesFortySecondFlagAndSendsEmptyAnimation(
        bool alreadyActive,
        ulong startingStamina,
        bool hasHeavenBlessing,
        ushort expectedStamina,
        bool expectsStaminaUpdate)
    {
        var statusFlags = new List<int> { LegacyXpListFlag, LegacySupermanFlag };
        if (hasHeavenBlessing)
        {
            statusFlags.Add(LegacyHeavenBlessingFlag);
        }

        if (alreadyActive)
        {
            statusFlags.Add(TestThunderRampageFlag);
        }

        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(statusFlags.ToArray()),
            ["XPCount"] = "55",
        };
        if (hasHeavenBlessing)
        {
            attributes["HeavenBlessing"] = "3600";
        }

        var result = await RunLegacyXpSkillActivationAsync(
            TestThunderRampageSpellId,
            sort: 94,
            duration: 40,
            startingStamina: startingStamina,
            initialAttributes: attributes,
            classCode: 9_001);

        AssertLegacyXpSkillOpened(
            result,
            TestThunderRampageSpellId,
            TestThunderRampageFlag,
            RuntimeLegacyThunderRampageExpiresAtUtcAttribute,
            expectedDurationSeconds: 40);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.Equal(
            expectedStamina.ToString(CultureInfo.InvariantCulture),
            result.Session.Character.Attributes["Stamina"]);

        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(TestThunderRampageSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var updates = ParseMsgUpdates(result.Sent);
        if (expectsStaminaUpdate)
        {
            Assert.Contains(updates, update => update.Uid == PlayerUid &&
                update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, expectedStamina)]));
        }
        else
        {
            Assert.DoesNotContain(updates, update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        }

        Assert.Contains("WorldServer handled legacy ThunderRampage XPList transition", result.Log);
        Assert.Contains($"refreshedThunderRampage={alreadyActive}", result.Log);
    }

    [Fact]
    public async Task HandlerThunderRampageWrongClassSpendsResourcesThenReturnsWithoutXpListConsumptionOrAnimation()
    {
        var result = await RunLegacyXpSkillActivationAsync(
            TestThunderRampageSpellId,
            sort: 94,
            duration: 40,
            useMana: 7,
            startingStamina: 50,
            initialAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag),
                ["XPCount"] = "55",
            },
            classCode: 142);

        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, TestThunderRampageFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(RuntimeLegacyThunderRampageExpiresAtUtcAttribute));
        Assert.Equal("55", result.Session.Character.Attributes["XPCount"]);

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or LegacyMsgUpdateDataType.XPCircle or LegacyMsgUpdateDataType.Stamina));
        Assert.Contains("WorldServer source-backed returned legacy ThunderRampage XP skill", result.Log);
        Assert.Contains("AtributesStatus.IsThunderStriker is false", result.Log);
    }

    private static async Task<LegacyXpSkillActivationResult> RunLegacyXpSkillActivationAsync(
        ushort spellId,
        byte sort,
        uint duration,
        IReadOnlyDictionary<string, string> initialAttributes,
        ulong startingStamina = 100,
        ushort? x = null,
        ushort? y = null,
        uint? opponentUid = null,
        ushort useMana = 0,
        int classCode = 142)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                spellId,
                sort,
                useMana: useMana,
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
            stamina: startingStamina,
            spellIds: [spellId],
            attributes: initialAttributes,
            classCode: classCode);
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
                spellId: spellId,
                x: x,
                y: y,
                opponentUid: opponentUid)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        return new LegacyXpSkillActivationResult(
            updatedSession,
            sent,
            log.ToString(),
            beforeCast,
            afterCast);
    }

    private static void AssertLegacyXpSkillOpened(
        LegacyXpSkillActivationResult result,
        ushort spellId,
        int openedStatusFlag,
        string expiresAtAttribute,
        int expectedDurationSeconds)
    {
        Assert.Equal(500, result.Session.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, openedStatusFlag));
        Assert.Equal("0", result.Session.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            expiresAtAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds);
        Assert.True(result.Session.MagicCooldowns is null ||
            !result.Session.MagicCooldowns.ContainsKey(spellId));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        var statusUpdate = Assert.Single(updates, update => update.Uid == PlayerUid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, openedStatusFlag));
    }

    private sealed record LegacyXpSkillActivationResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        string Log,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
