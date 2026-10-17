using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte ShieldBlockSort = 52;
    private const ushort ShieldBlockSpellId = 10_470;
    private const int ShieldBlockStatusFlag = 0x71;

    [Fact]
    public async Task HandlerShieldBlock10470AppliesSourceDamageAndBroadcastsExactPacketSequence()
    {
        const uint duration = 25;
        const uint shieldBlockDamage = 37;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                ShieldBlockSpellId,
                sort: ShieldBlockSort,
                level: 2,
                useMana: 7,
                damage: shieldBlockDamage,
                experience: 900,
                useStamina: 5,
                coolDown: 0,
                coldTime: 0,
                duration: duration,
                damageOnMonster: 9_999));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    ShieldBlockSpellId,
                    Level: 2,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 3),
            ]);
        var viewerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Viewer",
            x: 831,
            y: 584);
        sessions.Set(casterConnection, session);
        sessions.Set(viewerConnection, viewerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveShieldBlockSpell(session, worldData, ShieldBlockSpellId);
        var query = CreateShieldBlockQuery(ShieldBlockSpellId);
        var beforeCast = DateTimeOffset.UtcNow;

        var handled = await InvokeLegacyShieldBlockAsync(
            context,
            session,
            query,
            playerSpell,
            spell);

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(handled);
        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, ShieldBlockStatusFlag));
        Assert.Equal(
            shieldBlockDamage.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["ShieldBlockDamage"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyShieldBlockRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyShieldBlockExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));

        var sentSnapshot = casterSent.ToArray();
        var staminaIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        var manaIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, ShieldBlockStatusFlag)));
        var azureShieldIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ShieldBlockSpellId);
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < statusIndex);
        Assert.True(statusIndex < azureShieldIndex);
        Assert.True(azureShieldIndex < animationIndex);

        var azureShield = Assert.Single(
            ParseMsgUpdates([sentSnapshot[azureShieldIndex]]).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);
        Assert.Equal(0UL, azureShield.Value);
        Assert.Equal((uint)ShieldBlockStatusFlag, azureShield.Flag);
        Assert.Equal(duration, azureShield.Time);
        Assert.Equal(shieldBlockDamage, azureShield.Damage);
        Assert.Equal(2U, azureShield.SpellLevel);

        var animation = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(query.X, animation.X);
        Assert.Equal(query.Y, animation.Y);
        Assert.Equal(ShieldBlockSpellId, animation.SpellId);
        Assert.Equal(2, animation.SpellLevel);
        Assert.Equal(3, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.DoesNotContain(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(ReadFrame(frame), out _));
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));
        Assert.Empty(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.DoesNotContain(
            viewerSent,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana));
        Assert.Equal(
            [LegacyMsgUpdateDataType.StatusFlag, LegacyMsgUpdateDataType.AzureShield],
            ParseMsgUpdates(viewerSent)
                .SelectMany(update => update.Updates)
                .Select(entry => entry.Type)
                .ToArray());
        Assert.Single(ParseSpellAnimations(viewerSent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ShieldBlock magic", output);
        Assert.Contains("spellId=10470", output);
        Assert.Contains("sort=52", output);
        Assert.Contains("statusFlag=113", output);
        Assert.Contains("duration=25", output);
        Assert.Contains("shieldBlockDamage=37", output);
        Assert.Contains("source=ShieldBlock.cs spell 10470", output);
    }

    [Theory]
    [InlineData(ShieldBlockSpellId, ShieldBlockSort - 1)]
    [InlineData(ShieldBlockSpellId + 1, ShieldBlockSort)]
    public async Task HandlerShieldBlockRejectsUnownedSpellOrSortWithoutSpendingResources(
        ushort spellId,
        byte sort)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                spellId,
                sort,
                useMana: 7,
                damage: 40,
                useStamina: 5,
                coolDown: 0,
                coldTime: 0,
                duration: 25));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [spellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveShieldBlockSpell(session, worldData, spellId);

        var handled = await InvokeLegacyShieldBlockAsync(
            context,
            session,
            CreateShieldBlockQuery(spellId),
            playerSpell,
            spell);

        Assert.False(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Equal(500, unchanged.Character.MinMana);
        Assert.Equal("100", unchanged.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(unchanged.Character.Attributes, ShieldBlockStatusFlag));
        Assert.False(unchanged.Character.Attributes.ContainsKey("ShieldBlockDamage"));
    }

    [Fact]
    public async Task HandlerShieldBlockResourceFailureDoesNotApplyStatusOrSendAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                ShieldBlockSpellId,
                sort: ShieldBlockSort,
                useMana: 7,
                damage: 40,
                useStamina: 50,
                coolDown: 0,
                coldTime: 0,
                duration: 25));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            minMana: 500,
            stamina: 49,
            spellIds: [ShieldBlockSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveShieldBlockSpell(session, worldData, ShieldBlockSpellId);

        var handled = await InvokeLegacyShieldBlockAsync(
            context,
            session,
            CreateShieldBlockQuery(ShieldBlockSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Equal(500, unchanged.Character.MinMana);
        Assert.Equal("49", unchanged.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(unchanged.Character.Attributes, ShieldBlockStatusFlag));
        Assert.False(unchanged.Character.Attributes.ContainsKey("ShieldBlockDamage"));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains("requiredStamina=50", log.ToString());
    }

    [Fact]
    public async Task RuntimeLoopExpiresShieldBlockStatusAndRuntimeAttributes()
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
                ["StatusFlags"] = BuildStatusFlags(ShieldBlockStatusFlag),
                ["LegacyShieldBlockExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyShieldBlockRemoveOnDead"] = "true",
                ["ShieldBlockDamage"] = "37",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, ShieldBlockStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyShieldBlockExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyShieldBlockRemoveOnDead"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("ShieldBlockDamage"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, ShieldBlockStatusFlag)));
            Assert.Contains("WorldServer expired legacy ShieldBlock flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateShieldBlockQuery(ushort spellId)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 847,
            Y: 592,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveShieldBlockSpell(
            WorldSession session,
            WorldDataCatalog worldData,
            ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static async Task<bool> InvokeLegacyShieldBlockAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerShieldBlockMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<Task<bool>>(result);
    }
}
