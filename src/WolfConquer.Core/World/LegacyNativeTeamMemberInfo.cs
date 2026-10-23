namespace WolfConquer.Core.World;

// Entire native TeamMemberInfo.cs row, not a managed membership projection.
internal struct LegacyNativeTeamMemberInfo
{
    public string? Name;
    public uint UID;
    public uint Mesh;
    public ushort MaxHitpoints;
    public ushort MinMHitpoints;
}
