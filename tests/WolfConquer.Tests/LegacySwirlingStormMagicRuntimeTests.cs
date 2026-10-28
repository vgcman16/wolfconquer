using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SwirlingStormRuntimeSpellId = 12_890;
    private const byte SwirlingStormRuntimeSort = 91;
    private const int SwirlingStormRuntimeOmnipotenceFlag = 0xC0;

    [Fact]
    public async Task HandlerSwirlingStormHitsIncreasedRangeMixedTargetsWithSourceScalingAndHitBits()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var widenedSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 836,
            boundY: 592);
        var outsideWidenedSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 2,
            boundX: 836,
            boundY: 593);
        var sobNpc = CreateMoveLineSobNpc(id: 62_890, x: 837, y: 592);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [widenedSpawn, outsideWidenedSpawn],
            [sobNpc],
            [CreateSwirlingStormRuntimeRow()]);
        var widenedMonster = CreateRuntimeMonsterInstance(worldData, widenedSpawn, monsterFamily);
        var outsideWidenedMonster = CreateRuntimeMonsterInstance(
            worldData,
            outsideWidenedSpawn,
            monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SwirlingStormRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SwirlingStormTarget",
            x: 838,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var spell = ResolveSwirlingStormRuntimeSpell(casterSession, worldData).Spell;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 9 / 15;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 40 / 100;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSwirlingStormRuntimeQuery()).ToFrameBytes()));

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(SwirlingStormRuntimeSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.Equal(
                [widenedMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id)],
                animation.Targets.Select(target => target.Uid).ToArray());
        }

        var monsterAnimationTarget = casterAnimation.Targets[0];
        Assert.True(monsterAnimationTarget.Hit);
        Assert.True(monsterAnimationTarget.Damage > 0);
        var playerAnimationTarget = casterAnimation.Targets[1];
        Assert.Equal(expectedPlayerDamage, playerAnimationTarget.Damage);
        Assert.False(playerAnimationTarget.Hit);
        var sobNpcAnimationTarget = casterAnimation.Targets[2];
        Assert.Equal(expectedSobNpcDamage, sobNpcAnimationTarget.Damage);
        Assert.True(sobNpcAnimationTarget.Hit);

        Assert.True(monsterStates.TryGetVisibleInstance(
            widenedMonster,
            DateTimeOffset.UtcNow,
            out var visibleWidenedMonster));
        Assert.Equal(
            widenedMonster.MaxLife - monsterAnimationTarget.Damage,
            visibleWidenedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(
            outsideWidenedMonster,
            DateTimeOffset.UtcNow,
            out var visibleOutsideWidenedMonster));
        Assert.Equal(outsideWidenedMonster.MaxLife, visibleOutsideWidenedMonster.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            sobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SwirlingStorm magic", output);
        Assert.Contains("spellId=12890", output);
        Assert.Contains("sort=91", output);
        Assert.Contains("range=13", output);
        Assert.Contains("width=2", output);
        Assert.Contains("increasedRange=true", output);
        Assert.Contains("targets=3", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=matching DecompiledSource SwirlingStorm.cs", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("gap=unsupported-magic", output);
    }

    [Fact]
    public async Task HandlerSwirlingStormOrdersResourcesOmnipotenceProgressAndAnimationLikeSource()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [
                CreateSwirlingStormRuntimeRow(
                    useMana: 7,
                    useStamina: 5,
                    experience: 1),
                CreateSwirlingStormRuntimeRow(level: 1, experience: 1_000),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            strength: 300,
            stamina: 104,
            spellIds: [SwirlingStormRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(SwirlingStormRuntimeOmnipotenceFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSwirlingStormRuntimeQuery()).ToFrameBytes()));

        var staminaUpdates = FindSwirlingStormRuntimeScalarUpdates(
            sent,
            LegacyMsgUpdateDataType.Stamina);
        Assert.Equal([99UL, 109UL], staminaUpdates.Select(update => update.Value).ToArray());
        var manaUpdate = Assert.Single(FindSwirlingStormRuntimeScalarUpdates(
            sent,
            LegacyMsgUpdateDataType.Mana));
        Assert.Equal(493UL, manaUpdate.Value);
        var coldTimeIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMagicColdTimePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.Spells.Any(spell => spell.SpellId == SwirlingStormRuntimeSpellId));
        var experienceIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellPacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.Id == SwirlingStormRuntimeSpellId);
        var animationIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out var animation) &&
                animation.SpellId == SwirlingStormRuntimeSpellId);

        Assert.True(staminaUpdates[0].Index < manaUpdate.Index);
        Assert.True(manaUpdate.Index < coldTimeIndex);
        Assert.True(coldTimeIndex < staminaUpdates[1].Index);
        Assert.True(staminaUpdates[1].Index < experienceIndex);
        Assert.True(experienceIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);
        Assert.Equal(sent.Count - 1, animationIndex);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, animationTarget.Uid);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("109", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.Character.Experience > 0);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(SwirlingStormRuntimeSpellId));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(SwirlingStormRuntimeSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("stamina=99 restoredStamina=109 threshold=100", output);
        Assert.Contains("WorldServer sent legacy SwirlingStorm experience", output);
        Assert.Contains("WorldServer updated legacy player magic spell", output);
        Assert.Contains("WorldServer handled legacy SwirlingStorm magic", output);
    }

    [Theory]
    [InlineData(false, 104UL, 109U)]
    [InlineData(true, 154UL, 159U)]
    public async Task HandlerSwirlingStormOmnipotenceAddsTenWithoutClampingToThreshold(
        bool heavenBlessing,
        ulong initialStamina,
        uint expectedStamina)
    {
        var result = await RunSwirlingStormRuntimeStaminaCastAsync(
            initialStamina,
            heavenBlessing,
            useStamina: 5);

        var staminaUpdates = FindSwirlingStormRuntimeScalarUpdates(
            result.Sent,
            LegacyMsgUpdateDataType.Stamina);
        Assert.Equal(
            [initialStamina - 5, expectedStamina],
            staminaUpdates.Select(update => update.Value).ToArray());
        Assert.Equal(
            expectedStamina.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Session.Character.Attributes["Stamina"]);
        Assert.Contains(
            $"restoredStamina={expectedStamina} threshold={(heavenBlessing ? 150 : 100)}",
            result.Log);
        Assert.Single(ParseSpellAnimations(result.Sent));
    }

    [Theory]
    [InlineData(false, 105UL, 100U)]
    [InlineData(true, 155UL, 150U)]
    public async Task HandlerSwirlingStormOmnipotenceDoesNotAddAtSourceThreshold(
        bool heavenBlessing,
        ulong initialStamina,
        uint expectedStamina)
    {
        var result = await RunSwirlingStormRuntimeStaminaCastAsync(
            initialStamina,
            heavenBlessing,
            useStamina: 5);

        var staminaUpdate = Assert.Single(FindSwirlingStormRuntimeScalarUpdates(
            result.Sent,
            LegacyMsgUpdateDataType.Stamina));
        Assert.Equal((ulong)expectedStamina, staminaUpdate.Value);
        Assert.Equal(
            expectedStamina.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Session.Character.Attributes["Stamina"]);
        Assert.DoesNotContain("restored legacy SwirlingStorm Omnipotence stamina", result.Log);
        Assert.Single(ParseSpellAnimations(result.Sent));
    }

    private static async Task<SwirlingStormRuntimeStaminaCastResult>
        RunSwirlingStormRuntimeStaminaCastAsync(
            ulong initialStamina,
            bool heavenBlessing,
            byte useStamina)
    {
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [],
            [CreateSwirlingStormRuntimeRow(useStamina: useStamina)]);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(SwirlingStormRuntimeOmnipotenceFlag),
        };
        if (heavenBlessing)
        {
            attributes["HeavenBlessing"] = "1";
        }

        var session = CreateSession(
            stamina: initialStamina,
            spellIds: [SwirlingStormRuntimeSpellId],
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSwirlingStormRuntimeQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new SwirlingStormRuntimeStaminaCastResult(
            sent,
            updatedSession,
            log.ToString());
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveSwirlingStormRuntimeSpell(
            WorldSession session,
            WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            SwirlingStormRuntimeSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateSwirlingStormRuntimeRow(
        byte level = 0,
        ushort useMana = 0,
        byte useStamina = 25,
        uint experience = 100_000)
    {
        return CreateMagicRow(
            SwirlingStormRuntimeSpellId,
            sort: SwirlingStormRuntimeSort,
            level: level,
            useMana: useMana,
            damage: 30_400,
            experience: experience,
            maxTargets: 6,
            range: 13,
            useStamina: useStamina,
            coolDown: 800,
            coldTime: 1_500,
            damageOnHuman: 30_320,
            damageOnMonster: 30_200);
    }

    private static MsgAttackQuery CreateSwirlingStormRuntimeQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 842,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: SwirlingStormRuntimeSpellId);
    }

    private static IReadOnlyList<SwirlingStormRuntimeScalarUpdate>
        FindSwirlingStormRuntimeScalarUpdates(
            IReadOnlyList<byte[]> frames,
            LegacyMsgUpdateDataType type)
    {
        var updates = new List<SwirlingStormRuntimeScalarUpdate>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) ||
                update.Updates.Count != 1 ||
                update.Updates[0].Type != type)
            {
                continue;
            }

            updates.Add(new SwirlingStormRuntimeScalarUpdate(index, update.Updates[0].Value));
        }

        return updates;
    }

    private sealed record SwirlingStormRuntimeStaminaCastResult(
        IReadOnlyList<byte[]> Sent,
        WorldSession Session,
        string Log);

    private readonly record struct SwirlingStormRuntimeScalarUpdate(int Index, ulong Value);
}
