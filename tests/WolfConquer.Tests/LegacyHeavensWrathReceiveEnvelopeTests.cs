using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int HeavensWrathReceiveEnvelopeSolidFlag = 0x164;
    private const string HeavensWrathReceiveEnvelopeSpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string HeavensWrathReceiveEnvelopeSpeffectRollAttribute = "LegacySpeffectRoll";

    [Fact]
    public async Task HandlerHeavensWrathActiveRecastRunsReceiveEnvelopeBeforeSingleDamagePublication()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateHeavensWrathRuntimeRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var expiry = DateTimeOffset.UtcNow.AddSeconds(37).ToString("O");
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            strength: 300,
            stamina: 100,
            spellIds: [HeavensWrathRuntimeSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["StatusFlags"] = BuildStatusFlags(
                    HeavensWrathRuntimeStatusFlag,
                    HeavensWrathRuntimeFlyFlag),
                [WorldPacketHandlers.RuntimeLegacyHeavensWrathExpiresAtUtcAttribute] = expiry,
                [WorldPacketHandlers.RuntimeLegacyHeavensWrathRemoveOnDeadAttribute] = "true",
                [WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute] = expiry,
                ["LegacyFlyRemoveOnDead"] = "true",
            },
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "HeavensWrathTarget",
            x: 842,
            y: 584,
            minHitPoints: 100_000,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HeavensWrathReceiveEnvelopeSpeffectLevelAttribute + ".Solid"] = "4",
                [HeavensWrathReceiveEnvelopeSpeffectRollAttribute + ".Solid"] = "0",
            });
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
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            HeavensWrathRuntimeSpellId,
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
            context,
            ReadFrame(new MsgAttackPacket(CreateHeavensWrathRuntimeQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000L - expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(
            HasLegacyStatusFlag(
                updatedTargetSession.Character.Attributes,
                HeavensWrathReceiveEnvelopeSolidFlag));
        Assert.Equal("2400", updatedTargetSession.Character.Attributes["Solid"]);

        AssertHeavensWrathReceiveEnvelopeAnimation(casterSent, expectedDamage);
        AssertHeavensWrathReceiveEnvelopeAnimation(targetSent, expectedDamage);
        AssertHeavensWrathReceiveEnvelopePublicationOrder(targetSent);
        Assert.Contains("speffect Solid", log.ToString());
    }

    private static void AssertHeavensWrathReceiveEnvelopeAnimation(
        IEnumerable<byte[]> frames,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(HeavensWrathRuntimeSpellId, animation.SpellId);
        Assert.Equal(1, animation.SpellEffect);
        AssertBombEqualsOneIfParserSupportsIt(animation);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertHeavensWrathReceiveEnvelopePublicationOrder(
        IReadOnlyList<byte[]> frames)
    {
        var statusFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, HeavensWrathReceiveEnvelopeSolidFlag)));
        var archiveFrameIndex = Assert.Single(
            Enumerable.Range(0, frames.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == HeavensWrathReceiveEnvelopeSolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
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
                animation.SpellId == HeavensWrathRuntimeSpellId);

        Assert.Equal(2, maxHitpointFrameIndices.Length);
        Assert.True(statusFrameIndex < hitpointFrameIndex);
        Assert.True(archiveFrameIndex < hitpointFrameIndex);
        Assert.True(hitpointFrameIndex < animationFrameIndex);
        Assert.All(maxHitpointFrameIndices, index => Assert.True(index < animationFrameIndex));
    }
}
