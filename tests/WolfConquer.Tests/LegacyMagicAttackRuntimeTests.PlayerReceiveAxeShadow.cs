using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesAxeShadowReceiveDamageAfterHookMoonAndCleanSweep()
    {
        const uint hookMoonPower = 140;
        const uint cleanSweepPower = 25;
        const uint axeShadowPower = 10;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
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
                    AxeShadowStatusSetterFlag),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
                ["CleanSweepPower"] = cleanSweepPower.ToString(CultureInfo.InvariantCulture),
                ["AxeShadowPower"] = axeShadowPower.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
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
        var expectedModifiedDamage = AddLegacyReceivePercent(afterCleanSweep, axeShadowPower);
        Assert.Equal(809U, expectedModifiedDamage);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - expectedModifiedDamage, updatedTargetSession.Character.MinHitPoints);

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

    private static uint AddLegacyReceivePercent(uint damage, uint percent)
    {
        return checked(damage + MultiplyLegacyReceivePercent(damage, percent));
    }

    private static uint MultiplyLegacyReceivePercent(uint damage, uint percent)
    {
        return checked(damage * percent / 100);
    }
}
