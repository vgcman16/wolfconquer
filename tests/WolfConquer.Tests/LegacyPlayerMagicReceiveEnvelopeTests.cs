using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int ReceiveEnvelopeSolidFlag = 0x164;
    private const string ReceiveEnvelopeNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string ReceiveEnvelopeNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string ReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string ReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerOrdinaryThunderAgainstVisiblePlayerRunsReceiveEnvelopeNinjaSageModeUsingChance()
    {
        const uint expectedDuration = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0)],
            items: [CreateRetainedNativeMagic421Helmet()],
            gouyuImmortals: [CreateReceiveEnvelopeGouyuImmortal(level: 17, chance: 5_000, rate: 0, seconds: 10)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            classCode: 5_005,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ReceiveEnvelopeNinjaLevelsAttribute] = "17",
                [ReceiveEnvelopeNinjaSageModeRollAttribute] = "0",
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
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
                SpellId: ThunderSpellId)).ToFrameBytes()));

        AssertNormalThunderDamage(casterSent);
        AssertNormalThunderDamage(targetSent);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(
            HasLegacyStatusFlag(
                updatedCasterSession.Character.Attributes,
                ReceiveEnvelopeNinjaSageSpeedModeFlag),
            "Expected ordinary player-target Thunder to run ReceiveAttack.Player Ninja SageMode.");
        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == ReceiveEnvelopeNinjaSageSpeedModeFlag &&
                    entry.Time == expectedDuration));
    }

    [Fact]
    public async Task HandlerOrdinaryThunderAgainstVisiblePlayerRunsReceiveEnvelopeDefenderSolidSpeffect()
    {
        var result = await RunPlayerReceiveResponseEffectsMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [ReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });

        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
        Assert.True(
            HasLegacyStatusFlag(result.TargetSession.Character.Attributes, ReceiveEnvelopeSolidFlag),
            "Expected ordinary player-target Thunder to run defender speffect_attribute group 3.");
        Assert.Equal("2400", result.TargetSession.Character.Attributes["Solid"]);
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, ReceiveEnvelopeSolidFlag)));
        Assert.Contains(
            ParseMsgUpdates(result.TargetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == ReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
    }

    private static GouyuImmortal CreateReceiveEnvelopeGouyuImmortal(
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
