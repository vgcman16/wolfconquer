using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int AttackStatus7000ReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int AttackStatus7000ReceiveEnvelopeSolidFlag = 0x164;
    private const string AttackStatus7000ReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string AttackStatus7000ReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string AttackStatus7000ReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string AttackStatus7000ReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerAttackStatus7000AgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateAttackStatus7000Row(range: 7)],
            gouyuImmortals:
            [
                CreateAttackStatus7000ReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunAttackStatus7000ReceiveEnvelopeAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AttackStatus7000ReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [AttackStatus7000ReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertAttackStatus7000ReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertAttackStatus7000ReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                AttackStatus7000ReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected AttackStatus 7000 player damage to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        AttackStatus7000ReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AttackStatus7000ReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));
        Assert.Contains("Chance / 100", result.Log);
    }

    [Fact]
    public async Task HandlerAttackStatus7000AgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAttackStatus7000Row(range: 7));
        var result = await RunAttackStatus7000ReceiveEnvelopeAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AttackStatus7000ReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [AttackStatus7000ReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertAttackStatus7000ReceiveEnvelopeAnimation(result.CasterSent, result.ExpectedDamage);
        AssertAttackStatus7000ReceiveEnvelopeAnimation(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                AttackStatus7000ReceiveEnvelopeSolidFlag),
            "Expected AttackStatus 7000 player damage to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatus7000ReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AttackStatus7000ReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedDamage,
        string Log)> RunAttackStatus7000ReceiveEnvelopeAsync(
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
            minMana: 50,
            strength: 300,
            spellIds: [AttackStatus7000SpellId],
            attributes: casterAttributes,
            classCode: casterClassCode);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 835,
            y: 584,
            minHitPoints: 100_000,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            AttackStatus7000SpellId,
            out _,
            out var spell));
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        Assert.True(expectedDamage > 0);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AttackStatus7000SpellId)).ToFrameBytes()));

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

    private static void AssertAttackStatus7000ReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(AttackStatus7000SpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static GouyuImmortal CreateAttackStatus7000ReceiveEnvelopeGouyuImmortal(
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
