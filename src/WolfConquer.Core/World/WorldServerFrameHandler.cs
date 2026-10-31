using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const string RuntimeBlackSpotAttribute = "BlackSpot";
    private const string RuntimeBlackSpotExpiresAtUtcAttribute = "BlackSpotExpiresAtUtc";
    private const int LegacyFlashingNameStatusFlag = 0;
    private const int LegacyFlyStatusFlag = 0x1B;
    private const int LegacyGuardReturnMoveMilliseconds = 300;
    private const long LegacyMonsterViewThreshold = 18;
    private const long LegacyAlienWorldMapId = 10_428;

    private static readonly Encoding LegacyCharacterSnapshotEncoding = CreateLegacyCharacterSnapshotEncoding();
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> DefaultPacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly AccountHandoffService? _handoffService;
    private readonly IAccountEmailVerifier? _accountEmailVerifier;
    private readonly ArenaSessionStateStore _arenaStates = new();
    private readonly TeamArenaSessionStateStore _teamArenaStates = new();
    private readonly TeamSessionStateStore _teamStates;
    private readonly LegacyTournamentWatcherHolders _tournamentWatcherHolders;
    private readonly LegacyElitePkSessionStateStore _elitePkStates = new();
    private readonly LegacyTeamPkSessionStateStore _teamPkStates = new();
    private readonly MonsterRuntimeStateStore _monsterStates = new();
    private readonly SobNpcRuntimeStateStore _sobNpcStates = new();
    private readonly FloorItemRuntimeStateStore _floorItems = new();
    private readonly LegacyGuildWarRuntimeStateStore _guildWarStates = new();
    private readonly LegacyDragonWarRuntimeStateStore _dragonWarStates = new();
    private readonly LegacyFreezeWarRuntimeStateStore _freezeWarStates = new();
    private readonly LegacyChatItemStore _chatItems = new();
    private readonly LegacyGuildWarRuntimeScheduler _guildWarRuntime;
    private readonly ArenaStatisticsProvider _arenaStatistics;
    private readonly TeamArenaStatisticsProvider _teamArenaStatistics;
    private readonly CharacterSnapshotLoader? _snapshotLoader;
    private readonly PacketHandlerRegistry<WorldPacketHandlerContext> _packetHandlers;
    private readonly WorldSessionStore _sessions;
    private readonly ConditionalWeakTable<ConquerTcpConnection, PendingLegacyCharacterCreation> _pendingCreations = new();
    private readonly WorldDataCatalog? _worldData;
    private readonly LegacyGameMapTerrainQuery? _terrain;
    private readonly LegacyGameMapTerrainQuery? _teleportTerrain;
    private readonly LegacyMailboxStore? _mailboxes;
    private readonly LegacyVoteRuntimeStateStore? _voteStates;
    private readonly LegacyRuntimeUpdateLoopOptions _runtimeUpdateLoop;
    private readonly LegacyClientCompatibilityOptions _clientCompatibility;
    private readonly LegacyWorldEntryStartupProbeOptions _worldEntryStartupProbe;
    private readonly ConquerProtocolProfile _protocolProfile;
    private readonly UnknownPacketAndFieldLogger? _wireDiagnostics;
    private readonly LegacyGmPolicy? _gmPolicy;
    private readonly SemaphoreSlim _paidNobilityExpiryLock = new(1, 1);
    private readonly TextWriter _log;
    private int _legacyRuntimeGlobalLoopStarted;

    public WorldServerFrameHandler(CharacterSnapshotLoader? snapshotLoader, TextWriter? log = null)
        : this(snapshotLoader, handoffService: null, log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        ConquerProtocolProfile protocolProfile,
        TextWriter? log = null)
        : this(
            snapshotLoader,
            handoffService: null,
            ArenaStatisticsProvider.Empty,
            TeamArenaStatisticsProvider.Empty,
            worldData: null,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log,
            protocolProfile: protocolProfile)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        TextWriter? log = null)
        : this(snapshotLoader, handoffService, ArenaStatisticsProvider.Empty, log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        WorldDataCatalog? worldData,
        TextWriter? log = null)
        : this(snapshotLoader, handoffService, ArenaStatisticsProvider.FromCatalog(worldData), worldData, log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        WorldDataCatalog? worldData,
        LegacyRuntimeUpdateLoopOptions runtimeUpdateLoop,
        TextWriter? log = null,
        LegacyClientCompatibilityOptions? clientCompatibility = null,
        LegacyWorldEntryStartupProbeOptions? worldEntryStartupProbe = null,
        IAccountEmailVerifier? accountEmailVerifier = null,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.RootD11_7899,
        LegacyOwnedAdmissionRuntime? ownedAdmission = null,
        UnknownPacketAndFieldLogger? wireDiagnostics = null,
        LegacyGmPolicy? gmPolicy = null)
        : this(
            snapshotLoader,
            handoffService,
            ArenaStatisticsProvider.FromCatalog(worldData),
            TeamArenaStatisticsProvider.FromCatalog(worldData),
            worldData,
            runtimeUpdateLoop,
            log,
            terrain: null,
            clientCompatibility,
            worldEntryStartupProbe,
            voteStates: null,
            accountEmailVerifier,
            protocolProfile,
            wireDiagnostics)
    {
        if (gmPolicy is not null && (ownedAdmission is null ||
            protocolProfile != ConquerProtocolProfile.VisibleDx8D13 ||
            gmPolicy.ExpectedDomain != ownedAdmission.Ordinary.Domain))
            throw new ArgumentException("gmPolicyActualOwnedDomainAndProfileRequired", nameof(gmPolicy));
        if (ownedAdmission is not null)
        {
            // These are genuine factory-owned participants. Assign before the
            // claim so concurrent shutdown sees a completely configured Frame.
            _ordinaryAdmissionUids = ownedAdmission.Ordinary;
            _tradeRecovery = ownedAdmission.Recovery;
            _ownedAdmissionRuntime = ownedAdmission;
            if (!ownedAdmission.TryClaimFrame(this, snapshotLoader, _mailboxes?.ClaimRoot, out var reason))
                throw new ArgumentException(reason, nameof(ownedAdmission));
        }
        if (gmPolicy is not null &&
            !ownedAdmission!.TryGetOperatorGearMailConfiguration(this, gmPolicy.ExpectedDomain, out _, out _))
            throw new ArgumentException("gmPolicyCurrentFrameOwnerUnavailable", nameof(gmPolicy));
        _gmPolicy = gmPolicy;
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        ArenaStatisticsProvider? arenaStatistics,
        TextWriter? log = null)
        : this(snapshotLoader, handoffService, arenaStatistics, worldData: null, log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        ArenaStatisticsProvider? arenaStatistics,
        TeamArenaStatisticsProvider? teamArenaStatistics,
        TextWriter? log = null)
        : this(
            snapshotLoader,
            handoffService,
            arenaStatistics,
            teamArenaStatistics,
            worldData: null,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        ArenaStatisticsProvider? arenaStatistics,
        WorldDataCatalog? worldData,
        TextWriter? log = null)
        : this(
            snapshotLoader,
            handoffService,
            arenaStatistics,
            TeamArenaStatisticsProvider.FromCatalog(worldData),
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        ArenaStatisticsProvider? arenaStatistics,
        WorldDataCatalog? worldData,
        LegacyRuntimeUpdateLoopOptions? runtimeUpdateLoop,
        TextWriter? log = null)
        : this(
            snapshotLoader,
            handoffService,
            arenaStatistics,
            TeamArenaStatisticsProvider.FromCatalog(worldData),
            worldData,
            runtimeUpdateLoop,
            log)
    {
    }

    public WorldServerFrameHandler(
        CharacterSnapshotLoader? snapshotLoader,
        AccountHandoffService? handoffService,
        ArenaStatisticsProvider? arenaStatistics,
        TeamArenaStatisticsProvider? teamArenaStatistics,
        WorldDataCatalog? worldData,
        LegacyRuntimeUpdateLoopOptions? runtimeUpdateLoop,
        TextWriter? log = null,
        LegacyGameMapTerrainQuery? terrain = null,
        LegacyClientCompatibilityOptions? clientCompatibility = null,
        LegacyWorldEntryStartupProbeOptions? worldEntryStartupProbe = null,
        LegacyVoteRuntimeStateStore? voteStates = null,
        IAccountEmailVerifier? accountEmailVerifier = null,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.RootD11_7899,
        UnknownPacketAndFieldLogger? wireDiagnostics = null)
    {
        if (!Enum.IsDefined(protocolProfile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(protocolProfile),
                protocolProfile,
                "Unknown Conquer protocol profile.");
        }

        _snapshotLoader = snapshotLoader;
        _handoffService = handoffService;
        _accountEmailVerifier = accountEmailVerifier;
        _arenaStatistics = arenaStatistics ?? ArenaStatisticsProvider.Empty;
        _teamArenaStatistics = teamArenaStatistics ?? TeamArenaStatisticsProvider.Empty;
        _packetHandlers = DefaultPacketHandlers;
        _worldData = worldData;
        _runeRankings = new LegacyRuneRankingState(worldData?.RuneRankings);
        _terrain = terrain;
        _teleportTerrain = terrain ??
            (worldData is null || worldData.LegacyGameMapsByMapId.Count == 0
                ? null
                : new LegacyGameMapTerrainQuery(worldData));
        _mailboxes = TryCreateLegacyMailboxStore(_snapshotLoader?.UsersRoot);
        _runtimeUpdateLoop = runtimeUpdateLoop ?? LegacyRuntimeUpdateLoopOptions.Disabled;
        _clientCompatibility = clientCompatibility ?? LegacyClientCompatibilityOptions.DecompiledSourceCompatible;
        _worldEntryStartupProbe = worldEntryStartupProbe ?? LegacyWorldEntryStartupProbeOptions.Default;
        _protocolProfile = protocolProfile;
        _wireDiagnostics = wireDiagnostics;
        _guildWarRuntime = new LegacyGuildWarRuntimeScheduler(_guildWarStates);
        _log = TextWriter.Synchronized(log ?? TextWriter.Null);
        _tradeRecovery = new(_snapshotLoader?.UsersRoot, _snapshotLoader?.PlayerItemsRoot,
            _snapshotLoader?.PlayerQuestsRoot, _mailboxes?.ClaimRoot, _snapshotLoader?.PlayersCombatGearRoot,
            _snapshotLoader?.PlayerSpellsRoot, _snapshotLoader?.PlayerProficienciesRoot);
        _voteStates = voteStates ?? TryCreateLegacyVoteRuntimeStateStore(_snapshotLoader?.UsersRoot);
        if (_voteStates is { LoadResult.Success: false })
        {
            _log.WriteLine(
                "PARITY-GAP legacy vote runtime load failed " +
                $"file={Path.GetFileName(_voteStates.VotesFilePath)} " +
                $"reason={_voteStates.LoadResult.FailureReason ?? "unknown"}");
        }

        _sessions = WorldSessionStore.CreateForFrame(this);
        _tournamentWatcherHolders = new(_sessions);
        _teamStates = new(_sessions, _tournamentWatcherHolders);
    }

    internal void PersistChangedSessionState(
        WorldSessionStore issuingStore,
        ConquerTcpConnection connection,
        WorldSession? previous,
        WorldSession current,
        LegacyBoundHitpointsScalarWrite? completedHitpoints = null)
    {
        if (!ReferenceEquals(_sessions, issuingStore) ||
            !issuingStore.IsFramePersistenceCurrent(this, connection, current))
            throw new InvalidOperationException("Frame persistence requires its current issued Store owner.");
        if (completedHitpoints is null) PersistChangedSessionCharacterScalars(connection, previous, current);
        else if (!completedHitpoints.CoversCommittedPublication(this, issuingStore, connection, previous, current))
            throw new InvalidOperationException("ordinaryHitpointsCommittedScalarCoverageLost");
        PersistChangedSessionPlayerItems(previous, current);
        PersistChangedSessionPlayerSpells(connection, previous, current);
        PersistChangedSessionPlayerProficiencies(previous, current);
        PersistChangedSessionCombatGear(previous, current);
        PersistChangedSessionPlayerQuests(previous, current);
    }

    private void PersistChangedSessionCharacterScalars(
        ConquerTcpConnection connection,
        WorldSession? previous,
        WorldSession current)
    {
        if (previous is null ||
            string.IsNullOrWhiteSpace(current.UsersRoot) ||
            !CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(previous.Character, current.Character))
        {
            return;
        }

        try
        {
            if (CharacterSnapshotScalarPersistence.TryPersist(current.UsersRoot, current.Character, out var path, out var reason))
            {
                return;
            }

            LogCharacterScalarPersistenceGap(current.AccountId, current.Character, path, reason);
        }
        catch (Exception ex)
        {
            LogCharacterScalarPersistenceGap(current.AccountId, current.Character, string.Empty, ex.GetType().Name);
        }
    }

    private void PersistChangedSessionPlayerItems(
        WorldSession? previous,
        WorldSession current)
    {
        if (previous is null ||
            ReferenceEquals(previous.PlayerItems, current.PlayerItems) ||
            PlayerItemSnapshotsEqual(previous.PlayerItems, current.PlayerItems))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayerItemsRoot))
        {
            LogPlayerItemPersistenceGap(current, "playerItemsRootMissing", string.Empty);
            return;
        }

        try
        {
            var loader = new PlayerItemSnapshotLoader(_snapshotLoader.PlayerItemsRoot);
            loader.SaveByCharacterUid(current.Character.Uid, current.PlayerItems);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPlayerItemPersistenceGap(current, ex.GetType().Name, _snapshotLoader.PlayerItemsRoot);
        }
    }

    private static bool PlayerItemSnapshotsEqual(PlayerItemSnapshot previous, PlayerItemSnapshot current)
    {
        return PlayerItemSnapshotLoader.ToLegacyBytes(previous).AsSpan()
            .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(current));
    }

    private void PersistChangedSessionPlayerSpells(
        ConquerTcpConnection connection,
        WorldSession? previous,
        WorldSession current)
    {
        if (TryPersistChangedOrdinarySpells(connection, previous, current)) return;
        if (previous is null ||
            ReferenceEquals(previous.PlayerSpells, current.PlayerSpells) ||
            PlayerSpellSnapshotsEqual(previous.PlayerSpells, current.PlayerSpells))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayerSpellsRoot))
        {
            LogPlayerSpellPersistenceGap(current, "playerSpellsRootMissing", string.Empty);
            return;
        }

        try
        {
            var loader = new PlayerSpellSnapshotLoader(_snapshotLoader.PlayerSpellsRoot);
            loader.SaveByCharacterUid(current.Character.Uid, current.PlayerSpells);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPlayerSpellPersistenceGap(current, ex.GetType().Name, _snapshotLoader.PlayerSpellsRoot);
        }
    }

    private static bool PlayerSpellSnapshotsEqual(PlayerSpellSnapshot previous, PlayerSpellSnapshot current)
    {
        return PlayerSpellSnapshotLoader.ToLegacyBytes(previous).AsSpan()
            .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(current));
    }

    private void PersistChangedSessionCombatGear(
        WorldSession? previous,
        WorldSession current)
    {
        var previousCombatGear = previous?.CombatGear ?? CombatGearSnapshot.Empty;
        var currentCombatGear = current.CombatGear ?? CombatGearSnapshot.Empty;
        if (previous is null ||
            ReferenceEquals(previous.CombatGear, current.CombatGear) ||
            CombatGearSnapshotsEqual(previousCombatGear, currentCombatGear))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayersCombatGearRoot))
        {
            LogCombatGearPersistenceGap(current, currentCombatGear, "playersCombatGearRootMissing", string.Empty);
            return;
        }

        try
        {
            var loader = new CombatGearSnapshotLoader(_snapshotLoader.PlayersCombatGearRoot);
            loader.SaveByCharacterUid(current.Character.Uid, currentCombatGear);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogCombatGearPersistenceGap(current, currentCombatGear, ex.GetType().Name, _snapshotLoader.PlayersCombatGearRoot);
        }
    }

    private static bool CombatGearSnapshotsEqual(CombatGearSnapshot previous, CombatGearSnapshot current)
    {
        return string.Equals(
            CombatGearSnapshotLoader.ToLegacyText(previous),
            CombatGearSnapshotLoader.ToLegacyText(current),
            StringComparison.Ordinal);
    }

    private void LogCharacterScalarPersistenceGap(long accountId, CharacterSnapshot snapshot, string path, string reason)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "character-scalar-persistence",
            $"failed to persist legacy character scalar snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("map", snapshot.MapId),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-character-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("uid", snapshot.Uid),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-character-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG character scalar persistence failed " +
            $"accountId={accountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }

    private void LogPlayerItemPersistenceGap(WorldSession session, string reason, string path)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "player-item-persistence",
            $"failed to persist legacy player item snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("accountId", session.AccountId),
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("inventoryItems", session.PlayerItems.InventoryItems.Count),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-items-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("uid", session.Character.Uid),
                LegacyParityGapDiagnostics.Fact("warehouseItems", session.PlayerItems.WarehouseItems.Count),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-items-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG player item persistence failed " +
            $"accountId={session.AccountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={session.Character.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }

    private void LogPlayerSpellPersistenceGap(WorldSession session, string reason, string path)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "player-spell-persistence",
            $"failed to persist legacy player spell snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("accountId", session.AccountId),
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-spells-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("spellCount", session.PlayerSpells.Spells.Count),
                LegacyParityGapDiagnostics.Fact("uid", session.Character.Uid),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-spells-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG player spell persistence failed " +
            $"accountId={session.AccountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={session.Character.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }

    private void LogCombatGearPersistenceGap(
        WorldSession session,
        CombatGearSnapshot combatGear,
        string reason,
        string path)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "combat-gear-persistence",
            $"failed to persist legacy combat gear snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("accountId", session.AccountId),
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-combat-gear-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("pirateExp", combatGear.PirateExp),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("uid", session.Character.Uid),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-combat-gear-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG combat gear persistence failed " +
            $"accountId={session.AccountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={session.Character.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }

    private async ValueTask HandleWorldEnterRequestAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!InternalWorldEnterRequestPacket.TryParse(frame, out var request))
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.InvalidPacket,
                    accountId: 0,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("WorldServer rejected malformed internal world-enter request.").ConfigureAwait(false);
            return;
        }

        var redeemed = _handoffService?.Redeem(request.Ticket) ??
            new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        if (!redeemed.IsSuccess)
        {
            var accountId = redeemed.Ticket?.AccountId ?? 0;
            await SendResponseAsync(
                    connection,
                    MapHandoffStatus(redeemed.Status),
                    accountId,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    $"WorldServer handoff {redeemed.Status} for account {accountId}.")
                .ConfigureAwait(false);
            return;
        }

        var ticket = redeemed.Ticket!;
        var reply = await SendSnapshotForAccountAsync(connection, ticket.AccountId, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (reply.Snapshot is { } snapshot)
        {
            await StartWorldSessionAsync(connection, ticket.AccountId, ticket.Username, snapshot, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (reply.Status == WorldSnapshotWireStatus.MissingSnapshot && HasOrdinaryUidNamespace() &&
            !TryRegisterAuthenticatedCreation(connection, ticket, out var creationReason))
            await _log.WriteLineAsync("WorldServer authenticated creation permit refused: " + creationReason + ".")
                .ConfigureAwait(false);
    }

    private async ValueTask HandleLegacyWorldLoginAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!LegacyMsgLoginGamePacket.TryParse(frame, out var request))
        {
            await _log.WriteLineAsync("WorldServer rejected malformed legacy MsgLoginGame request.")
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacyWorldLoginAdmissionAsync(connection, request.Key, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWorldLoginDelayedStartupReviewsAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyWorldLoginPreAnswerUpdates(snapshot))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        var now = DateTime.Now;
        foreach (var quest in LegacyQuestListPacket.CreateDailyResetInitialReviews(snapshot, now))
        {
            await connection.SendAsync(quest.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyQuestListPacket.ShouldSendDailyReset(snapshot, now) &&
            LegacyMsgUpdatePacket.TryCreateLegacyDailyResetEnlightPointsUpdate(snapshot, out var enlightenUpdate))
        {
            await connection.SendAsync(enlightenUpdate.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var quest in LegacyQuestListPacket.CreateDailyResetFollowupReviews(snapshot, now))
        {
            await connection.SendAsync(quest.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask HandleLegacyNewRoleAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null ||
            !_pendingCreations.TryGetValue(connection, out var pending))
        {
            await _log.WriteLineAsync(
                    $"WorldServer ignored legacy NewRole without pending creation {FormatFrameDiagnostic(frame)}.")
                .ConfigureAwait(false);
            return;
        }

        var parsed = LegacyNewRolePacket.TryParse(frame, out var request);
        if (HasOrdinaryUidNamespace())
        {
            if (pending.Authenticated && _ownedAdmissionRuntime is { } runtime &&
                runtime.HasAuthenticatedCreationPermit(this, connection))
            {
                await HandleAuthenticatedOrdinaryCreationAsync(connection, frame, pending, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            await _log.WriteLineAsync($"WorldServer refused legacy NewRole uid={pending.CharacterUid}; ordinaryAdmissionUidCreatorRequired.")
                .ConfigureAwait(false);
            return; // Preserve the pending request; never use the old derived-UID writer in this domain.
        }
        if (!parsed ||
            !TryNormalizeLegacyNewRoleName(request.Name, out var name) ||
            !IsLegacyNewRoleBody(request.Body) ||
            !TryMapLegacyNewRoleClass(request.ClassSelection, out var classCode))
        {
            await _log.WriteLineAsync(
                    "WorldServer rejected legacy NewRole " +
                    $"uid={pending.CharacterUid} body={(parsed ? request.Body : 0)} " +
                    $"classSelection={(parsed ? request.ClassSelection : 0)}.")
                .ConfigureAwait(false);
            return;
        }

        CharacterSnapshot snapshot;
        try
        {
            snapshot = _snapshotLoader.CreateLegacyFirstCharacterSnapshot(
                pending.CharacterUid,
                name,
                request.Body,
                classCode);
            var playerItems = _snapshotLoader.LoadPlayerItemsByCharacterUid(snapshot.Uid);
            var starterState = await EnsureLegacyFirstCharacterStarterStateAsync(
                    snapshot,
                    playerItems,
                    cancellationToken)
                .ConfigureAwait(false);
            snapshot = starterState.Snapshot;
            var playerSpells = _snapshotLoader.LoadPlayerSpellsByCharacterUid(snapshot.Uid);
            await EnsureLegacyFirstCharacterStarterSpellsAsync(
                    snapshot,
                    starterState.PlayerItems,
                    playerSpells,
                    cancellationToken)
                .ConfigureAwait(false);
            await PersistLegacyFirstCharacterEmptyProficienciesAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);
            await PersistLegacyFirstCharacterCreationSidecarsAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            await _log.WriteLineAsync(
                    $"WorldServer legacy NewRole snapshot create error uid={pending.CharacterUid}: {ex.Message}")
                .ConfigureAwait(false);
            return;
        }

        _pendingCreations.Remove(connection);
        await connection.SendAsync(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await StartWorldSessionAsync(connection, pending.CharacterUid, snapshot.Name, snapshot, cancellationToken)
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                $"WorldServer legacy NewRole Success uid={snapshot.Uid} name={snapshot.Name} class={snapshot.Class} body={snapshot.Body}.")
            .ConfigureAwait(false);
    }

    private async ValueTask<bool> TrySendLegacyGuardMonsterProtectionAttackAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance guard,
        Monster guardFamily,
        LegacyMonsterInstance target,
        Monster targetFamily,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var spellId = ReadMonsterAttributeUInt32(
            guardFamily.Attributes,
            "magic_type",
            "MagicType",
            "spell_id",
            "SpellId",
            "spellid");
        if (spellId == 0 || spellId > ushort.MaxValue)
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy guard monster-protection attack " +
                    $"guardUid={guard.Uid} targetUid={target.Uid}; spellId={spellId} is not a valid legacy UInt16.")
                .ConfigureAwait(false);
            return false;
        }

        var damage = CalculateLegacyGuardMonsterProtectionDamage(guardFamily, targetFamily);
        var result = _monsterStates.ApplyDamage(target, damage, now);
        if (result.AlreadyDead)
        {
            return false;
        }

        var packet = LegacySpellAnimationPacket.ForMonsterMagic(
            guard.Uid,
            target.Uid,
            target.X,
            target.Y,
            (ushort)spellId,
            damage);
        var frames = packet.ToFrames();
        byte[]? deathFrame = result.Killed
            ? new MsgAttackPacket(new MsgAttackQuery(
                Uid: guard.Uid,
                OpponentUid: target.Uid,
                X: target.X,
                Y: target.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()
            : null;
        var sent = false;
        foreach (var active in activeSessions)
        {
            if (!CanSeeMonster(active.Session.Character, guard))
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            if (deathFrame is not null)
            {
                await active.Connection.SendAsync(deathFrame, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var updatedTarget = target with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedTarget))
                {
                    await active.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            sent = true;
        }

        return sent;
    }

    private static uint CalculateLegacyGuardMonsterProtectionDamage(
        Monster guardFamily,
        Monster targetFamily)
    {
        var maxAttack = ReadMonsterAttributeUInt32(guardFamily.Attributes, "attack_max", "MaxAttack");
        var defense = ReadMonsterAttributeUInt32(
            targetFamily.Attributes,
            "defense",
            "defence",
            "Defense",
            "Defence");
        return maxAttack > defense ? maxAttack - defense : 1U;
    }

    private async ValueTask<WorldSession?> ApplyLegacyReviverReviveAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Native Player.Revive retains the original physical receiver, not a later client.Player.
        var nativePlayerReceiver = session.NativePlayer;
        var currentHitpoints = session.Character.MinHitPoints <= 0
            ? 1U
            : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyStartupMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            _worldData);
        var revive = LegacyPlayerDeathState.TryRevive(
            session.Character,
            now,
            new LegacyPlayerReviveOptions
            {
                IgnoreReviveDelay = true,
                ReviveDelay = TimeSpan.Zero,
                MaximumHitpoints = maxHitpoints,
                MaximumMana = session.Character.MaxMana,
            });
        if (!revive.Allowed)
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy Reviver revive " +
                    $"uid={session.Character.Uid} hitpoints={session.Character.MinHitPoints} " +
                    $"reviveAllowedAt={revive.State.ReviveAllowedAt}.")
                .ConfigureAwait(false);
            return null;
        }

        var updatedSession = session with { Character = revive.Character };
        _sessions.Set(connection, updatedSession);
        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            uid,
            maxHitpoints,
            maxHitpoints))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                    uid,
                    LegacyPlayerDeathState.DefaultReviveStamina).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyNativePlayerFlag6Lifecycle.CaptureVector(nativePlayerReceiver,
                        LegacyPlayerDeathState.CreateStatusFlagVector(
                            updatedSession.Character.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(
                    uid,
                    BuildLegacyCharacterMesh(updatedSession.Character)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMapStatusPacket.FromMap(updatedSession.Character.MapId, _worldData?.MapsById).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private async ValueTask<int> SendLegacyReviverSpellAnimationAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        WorldSession revivedSession,
        LegacyMonsterInstance reviver,
        CancellationToken cancellationToken)
    {
        var packet = new LegacySpellAnimationPacket(
            reviver.Uid,
            AttackedUid: 0,
            reviver.X,
            reviver.Y,
            SpellId: 1100,
            SpellLevel: 0,
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(
                CheckedUInt32(revivedSession.Character.Uid, nameof(revivedSession.Character.Uid)),
                Damage: 0,
                Hit: true,
                MsgAttackEffect.None)]);
        var frames = packet.ToFrames();
        var sentCount = 0;
        foreach (var active in activeSessions)
        {
            if (!CanSeeMonster(active.Session.Character, reviver))
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            sentCount++;
        }

        return sentCount;
    }

    private Dictionary<(ushort X, ushort Y), int> CreateVisibleMonsterTileCounts(
        int mapId,
        IReadOnlyList<MonsterSpawn> spawns,
        DateTimeOffset now)
    {
        var tiles = new Dictionary<(ushort X, ushort Y), int>();
        if (_worldData is not { } worldData)
        {
            return tiles;
        }

        foreach (var entry in EnumerateLegacyStaticMonsterTileClaims(worldData, spawns))
        {
            if (_monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visible))
            {
                AddLegacyMonsterTileOccupancy(tiles, visible.X, visible.Y);
            }
        }

        return tiles;
    }

    private static IEnumerable<LegacyStaticMonsterEntry> EnumerateLegacyStaticMonsterTileClaims(
        WorldDataCatalog worldData,
        IReadOnlyList<MonsterSpawn> spawns)
    {
        var materializedSpawns = new List<MonsterSpawn>();
        foreach (var spawn in spawns)
        {
            if (!IsLegacySingleTileStaticTileClaim(spawn))
            {
                materializedSpawns.Add(spawn);
                continue;
            }

            if (!worldData.MonstersById.TryGetValue(spawn.MonsterTypeId, out var monsterFamily))
            {
                continue;
            }

            var uidBase = worldData.GetLegacyMonsterRuntimeUidBase(spawn);
            foreach (var instance in LegacyMonsterInstance.FromGenerator(spawn, monsterFamily, uidBase))
            {
                yield return new LegacyStaticMonsterEntry(spawn, monsterFamily, instance);
            }
        }

        if (materializedSpawns.Count == 0)
        {
            yield break;
        }

        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, materializedSpawns))
        {
            yield return entry;
        }
    }

    private static bool IsLegacySingleTileStaticTileClaim(MonsterSpawn spawn)
    {
        return LegacyMonsterSpawnCountRules.IsLegacyLoadMapMonstersSpawn(spawn) ||
            spawn.BoundWidth == 1 &&
            spawn.BoundHeight == 1 &&
            spawn.MaxNpc == 1 &&
            spawn.MaxPerGenerator == 1;
    }

    private static void AddLegacyMonsterTileOccupancy(
        Dictionary<(ushort X, ushort Y), int> occupiedMonsterTiles,
        ushort x,
        ushort y)
    {
        var key = (x, y);
        occupiedMonsterTiles.TryGetValue(key, out var count);
        occupiedMonsterTiles[key] = count + 1;
    }

    private static void RemoveLegacyMonsterTileOccupancy(
        Dictionary<(ushort X, ushort Y), int> occupiedMonsterTiles,
        ushort x,
        ushort y)
    {
        var key = (x, y);
        if (!occupiedMonsterTiles.TryGetValue(key, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            occupiedMonsterTiles.Remove(key);
            return;
        }

        occupiedMonsterTiles[key] = count - 1;
    }

    private Func<ushort, ushort, bool>? CreateLegacyMonsterMovementValidator(
        LegacyMonsterInstance monster,
        IReadOnlyDictionary<(ushort X, ushort Y), int> occupiedMonsterTiles)
    {
        var hasTerrain = _terrain is { } terrain &&
            terrain.TryGetTerrain(monster.MapId, out _);
        if (!hasTerrain && occupiedMonsterTiles.Count == 0)
        {
            return null;
        }

        var mapId = monster.MapId;
        return (x, y) =>
        {
            if (hasTerrain && !_terrain!.IsWalkable(mapId, x, y))
            {
                return false;
            }

            return !occupiedMonsterTiles.ContainsKey((x, y)) ||
                x == monster.X && y == monster.Y;
        };
    }

    private static TimeSpan GetLegacyMonsterAttackInterval(Monster monsterFamily)
    {
        var attackSpeed = ReadMonsterAttributeUInt32(monsterFamily.Attributes, "attack_speed", "AttackSpeed");
        return TimeSpan.FromMilliseconds(attackSpeed == 0 ? 1000 : attackSpeed);
    }

    private static bool IsLegacyGenericAggroExcludedMonsterFamily(Monster monsterFamily)
    {
        return LegacyMonsterFamilyRuntimeRules.IsExcludedFromGenericAggro(monsterFamily) ||
            IsLegacyGuardMonsterFamily(monsterFamily) ||
            IsLegacyReviverMonsterFamily(monsterFamily) ||
            monsterFamily.Name.Contains("Lottus", StringComparison.Ordinal);
    }

    private static bool IsLegacyReviverMonsterFamily(Monster monsterFamily)
    {
        return monsterFamily.Name.Contains("Reviver", StringComparison.Ordinal);
    }

    private static bool IsLegacyGuardMonsterFamily(Monster monsterFamily)
    {
        return (monsterFamily.Name.Contains("Guard", StringComparison.Ordinal) &&
                !monsterFamily.Name.Contains("Chaos", StringComparison.Ordinal)) ||
            monsterFamily.Name.Contains("CitySoldier", StringComparison.Ordinal);
    }

    private static long ResolveLegacyMonsterTargetScanRange(
        LegacyMonsterInstance monster,
        uint attackRange,
        uint scanRange)
    {
        var effectiveScanRange = monster.Boss && monster.Hitpoints >= 2_000_000U
            ? Math.Max((long)scanRange, LegacyMonsterViewThreshold)
            : scanRange;

        return Math.Max(effectiveScanRange, attackRange);
    }

    private static TimeSpan GetLegacyMonsterMoveInterval(Monster monsterFamily)
    {
        var moveSpeed = ReadMonsterAttributeUInt32(monsterFamily.Attributes, "move_speed", "MoveSpeed");
        return TimeSpan.FromMilliseconds(moveSpeed == 0 ? 500 : moveSpeed);
    }

    private static bool IsLegacyCharacterDead(CharacterSnapshot character)
    {
        return character.MinHitPoints <= 0 ||
            LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) ||
            LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyPlayerDeathState.GhostStatusFlag);
    }

    private static async ValueTask SendMonsterMovementToVisibleAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance previousMonster,
        LegacyMonsterInstance monster,
        uint direction,
        DateTimeOffset now,
        MonsterRuntimeStateStore monsterStates,
        LegacyClientCompatibilityOptions? clientCompatibility,
        CancellationToken cancellationToken)
    {
        var frame = new MsgMovementPacket
        {
            Direction = direction,
            Uid = monster.Uid,
            Running = MsgMovementMode.Run,
            Timestamp = unchecked((uint)now.ToUnixTimeMilliseconds()),
            MapId = checked((uint)monster.MapId),
        }.ToFrameBytes();

        foreach (var active in activeSessions)
        {
            if (!CanSeeMonster(active.Session.Character, monster))
            {
                continue;
            }

            if (!CanSeeMonster(active.Session.Character, previousMonster))
            {
                await SendMonsterSpawnFramesToConnectionAsync(
                        active.Connection,
                        monster,
                        monsterStates,
                        now,
                        clientCompatibility,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendMonsterSpawnFramesToConnectionAsync(
        ConquerTcpConnection connection,
        LegacyMonsterInstance monster,
        MonsterRuntimeStateStore monsterStates,
        DateTimeOffset now,
        LegacyClientCompatibilityOptions? clientCompatibility,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(
                monster.ToSpawnPacket(clientCompatibility: clientCompatibility).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monster))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        if (monsterStates.IsBlackSpotted(monster, now))
        {
            await connection.SendAsync(
                    LegacyBlackspotPacket.CreateActive(monster.Uid).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<byte[]> CreateLegacyMonsterRespawnFrameBytes(
        LegacyMonsterInstance monster,
        LegacyClientCompatibilityOptions? clientCompatibility)
    {
        var frames = new List<byte[]>(capacity: 4)
        {
            new MsgActionPacket
            {
                ObjId = monster.Uid,
                Type = MsgActionType.RemoveEntity,
            }.ToFrameBytes(),
            monster.ToSpawnPacket(clientCompatibility: clientCompatibility).ToFrameBytes(),
            new MsgActionPacket
            {
                ObjId = monster.Uid,
                Type = MsgActionType.MonsterRespawnEffect,
            }.ToFrameBytes(),
        };

        foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monster))
        {
            frames.Add(update.ToFrameBytes());
        }

        return frames;
    }

    private static bool CanSeeMonster(CharacterSnapshot character, LegacyMonsterInstance monster)
    {
        return character.MapId == monster.MapId &&
            ReadCharacterDynamicId(character) == monster.DynamicId &&
            LegacyGetDistance(character.X, character.Y, monster.X, monster.Y) <= LegacyMonsterViewThreshold;
    }

    private static bool CanAnyActiveSessionSeeMonster(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance monster)
    {
        foreach (var active in activeSessions)
        {
            if (CanSeeMonster(active.Session.Character, monster))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanAnyActiveSessionSeeBothMonsters(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyMonsterInstance first,
        LegacyMonsterInstance second)
    {
        foreach (var active in activeSessions)
        {
            var character = active.Session.Character;
            if (CanSeeMonster(character, first) && CanSeeMonster(character, second))
            {
                return true;
            }
        }

        return false;
    }

    private static uint ReadMonsterAttributeUInt32(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static Encoding CreateLegacyCharacterSnapshotEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy world startup value must fit UInt32.");
        }

        return (uint)value;
    }

    private static int CheckedInt32(long value, string name)
    {
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy world startup value must fit Int32.");
        }

        return (int)value;
    }

    private static ulong CheckedUInt64(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy world startup value must fit UInt64.");
        }

        return (ulong)value;
    }

    private static ushort CheckedUInt16(long value, string name)
    {
        if (value < 0 || value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy world startup value must fit UInt16.");
        }

        return (ushort)value;
    }

    private async ValueTask<LegacyWorldLoginSnapshotLoadResult> LoadSnapshotForLegacyWorldLoginAsync(long characterUid)
    {
        if (_snapshotLoader is null)
        {
            await _log.WriteLineAsync(
                    $"WorldServer legacy MsgLoginGame key {characterUid} received but no Users root is configured.")
                .ConfigureAwait(false);
            return new LegacyWorldLoginSnapshotLoadResult(
                null,
                LegacyWorldLoginSnapshotLoadStatus.Unavailable);
        }

        var loadResult = _snapshotLoader.TryLoadByAccountIdForAdmission(characterUid);
        if (loadResult.Status is CharacterSnapshotLoadStatus.SnapshotReadFailed or
            CharacterSnapshotLoadStatus.SnapshotReadDenied or
            CharacterSnapshotLoadStatus.SnapshotParseFailed or
            CharacterSnapshotLoadStatus.UidMismatch)
        {
            await _log.WriteLineAsync(
                    FormatSnapshotLoadFailure("WorldServer legacy MsgLoginGame snapshot error", loadResult))
                .ConfigureAwait(false);
            return new LegacyWorldLoginSnapshotLoadResult(
                null,
                LegacyWorldLoginSnapshotLoadStatus.Error);
        }

        if (loadResult.Status == CharacterSnapshotLoadStatus.MissingSnapshot)
        {
            await _log.WriteLineAsync(
                    FormatSnapshotLoadFailure("WorldServer legacy MsgLoginGame snapshot missing", loadResult))
                .ConfigureAwait(false);
            return new LegacyWorldLoginSnapshotLoadResult(
                null,
                LegacyWorldLoginSnapshotLoadStatus.MissingSnapshot);
        }

        var snapshot = loadResult.Snapshot;
        if (snapshot is null)
        {
            throw new InvalidOperationException("Successful character snapshot load returned no snapshot.");
        }

        return new LegacyWorldLoginSnapshotLoadResult(
            snapshot,
            LegacyWorldLoginSnapshotLoadStatus.Success);
    }

    private static ValueTask SendResponseAsync(
        ConquerTcpConnection connection,
        WorldSnapshotWireStatus status,
        long accountId,
        CharacterSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        return connection.SendAsync(WorldSnapshotResponsePacket.Build(status, accountId, snapshot), cancellationToken);
    }

    private static string FormatSnapshotLoadFailure(string prefix, CharacterSnapshotLoadResult result)
    {
        var file = string.IsNullOrWhiteSpace(result.Path) ? "unknown" : Path.GetFileName(result.Path);
        return prefix +
            $" accountId={result.AccountId.ToString(CultureInfo.InvariantCulture)}" +
            $" file={file}" +
            $" reason={result.Reason}" +
            $" status={result.Status}" +
            $" detail={result.Detail}";
    }

    private static WorldSnapshotWireStatus MapHandoffStatus(AccountHandoffRedeemStatus status)
    {
        return status switch
        {
            AccountHandoffRedeemStatus.UnknownTicket => WorldSnapshotWireStatus.UnknownTicket,
            AccountHandoffRedeemStatus.ExpiredTicket => WorldSnapshotWireStatus.ExpiredTicket,
            AccountHandoffRedeemStatus.AlreadyRedeemed => WorldSnapshotWireStatus.AlreadyRedeemedTicket,
            AccountHandoffRedeemStatus.Success => WorldSnapshotWireStatus.Success,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown handoff status."),
        };
    }

    private static LegacyMailboxStore? TryCreateLegacyMailboxStore(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var parent = Directory.GetParent(Path.GetFullPath(usersRoot));
        return parent is null ? null : new LegacyMailboxStore(parent.FullName);
    }

    private static LegacyVoteRuntimeStateStore? TryCreateLegacyVoteRuntimeStateStore(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var parent = Directory.GetParent(Path.GetFullPath(usersRoot));
        return parent is null
            ? null
            : LegacyVoteRuntimeStateStore.Load(Path.Combine(parent.FullName, "Votes.txt"));
    }

    private static bool TryNormalizeLegacyNewRoleName(string rawName, out string name)
    {
        name = rawName.Trim();
        if (name.Length == 0 ||
            Encoding.UTF8.GetByteCount(name) >= LegacyNewRolePacket.NameLength)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (char.IsControl(c) || c is '[' or ']' or '=')
            {
                return false;
            }
        }

        return LegacyCharacterSnapshotEncoding.GetString(LegacyCharacterSnapshotEncoding.GetBytes(name)) == name;
    }

    private static bool IsLegacyNewRoleBody(ushort body)
    {
        return body is 1008 or 2007;
    }

    private static bool TryMapLegacyNewRoleClass(ushort selection, out long classCode)
    {
        classCode = selection switch
        {
            0 or 1 => 10000,
            2 or 3 => 1000,
            4 or 5 => 4000,
            6 or 7 => 2000,
            8 or 9 => 5000,
            10 or 11 => 6000,
            12 or 13 => 7000,
            14 or 15 => 8000,
            >= 16 and <= 19 => 16000,
            20 or 21 => 9000,
            _ => 0,
        };
        return classCode != 0;
    }

    private sealed record PendingLegacyCharacterCreation(long CharacterUid, bool Authenticated = false);

    private readonly record struct LegacyWorldLoginSnapshotLoadResult(
        CharacterSnapshot? Snapshot,
        LegacyWorldLoginSnapshotLoadStatus Status);

    private enum LegacyWorldLoginSnapshotLoadStatus
    {
        Success,
        MissingSnapshot,
        Unavailable,
        Error,
    }
}
