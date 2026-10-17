using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerGapingWoundsCanAttackPlayerAcceptedTargetAppliesCasterFlashingName()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_000,
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        var targetSession = CreateSession(
            mapId: 1_000,
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: ExpectedGapingWoundsPlayerDamage);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyFlashingNameFlag));

        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            AssertGapingWoundsAnimationHeader(
                spell,
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y));
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.True(target.Hit);
        }

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy player-target magic flashing-name", output);
        Assert.Contains(
            "source=CanAttackPlayer.Verified flashingname AddFlag(FlashingName, 30, RemoveOnDead: true)",
            output);
        Assert.Contains("WorldServer handled legacy GapingWounds magic", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }
}
