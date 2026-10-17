using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFrostArrowsLifecycleTests
{
    private const int FrostArrowsStatusFlag = 0xDB;

    [Fact]
    public void ApplyDeathClearsFrostArrowsFlagAndRemoveOnDeadMetadata()
    {
        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(FrostArrowsStatusFlag),
            [WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute] =
                DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"),
            [WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute] = "true",
        };

        var result = LegacyPlayerDeathState.ApplyDeath(
            attributes,
            DateTimeOffset.UtcNow,
            deadX: 301,
            deadY: 279);

        Assert.False(LegacyPlayerDeathState.HasStatusFlag(result.Attributes, FrostArrowsStatusFlag));
        Assert.False(result.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute));
        Assert.False(result.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute));
    }

    [Fact]
    public void TimedExpiryClearsFrostArrowsFlagAndMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateCharacter(new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(FrostArrowsStatusFlag),
            [WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute] =
                now.AddSeconds(-1).ToString("O"),
            [WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute] = "true",
        });

        var expired = WorldServerFrameHandler.TryExpireLegacyFrostArrowsStatus(
            character,
            now,
            out var updatedCharacter,
            out var removedVisibleFlag);

        Assert.True(expired);
        Assert.True(removedVisibleFlag);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            updatedCharacter.Attributes,
            FrostArrowsStatusFlag));
        Assert.False(updatedCharacter.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute));
        Assert.False(updatedCharacter.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute));
    }

    private static CharacterSnapshot CreateCharacter(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "FrostTarget",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 10,
            FirstClass: 10,
            SecondClass: 10,
            Reborn: 0,
            Level: 130,
            Experience: 0,
            MapId: 1002,
            X: 301,
            Y: 279,
            PreviousMapId: 1002,
            PreviousMapX: 301,
            PreviousMapY: 279,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "test.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static string BuildStatusFlags(int statusFlag)
    {
        var words = new ulong[Math.Max(10, statusFlag / 32 + 1)];
        words[statusFlag / 32] |= 1UL << (statusFlag % 32);
        return string.Join('/', words) + "/";
    }
}
