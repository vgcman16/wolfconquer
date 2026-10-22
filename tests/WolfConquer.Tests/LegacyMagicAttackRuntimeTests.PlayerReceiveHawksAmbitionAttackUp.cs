using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int LegacyAttackUpFlag = 0xE9;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesHawksAmbitionAndAttackUpAfterExistingReceiveModifiers()
    {
        const uint hookMoonPower = 140;
        const uint cleanSweepPower = 25;
        const uint axeShadowPower = 10;
        const uint hawksAmbitionPower = 20;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyHookMoonCasterFlag,
                    LegacyCleanSweepFlag,
                    AxeShadowStatusSetterFlag,
                    LegacyAttackUpFlag),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
                ["CleanSweepPower"] = cleanSweepPower.ToString(CultureInfo.InvariantCulture),
                ["AxeShadowPower"] = axeShadowPower.ToString(CultureInfo.InvariantCulture),
                ["HawksAmbitionPower"] = hawksAmbitionPower.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyHawksAmbitionFlag),
            });
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

        var afterHookMoon = MultiplyLegacyReceivePercent(ExpectedPlayerThunderDamage, hookMoonPower);
        var afterCleanSweep = AddLegacyReceivePercent(afterHookMoon, cleanSweepPower);
        var afterAxeShadow = AddLegacyReceivePercent(afterCleanSweep, axeShadowPower);
        var afterHawksAmbition = afterAxeShadow - MultiplyLegacyReceivePercent(afterAxeShadow, hawksAmbitionPower);
        var expectedModifiedDamage = checked(afterHawksAmbition * 2);
        Assert.Equal(589U, afterHookMoon);
        Assert.Equal(736U, afterCleanSweep);
        Assert.Equal(809U, afterAxeShadow);
        Assert.Equal(648U, afterHawksAmbition);
        Assert.Equal(1_296U, expectedModifiedDamage);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - expectedModifiedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyHawksAmbitionFlag));

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedModifiedDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }
}
