using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int BurntFrostReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int BurntFrostReceiveEnvelopeSolidFlag = 0x164;
    private const string BurntFrostReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string BurntFrostReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string BurntFrostReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string BurntFrostReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerBurntFrostAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateBurntFrostRow(BurntFrostSpellId, BurntFrostSpellId)],
            gouyuImmortals:
            [
                CreateBurntFrostReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunBurntFrostReceiveEnvelopeAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BurntFrostReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [BurntFrostReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertBurntFrostReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertBurntFrostReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                BurntFrostReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected BurntFrost player damage to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        BurntFrostReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == BurntFrostReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));
        AssertBurntFrostReceiveUpdatesPrecedeAnimation(
            result.CasterSent,
            PlayerUid,
            BurntFrostReceiveEnvelopeNinjaSageSpeedModeFlag);
        Assert.Contains("Chance / 100", result.Log);
    }

    [Fact]
    public async Task HandlerBurntFrostAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateBurntFrostRow(BurntFrostSpellId, BurntFrostSpellId));
        var result = await RunBurntFrostReceiveEnvelopeAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [BurntFrostReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [BurntFrostReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertBurntFrostReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertBurntFrostReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                BurntFrostReceiveEnvelopeSolidFlag),
            "Expected BurntFrost player damage to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, BurntFrostReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == BurntFrostReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
        AssertBurntFrostReceiveUpdatesPrecedeAnimation(
            result.TargetSent,
            TargetPlayerUid,
            BurntFrostReceiveEnvelopeSolidFlag);
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedDamage,
        string Log)> RunBurntFrostReceiveEnvelopeAsync(
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
            spellIds: [BurntFrostSpellId],
            attributes: casterAttributes,
            classCode: casterClassCode);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "BurntFrostTarget",
            x: 834,
            y: 586,
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
        var (playerSpell, spell) = ResolveBurntFrostSpell(
            casterSession,
            worldData,
            BurntFrostSpellId);
        var expectedBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(expectedBaseDamage * 5 / 7);
        Assert.True(expectedDamage > 0);

        var handled = await InvokeLegacyBurntFrostAsync(
            context,
            casterSession,
            CreateBurntFrostQuery(BurntFrostSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
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

    private static void AssertBurntFrostReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(837, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(BurntFrostSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertBurntFrostReceiveUpdatesPrecedeAnimation(
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
                animation.SpellId == BurntFrostSpellId);

        Assert.True(statusFrameIndex < animationFrameIndex);
        Assert.True(archiveFrameIndex < animationFrameIndex);
    }

    private static GouyuImmortal CreateBurntFrostReceiveEnvelopeGouyuImmortal(
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
