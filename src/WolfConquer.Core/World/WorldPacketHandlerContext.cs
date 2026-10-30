using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext(
    ConquerTcpConnection Connection,
    TextWriter Log,
    WorldSession? Session = null,
    ArenaStatisticsProvider? ArenaStatistics = null,
    ArenaSessionStateStore? ArenaStates = null,
    Data.WorldDataCatalog? WorldData = null,
    WorldSessionStore? Sessions = null,
    TeamArenaStatisticsProvider? TeamArenaStatistics = null,
    TeamArenaSessionStateStore? TeamArenaStates = null,
    TeamSessionStateStore? TeamStates = null,
    MonsterRuntimeStateStore? MonsterStates = null,
    SobNpcRuntimeStateStore? SobNpcStates = null,
    FloorItemRuntimeStateStore? FloorItems = null,
    Data.LegacyGameMapTerrainQuery? Terrain = null,
    LegacyGuildWarRuntimeStateStore? GuildWarStates = null,
    string? PlayerSpellsRoot = null,
    string? PlayerItemsRoot = null,
    LegacyMailboxStore? Mailboxes = null,
    string? PlayerProficienciesRoot = null,
    LegacyElitePkSessionStateStore? ElitePkStates = null,
    LegacyTeamPkSessionStateStore? TeamPkStates = null,
    LegacyDragonWarRuntimeStateStore? DragonWarStates = null,
    string? PlayerQuestsRoot = null,
    LegacyFreezeWarRuntimeStateStore? FreezeWarStates = null,
    LegacyClientCompatibilityOptions? ClientCompatibility = null,
    LegacyVoteRuntimeStateStore? VoteStates = null,
    TimeProvider? TimeProvider = null,
    IAccountEmailVerifier? AccountEmailVerifier = null,
    ConquerProtocolProfile ProtocolProfile = ConquerProtocolProfile.RootD11_7899,
    LegacyChatItemStore? ChatItems = null,
    Data.LegacyGameMapTerrainQuery? TeleportTerrain = null,
    ILegacySignedMillisecondClock? LegacyClock = null,
    LegacyTournamentWatcherHolders? TournamentWatcherHolders = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyRuneRankingState? RuneRankings { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyTradeStateStore? Trades { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldOperationBarrier? TradeOperations { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyTradeRecoveryState? TradeRecovery { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyJournalInterruption? TradeJournalInterruption { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyJournalInterruption? VendorJournalInterruption { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyMailboxPackageProducer? MailboxPackages { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyLocalServerIdentity? LocalServerIdentity { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyTeleportOwner? TeleportOwner { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyNativeTeamMembershipEntry? NativeTeamMembershipEntry { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyRootFirstFamilyInvocation? RootFirstFamilyInvocation { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyCombatGearReceiveParticipants? CombatGearReceiveParticipants { get; init; }
}
