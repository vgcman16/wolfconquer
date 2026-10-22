using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyLightningShieldSpellId = 15_750;
    private const int LegacyLightningShieldActivatedFlag = 0xED;
    private const int LegacyLightningShieldFlag = 0xF0;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerActivatesAndDrainsLightningShield()
    {
        const uint expectedActivationShieldLeft = 26_000;
        const uint expectedDrainedShieldLeft = expectedActivationShieldLeft - ExpectedPlayerThunderDamage;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(LegacyLightningShieldSpellId, sort: 0, level: 3, useMana: 0, damage: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellEntries:
            [
                CreatePlayerSpell(LegacyLightningShieldSpellId, level: 3),
            ],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyLightningShieldFlag),
                ["LegacyLightningShieldRoll"] = "0",
                ["FlashShieldLevel"] = "2",
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

        var azureShieldEntries = ParseMsgUpdates(targetSent)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield)
            .ToArray();
        Assert.Equal(2, azureShieldEntries.Length);

        var activationUpdate = azureShieldEntries[0];
        Assert.Equal(0UL, activationUpdate.Value);
        Assert.Equal((uint)LegacyLightningShieldActivatedFlag, activationUpdate.Flag);
        Assert.Equal(10U, activationUpdate.Time);
        Assert.Equal(expectedActivationShieldLeft, activationUpdate.Damage);
        Assert.Equal(3U, activationUpdate.SpellLevel);

        var drainUpdate = azureShieldEntries[1];
        Assert.Equal(0UL, drainUpdate.Value);
        Assert.Equal((uint)LegacyLightningShieldActivatedFlag, drainUpdate.Flag);
        Assert.InRange(drainUpdate.Time, 0U, 10U);
        Assert.Equal(expectedDrainedShieldLeft, drainUpdate.Damage);
        Assert.Equal(3U, drainUpdate.SpellLevel);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal(
            expectedDrainedShieldLeft.ToString(CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes["LightningShieldLeft"]);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }
}
