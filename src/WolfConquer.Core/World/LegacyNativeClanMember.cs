namespace WolfConquer.Core.World;

// Complete native Clan.Ranks enum; no outer Clan constructor is fabricated.
internal enum LegacyNativeClanRanks : ushort
{
    Leader = 100,
    Spouse = 11,
    Member = 10
}

// Complete native Clan.Member declaration and implicit initializer-only birth.
internal class LegacyNativeClanMember
{
    public LegacyNativeClanRanks Rank = LegacyNativeClanRanks.Member;

    public uint UID;

    public string Name = "";

    public uint Donation;

    public byte Level;

    public uint Class;

    public bool Online = false;
}
