using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PhysicalSpellsReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int PhysicalSpellsReceiveEnvelopeSolidFlag = 0x164;
    private const string PhysicalSpellsReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string PhysicalSpellsReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string PhysicalSpellsReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string PhysicalSpellsReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateTripleAttackPhysicalSpellsRow()],
            gouyuImmortals:
            [
                CreatePhysicalSpellsReceiveEnvelopeGouyuImmortal(
                    level: 17,
                    chance: 5_000,
                    rate: 0,
                    seconds: checked((int)expectedDuration)),
            ]);

        var result = await RunPhysicalSpellsReceiveEnvelopeTripleAttackAsync(
            worldData,
            casterClassCode: 5_005,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PhysicalSpellsReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [PhysicalSpellsReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });

        AssertPhysicalSpellsReceiveEnvelopeTripleAttack(result.CasterSent, result.ExpectedHitDamage);
        AssertPhysicalSpellsReceiveEnvelopeTripleAttack(result.TargetSent, result.ExpectedHitDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.CasterSession.Character.Attributes,
                PhysicalSpellsReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected TripleAttack PhysicalSpells to run ReceiveAttack.Player Ninja SageMode using Chance while Rate is zero.");
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(
                        entry.Values,
                        PhysicalSpellsReceiveEnvelopeNinjaSageSpeedModeFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == PhysicalSpellsReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration &&
                    entry.Damage == 0));

        Assert.Contains("targetKind=Player", result.Log);
        Assert.Contains("Chance / 100", result.Log);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateTripleAttackPhysicalSpellsRow());
        var result = await RunPhysicalSpellsReceiveEnvelopeTripleAttackAsync(
            worldData,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PhysicalSpellsReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [PhysicalSpellsReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertPhysicalSpellsReceiveEnvelopeTripleAttack(result.CasterSent, result.ExpectedHitDamage);
        AssertPhysicalSpellsReceiveEnvelopeTripleAttack(result.TargetSent, result.ExpectedHitDamage);
        Assert.True(
            HasLegacyStatusFlag(
                result.TargetSession.Character.Attributes,
                PhysicalSpellsReceiveEnvelopeSolidFlag),
            "Expected TripleAttack PhysicalSpells to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, PhysicalSpellsReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == PhysicalSpellsReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));

        Assert.Contains("WorldServer applied legacy ReceiveAttack.Player speffect Solid", result.Log);
    }

    private static async Task<(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession CasterSession,
        WorldSession TargetSession,
        uint ExpectedHitDamage,
        string Log)> RunPhysicalSpellsReceiveEnvelopeTripleAttackAsync(
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
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            attributes: casterAttributes,
            classCode: casterClassCode,
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            casterSession.Character,
            casterSession.PlayerItems,
            worldData,
            checked((uint)casterSession.Character.MinHitPoints),
            prestigeLevel: 0);
        var basePhysicalDamage = attackerFields[LegacyPlayerStatusPacket.MinAttackIndex];
        var expectedHitDamage = basePhysicalDamage * 75U / 100U / 2U;

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
                SpellId: TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(
            5_000L - checked((long)(expectedHitDamage * 3UL)),
            updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", log.ToString());
        Assert.Contains("targets=3", log.ToString());

        return (
            casterSent,
            targetSent,
            updatedCasterSession,
            updatedTargetSession,
            expectedHitDamage,
            log.ToString());
    }

    private static void AssertPhysicalSpellsReceiveEnvelopeTripleAttack(
        IEnumerable<byte[]> frames,
        uint expectedHitDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(TripleAttackPhysicalSpellsSpellId, animation.SpellId);
        Assert.Equal(3, animation.Targets.Count);
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedHitDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
    }

    private static GouyuImmortal CreatePhysicalSpellsReceiveEnvelopeGouyuImmortal(
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
