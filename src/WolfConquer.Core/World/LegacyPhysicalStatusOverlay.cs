namespace WolfConquer.Core.World;

public sealed record LegacyPhysicalStatusOverlay(
    long OwnerUid,
    uint? Breakthrough = null,
    uint? Counteraction = null,
    DateTimeOffset? FrontBreakExpiresAtUtc = null)
{
    // Native Equip.RidingCrop: independent transient query cache, not current item-list authority.
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? RidingCropItemId { get; init; }

    // Independent native Equip query caches. Unknown/fresh owner reads native zero.
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? RightWeaponItemEffect { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? RingItemEffect { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? NecklaceItemEffect { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? MaxMana { get; init; }
    // Native MyJiangHu.statusclient survives the interserver no-rebuild branch.
    [System.Text.Json.Serialization.JsonIgnore]
    public uint? JiangHuMaxMana { get; init; }

    // Native GameClient.PerfectionStatus field; earned by admitted equipment queries, never saved.
    [System.Text.Json.Serialization.JsonIgnore]
    public double? PerfectionToxinEraser { get; init; }

    public const int FrontBreakStatusFlag = 313;

    public static LegacyPhysicalStatusOverlay ForOwner(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.PhysicalStatusOverlay is { } overlay &&
            overlay.OwnerUid == session.Character.Uid
            ? overlay
            : new LegacyPhysicalStatusOverlay(session.Character.Uid);
    }
}
