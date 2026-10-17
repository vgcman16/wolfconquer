using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void ToxicFogPlayerPoisonHelperAppliesPoisonFlagLevelAndExpiry()
    {
        var now = DateTimeOffset.Parse("2026-06-22T12:00:00Z", CultureInfo.InvariantCulture);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target");
        var playerSpell = CreateToxicFogPlayerSpell(level: 3, useSpellSoul: 2);
        var spell = CreateToxicFogRuntimeSpell(duration: 30);

        var result = WorldPacketHandlers.TryApplyLegacyToxicFogPlayerPoison(
            targetSession,
            playerSpell,
            spell,
            worldData: null,
            now);

        Assert.True(result.Applied);
        Assert.Equal(LegacyToxicFogPlayerPoisonAvoidance.None, result.Avoidance);
        Assert.Equal((byte)5, result.PoisonLevel);
        Assert.Equal((DateTimeOffset?)now.AddSeconds(30), result.ExpiresAtUtc);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, WorldPacketHandlers.LegacyToxicFogPoisonedFlag));
        Assert.Equal("5", result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyToxicFogPoisonLevelAttribute]);
        Assert.Equal(
            now.AddSeconds(30).ToString("O", CultureInfo.InvariantCulture),
            result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute]);
        Assert.False(targetSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyToxicFogPoisonLevelAttribute));
    }

    [Fact]
    public void ToxicFogPlayerPoisonHelperSkipsWhenDeterministicToxinEraserIsPresent()
    {
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            attributes: new Dictionary<string, string>
            {
                ["ToxinEraser"] = "true",
            });

        var result = WorldPacketHandlers.TryApplyLegacyToxicFogPlayerPoison(
            targetSession,
            CreateToxicFogPlayerSpell(),
            CreateToxicFogRuntimeSpell(),
            worldData: null,
            DateTimeOffset.Parse("2026-06-22T12:00:00Z", CultureInfo.InvariantCulture));

        Assert.False(result.Applied);
        Assert.Equal(LegacyToxicFogPlayerPoisonAvoidance.ToxinEraser, result.Avoidance);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, WorldPacketHandlers.LegacyToxicFogPoisonedFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyToxicFogPoisonLevelAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute));
    }

    [Theory]
    [InlineData(49, false, 2)]
    [InlineData(50, true, 0)]
    public void ToxicFogPlayerPoisonHelperUsesSturdinessForcedRoll(
        uint forcedRoll,
        bool expectedApplied,
        int expectedAvoidanceValue)
    {
        var expectedAvoidance = (LegacyToxicFogPlayerPoisonAvoidance)expectedAvoidanceValue;
        const uint sturdinessRuneItemId = 300_109;
        var worldData = CreateMagicCatalogWithItems(
            CreateMonsterFamily(),
            [],
            [CreateItemType((int)sturdinessRuneItemId, "Sturdiness(+9)")],
            CreateToxicFogRow());
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 1,
                ItemId = sturdinessRuneItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyToxicFogSturdinessRollAttribute] =
                    forcedRoll.ToString(CultureInfo.InvariantCulture),
            });

        var result = WorldPacketHandlers.TryApplyLegacyToxicFogPlayerPoison(
            targetSession,
            CreateToxicFogPlayerSpell(),
            CreateToxicFogRuntimeSpell(),
            worldData,
            DateTimeOffset.Parse("2026-06-22T12:00:00Z", CultureInfo.InvariantCulture));

        Assert.Equal(expectedApplied, result.Applied);
        Assert.Equal(expectedAvoidance, result.Avoidance);
        Assert.Equal(
            expectedApplied,
            HasLegacyStatusFlag(result.Session.Character.Attributes, WorldPacketHandlers.LegacyToxicFogPoisonedFlag));
    }

    private static PlayerSpellSnapshotEntry CreateToxicFogPlayerSpell(ushort level = 0, byte useSpellSoul = 0)
    {
        return new PlayerSpellSnapshotEntry(
            ToxicFogSpellId,
            Level: level,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: useSpellSoul);
    }

    private static LegacyMagicRuntimeSpell CreateToxicFogRuntimeSpell(uint duration = 30)
    {
        return new LegacyMagicRuntimeSpell(
            ToxicFogSpellId,
            LegacyMagicSort.Toxic,
            "ToxicFog",
            Level: 0,
            UseMana: 7,
            Damage: 100,
            Experience: 0,
            MaxTargets: 0,
            Range: 4,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 300,
            Duration: duration);
    }
}
