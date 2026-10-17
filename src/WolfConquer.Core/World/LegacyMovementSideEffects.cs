using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyMovementSideEffectResult(
    CharacterSnapshot Character,
    bool ClearedAway,
    bool RefreshVisiblePeerSpawn);

public readonly record struct LegacyRideVigorSideEffectResult(
    uint CurrentVigor,
    ushort VigorCost,
    uint UpdatedVigor,
    bool SendServerInfo)
{
    public bool VigorChanged => UpdatedVigor != CurrentVigor;
}

public static class LegacyMovementSideEffects
{
    public const string AwayAttribute = "Away";
    public const ushort MsgMovementSteedVigorCost = 2;

    public static LegacyMovementSideEffectResult ApplyMsgMovement(CharacterSnapshot character)
    {
        return ClearAwayForRegisteredMovement(character);
    }

    public static LegacyMovementSideEffectResult ApplyMsgDataPlayerJump(CharacterSnapshot character)
    {
        return ClearAwayForRegisteredMovement(character);
    }

    public static LegacyRideVigorSideEffectResult ApplyMsgMovementSteedVigor(uint currentVigor)
    {
        return ApplyRideVigorCost(currentVigor, MsgMovementSteedVigorCost);
    }

    public static LegacyRideVigorSideEffectResult ApplyMsgDataPlayerJumpRideVigor(
        uint currentVigor,
        short legacyDistance)
    {
        if (legacyDistance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(legacyDistance),
                legacyDistance,
                "Legacy jump distance cannot be negative.");
        }

        var cost = (ushort)(1.5f * (float)(legacyDistance / 2));
        return ApplyRideVigorCost(currentVigor, cost);
    }

    private static LegacyRideVigorSideEffectResult ApplyRideVigorCost(uint currentVigor, ushort cost)
    {
        return new LegacyRideVigorSideEffectResult(
            currentVigor,
            cost,
            currentVigor >= cost ? currentVigor - cost : 0,
            SendServerInfo: true);
    }

    private static LegacyMovementSideEffectResult ClearAwayForRegisteredMovement(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!IsAway(character.Attributes))
        {
            return new LegacyMovementSideEffectResult(
                character,
                ClearedAway: false,
                RefreshVisiblePeerSpawn: false);
        }

        // MsgMovement.Movement and MsgDataPacket.PlayerJump both clear Away before refreshing visible peers.
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [AwayAttribute] = "0",
        };

        return new LegacyMovementSideEffectResult(
            character with { Attributes = attributes },
            ClearedAway: true,
            RefreshVisiblePeerSpawn: true);
    }

    private static bool IsAway(IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(AwayAttribute, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out var boolValue))
        {
            return boolValue;
        }

        return ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue) &&
            numericValue != 0;
    }
}
