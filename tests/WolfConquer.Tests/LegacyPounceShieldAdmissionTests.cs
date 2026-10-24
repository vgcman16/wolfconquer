using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerPounceShieldAdmissionUsesCurrentOwnerRatherThanOppositeStaleContext(bool currentHasShield)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], CreatePounceRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var stale = CreateSession(strength: 300, stamina: 100, spellIds: [PounceRuntimeSpellId],
            playerItems: CreatePounceEquipment(mainShield: !currentHasShield, alternateShield: false));
        sessions.Set(connection, stale);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, stale.Character.Uid, out var identity));
        // Real same-client Store replacement before ingress, not an accepting callback.
        var expected = stale with
        {
            PlayerItems = CreatePounceEquipment(mainShield: currentHasShield, alternateShield: false),
        };
        sessions.Set(connection, expected);
        var before = System.Text.Json.JsonSerializer.Serialize(expected);
        var context = new WorldPacketHandlerContext(connection, log, stale, WorldData: worldData,
            Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery()).ToFrameBytes()));

        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, expected.Character.Uid, identity, out var actual));
        Assert.Same(expected.PlayerItems, actual.PlayerItems);
        Assert.Equal(expected.Character.MinMana, actual.Character.MinMana);
        Assert.Equal(expected.LegacyRandomSpell, actual.LegacyRandomSpell);
        Assert.Null(actual.MagicCooldowns);
        if (!currentHasShield)
        {
            Assert.Same(expected, actual);
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(actual));
            Assert.Equal("100", actual.Character.Attributes["Stamina"]);
            Assert.False(actual.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
            Assert.Empty(sent);
            Assert.Contains("activeLeftShield=false", log.ToString());
            return;
        }

        Assert.Equal("50", actual.Character.Attributes["Stamina"]);
        Assert.True(actual.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Equal((840L, 590L), (actual.Character.X, actual.Character.Y));
        var animation = Assert.Single(ParseSpellAnimations(sent));
        AssertPounceSourceAnimation(animation, 840, 590, expectedSoul: 0);
        Assert.Empty(animation.Targets);
        AssertNoPounceMovementFrame(sent);
        var stamina = Assert.Single(ParseMsgUpdates(sent));
        Assert.Equal(PlayerUid, stamina.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 50)], stamina.Updates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerPounceAlternateEquipmentUsesMainLeftFallbackOnlyWhenAlternateLeftIsAbsent(bool mainHasShield)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], CreatePounceRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var complete = CreatePounceEquipment(mainShield: mainHasShield, alternateShield: false);
        var selected = complete with
        {
            EquipmentItems = complete.EquipmentItems.Where(item => item.Position != 55).ToArray(),
        };
        Assert.Equal((ushort)5, Assert.Single(LegacyEquipmentSelector.SelectCurrentEquipment(selected, true)).Position);
        var expected = CreateSession(strength: 300, stamina: 100, spellIds: [PounceRuntimeSpellId],
            playerItems: selected) with { AlternateEquipmentActive = true };
        sessions.Set(connection, expected);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, expected.Character.Uid, out var identity));
        var before = System.Text.Json.JsonSerializer.Serialize(expected);
        var context = new WorldPacketHandlerContext(connection, log, expected, WorldData: worldData,
            Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery()).ToFrameBytes()));

        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, expected.Character.Uid, identity, out var actual));
        Assert.Same(selected, actual.PlayerItems);
        Assert.Equal(expected.Character.MinMana, actual.Character.MinMana);
        Assert.Equal(expected.LegacyRandomSpell, actual.LegacyRandomSpell);
        Assert.Null(actual.MagicCooldowns);
        if (!mainHasShield)
        {
            Assert.Same(expected, actual);
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(actual));
            Assert.Equal("100", actual.Character.Attributes["Stamina"]);
            Assert.False(actual.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
            Assert.Empty(sent);
            Assert.Contains("activeLeftShield=false", log.ToString());
            return;
        }

        Assert.Equal("50", actual.Character.Attributes["Stamina"]);
        Assert.True(actual.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Equal((840L, 590L), (actual.Character.X, actual.Character.Y));
        var animation = Assert.Single(ParseSpellAnimations(sent));
        AssertPounceSourceAnimation(animation, 840, 590, expectedSoul: 0);
        Assert.Empty(animation.Targets);
        AssertNoPounceMovementFrame(sent);
        var stamina = Assert.Single(ParseMsgUpdates(sent));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 50)], stamina.Updates);
    }
}
