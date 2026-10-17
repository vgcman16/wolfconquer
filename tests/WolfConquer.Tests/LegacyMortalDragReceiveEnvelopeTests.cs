using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int MortalDragReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int MortalDragReceiveEnvelopeSolidFlag = 0x164;
    private const string MortalDragReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string MortalDragReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string MortalDragReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string MortalDragReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerMortalDragPlayerPullRunsReceiveEnvelopeBeforeSinglePublication()
    {
        const uint expectedSageDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateMortalDragRow(
                    MortalDragSpellId,
                    MortalDragSpellId,
                    useMana: 0,
                    useStamina: 0,
                    coolDown: 0,
                    coldTime: 0),
            ],
            gouyuImmortals:
            [
                CreateMortalDragReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedSageDuration)),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [MortalDragSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                [MortalDragReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [MortalDragReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            },
            classCode: 5_005);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "DragTarget",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [MortalDragReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [MortalDragReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveMortalDragSpell(
            casterSession,
            worldData,
            MortalDragSpellId);
        var pulledTargetSession = targetSession with
        {
            Character = targetSession.Character with
            {
                X = casterSession.Character.X,
                Y = casterSession.Character.Y,
            },
        };
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            pulledTargetSession,
            spell,
            worldData,
            1U);

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            casterSession,
            CreateMortalDragQuery(
                MortalDragSpellId,
                TargetPlayerUid,
                x: checked((ushort)targetSession.Character.X),
                y: checked((ushort)targetSession.Character.Y)),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(casterSession.Character.X, updatedTarget.Character.X);
        Assert.Equal(casterSession.Character.Y, updatedTarget.Character.Y);
        Assert.Equal(100_000L - expectedDamage, updatedTarget.Character.MinHitPoints);
        Assert.True(
            HasLegacyStatusFlag(
                updatedCaster.Character.Attributes,
                MortalDragReceiveEnvelopeNinjaSageSpeedModeFlag));
        Assert.True(
            HasLegacyStatusFlag(
                updatedTarget.Character.Attributes,
                MortalDragReceiveEnvelopeSolidFlag));
        Assert.Equal("2400", updatedTarget.Character.Attributes["Solid"]);

        AssertMortalDragReceiveEnvelopeAnimation(casterSent, expectedDamage);
        AssertMortalDragReceiveEnvelopeAnimation(targetSent, expectedDamage);
        AssertMortalDragReceiveFlagBeforeAnimation(
            casterSent,
            PlayerUid,
            MortalDragReceiveEnvelopeNinjaSageSpeedModeFlag,
            expectedSageDuration,
            expectedArchiveDamage: 0);
        AssertMortalDragReceiveFlagBeforeAnimation(
            targetSent,
            TargetPlayerUid,
            MortalDragReceiveEnvelopeSolidFlag,
            expectedDuration: 5,
            expectedArchiveDamage: 2_400);
        AssertMortalDragSingleHitpointPublicationBeforeAnimation(targetSent);
        Assert.Contains("Chance / 100", log.ToString());
    }

    private static void AssertMortalDragReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(834, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(MortalDragSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertMortalDragReceiveFlagBeforeAnimation(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag,
        uint expectedDuration,
        uint expectedArchiveDamage)
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
                    entry.Flag == flag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == expectedArchiveDamage));
        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(frames[index]), out var animation) &&
                animation.SpellId == MortalDragSpellId);

        Assert.True(statusFrameIndex < animationFrameIndex);
        Assert.True(archiveFrameIndex < animationFrameIndex);
    }

    private static void AssertMortalDragSingleHitpointPublicationBeforeAnimation(
        IReadOnlyList<byte[]> frames)
    {
        var maxHitpointFrameIndices = Enumerable.Range(0, frames.Count)
            .Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        var hitpointFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(frames[index]), out var animation) &&
                animation.SpellId == MortalDragSpellId);

        Assert.Equal(2, maxHitpointFrameIndices.Length);
        Assert.True(hitpointFrameIndex < animationFrameIndex);
        Assert.All(maxHitpointFrameIndices, index => Assert.True(index < animationFrameIndex));
    }

    private static GouyuImmortal CreateMortalDragReceiveEnvelopeGouyuImmortal(
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
