using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int AirKickReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int AirKickReceiveEnvelopeSolidFlag = 0x164;
    private const string AirKickReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string AirKickReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string AirKickReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string AirKickReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerAirKickAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateAirKickRuntimeRow()],
            gouyuImmortals:
            [
                CreateAirKickReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunAirKickReceiveEnvelopeAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AirKickReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [AirKickReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertAirKickReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertAirKickReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                AirKickReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected AirKick player damage to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        AirKickReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AirKickReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));
        AssertAirKickReceiveUpdatesPrecedeAnimation(
            result.CasterSent,
            PlayerUid,
            AirKickReceiveEnvelopeNinjaSageSpeedModeFlag);
        Assert.Contains("Chance / 100", result.Log);
    }

    [Fact]
    public async Task HandlerAirKickAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAirKickRuntimeRow());
        var result = await RunAirKickReceiveEnvelopeAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AirKickReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [AirKickReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertAirKickReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertAirKickReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                AirKickReceiveEnvelopeSolidFlag),
            "Expected AirKick player damage to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AirKickReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AirKickReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
        AssertAirKickReceiveUpdatesPrecedeAnimation(
            result.TargetSent,
            TargetPlayerUid,
            AirKickReceiveEnvelopeSolidFlag);
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedDamage,
        string Log)> RunAirKickReceiveEnvelopeAsync(
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
            spellIds: [AirKickRuntimeSpellId],
            attributes: casterAttributes,
            classCode: casterClassCode);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "AirKickTarget",
            x: 836,
            y: 585,
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
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(
            casterSession,
            worldData,
            AirKickRuntimeSpellId);
        var movedCasterSession = casterSession with
        {
            Character = casterSession.Character with { X = 838, Y = 584 },
        };
        var expectedBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            movedCasterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(expectedBaseDamage * 13 / 5);
        Assert.True(expectedDamage > 0);

        var handled = await InvokeLegacyAirKickAsync(
            context,
            casterSession,
            CreateAirKickRuntimeQuery(),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(838, updatedCasterSession.Character.X);
        Assert.Equal(584, updatedCasterSession.Character.Y);
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

    private static void AssertAirKickReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(838, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(AirKickRuntimeSpellId, animation.SpellId);
        Assert.Equal(AirKickRuntimeNextSpellId, animation.NextSpell);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertAirKickReceiveUpdatesPrecedeAnimation(
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
                animation.SpellId == AirKickRuntimeSpellId);

        Assert.True(statusFrameIndex < animationFrameIndex);
        Assert.True(archiveFrameIndex < animationFrameIndex);
    }

    private static GouyuImmortal CreateAirKickReceiveEnvelopeGouyuImmortal(
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
