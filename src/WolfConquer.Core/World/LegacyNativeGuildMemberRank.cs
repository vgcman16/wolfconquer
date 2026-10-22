namespace WolfConquer.Core.World;

// Complete native Role.Flags.GuildMemberRank; no Flags marker exists on the source enum.
internal enum LegacyNativeGuildMemberRank : ushort
{
    None,
    GuildLeader,
    DeputyLeader,
    Manager,
    Steward,
    Elite,
    Member
}
