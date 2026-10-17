using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int AttackStatusReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int AttackStatusReceiveEnvelopeSolidFlag = 0x164;
    private const string AttackStatusReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string AttackStatusReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string AttackStatusReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string AttackStatusReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerDefaultAttackStatusAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(
                    PhoenixSpellId,
                    sort: 16,
                    useMana: 7,
                    damage: 120,
                    range: 9,
                    weaponType: 420_620,
                    coldTime: 0),
            ],
            gouyuImmortals:
            [
                CreateAttackStatusReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunDefaultAttackStatusReceiveEnvelopePhoenixAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AttackStatusReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [AttackStatusReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertDefaultAttackStatusReceiveEnvelopePhoenix(result.CasterSent, result.ExpectedDamage);
        AssertDefaultAttackStatusReceiveEnvelopePhoenix(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                AttackStatusReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected default AttackStatus player damage to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        AttackStatusReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AttackStatusReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));
    }

    [Fact]
    public async Task HandlerDefaultAttackStatusAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                PhoenixSpellId,
                sort: 16,
                useMana: 7,
                damage: 120,
                range: 9,
                weaponType: 420_620,
                coldTime: 0));
        var result = await RunDefaultAttackStatusReceiveEnvelopePhoenixAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AttackStatusReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [AttackStatusReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertDefaultAttackStatusReceiveEnvelopePhoenix(result.CasterSent, result.ExpectedDamage);
        AssertDefaultAttackStatusReceiveEnvelopePhoenix(result.TargetSent, result.ExpectedDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                AttackStatusReceiveEnvelopeSolidFlag),
            "Expected default AttackStatus player damage to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatusReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == AttackStatusReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedDamage)> RunDefaultAttackStatusReceiveEnvelopePhoenixAsync(
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
            strength: 300,
            minMana: 50,
            spellIds: [PhoenixSpellId],
            attributes: casterAttributes,
            classCode: casterClassCode,
            legacyRandomSpell: PhoenixSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, PhoenixSpellId, 0, out var spell));
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            4U);
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
                SpellId: PhoenixSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        Assert.Equal(0, updatedCasterSession.LegacyRandomSpell);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000L - expectedDamage, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AttackStatus magic", output);
        Assert.Contains("handledTarget=True", output);
        Assert.Contains("targets=1", output);
        Assert.Contains($"totalDamage={expectedDamage}", output);

        return (
            casterSent,
            targetSent,
            updatedCasterSession,
            updatedTargetSession,
            expectedDamage);
    }

    private static void AssertDefaultAttackStatusReceiveEnvelopePhoenix(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(checked((ushort)832), animation.X);
        Assert.Equal(checked((ushort)584), animation.Y);
        Assert.Equal(PhoenixSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static GouyuImmortal CreateAttackStatusReceiveEnvelopeGouyuImmortal(
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
