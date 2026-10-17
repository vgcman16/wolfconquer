using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int SwirlingStormReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int SwirlingStormReceiveEnvelopeSolidFlag = 0x164;
    private const string SwirlingStormReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string SwirlingStormReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string SwirlingStormReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string SwirlingStormReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerSwirlingStormAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateSwirlingStormRuntimeRow()],
            gouyuImmortals:
            [
                CreateSwirlingStormReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunSwirlingStormReceiveEnvelopeAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                [SwirlingStormReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [SwirlingStormReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertSwirlingStormReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertSwirlingStormReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                SwirlingStormReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected SwirlingStorm player damage to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        SwirlingStormReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == SwirlingStormReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));
        AssertSwirlingStormReceiveUpdatesPrecedeAnimation(
            result.CasterSent,
            PlayerUid,
            SwirlingStormReceiveEnvelopeNinjaSageSpeedModeFlag);
        Assert.Contains("Chance / 100", result.Log);
    }

    [Fact]
    public async Task HandlerSwirlingStormAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSwirlingStormRuntimeRow());
        var result = await RunSwirlingStormReceiveEnvelopeAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SwirlingStormReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [SwirlingStormReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertSwirlingStormReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertSwirlingStormReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                SwirlingStormReceiveEnvelopeSolidFlag),
            "Expected SwirlingStorm player damage to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, SwirlingStormReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == SwirlingStormReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
        AssertSwirlingStormReceiveUpdatesPrecedeAnimation(
            result.TargetSent,
            TargetPlayerUid,
            SwirlingStormReceiveEnvelopeSolidFlag);
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedDamage,
        string Log)> RunSwirlingStormReceiveEnvelopeAsync(
        WorldDataCatalog worldData,
        int casterClassCode = 142,
        IReadOnlyDictionary<string, string>? casterAttributes = null,
        IReadOnlyDictionary<string, string>? targetAttributes = null)
    {
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            strength: 300,
            stamina: 100,
            spellIds: [SwirlingStormRuntimeSpellId],
            attributes: casterAttributes ?? new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            },
            classCode: casterClassCode);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SwirlingStormTarget",
            x: 838,
            y: 584,
            minHitPoints: 100_000,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var spell = ResolveSwirlingStormRuntimeSpell(casterSession, worldData).Spell;
        var expectedBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(expectedBaseDamage * 9 / 15);
        Assert.True(expectedDamage > 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSwirlingStormRuntimeQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(
            100_000L - expectedDamage,
            updatedTargetSession.Character.MinHitPoints);

        return (
            casterSent,
            targetSent,
            updatedCasterSession,
            updatedTargetSession,
            expectedDamage,
            log.ToString());
    }

    private static void AssertSwirlingStormReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(842, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(SwirlingStormRuntimeSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertSwirlingStormReceiveUpdatesPrecedeAnimation(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag)
    {
        var statusFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, flag)));
        var archiveFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == flag));
        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(frames[index]), out var animation) &&
                animation.SpellId == SwirlingStormRuntimeSpellId);

        Assert.True(statusFrameIndex < animationFrameIndex);
        Assert.True(archiveFrameIndex < animationFrameIndex);
    }

    private static GouyuImmortal CreateSwirlingStormReceiveEnvelopeGouyuImmortal(
        int level,
        int chance,
        int rate,
        int seconds)
    {
        var values = Enumerable.Repeat(0, 15).ToArray();
        values[0] = level;
        values[5] = chance;
        values[6] = seconds;
        values[7] = rate;
        return new GouyuImmortal(
            Id: level,
            Values: values,
            SourceFile: "fixture://gouyu_immortal",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }
}
