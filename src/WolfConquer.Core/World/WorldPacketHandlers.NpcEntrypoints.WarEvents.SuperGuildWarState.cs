using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed class LegacySuperGuildWarStateStore
{
    private const ulong SourceGuildLeaderRank = 1;
    private const ulong SourceDeputyLeaderRank = 2;
    private const ulong SnapshotGuildLeaderRank = 100;
    private const ulong SnapshotDeputyLeaderRank = 90;

    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacySuperGuildWarGateRuntimeState> _gates = [];
    private readonly Dictionary<ulong, LegacySuperGuildWarScoreEntry> _scores = [];
    private readonly HashSet<uint> _claimedLeaderRewards = [];
    private readonly HashSet<uint> _claimedDeputyRewards = [];
    private string? _rewardSeedKey;
    private int _leaderRewardsRemaining;
    private int _deputyRewardsRemaining;

    public static bool TryGetWinnerGuildId(GuildWarData? superGuildWarInfo, out ulong winnerGuildId)
    {
        winnerGuildId = 0;
        if (superGuildWarInfo?.Info?.Id is not > 0)
        {
            return false;
        }

        winnerGuildId = (ulong)superGuildWarInfo.Info.Id.Value;
        return true;
    }

    public static bool IsWinnerGuildMember(ulong guildId, ulong winnerGuildId)
    {
        return guildId != 0 && guildId == winnerGuildId;
    }

    public static bool IsGateOfficerRank(ulong guildRank)
    {
        return guildRank is SourceGuildLeaderRank or SourceDeputyLeaderRank or
            SnapshotGuildLeaderRank or SnapshotDeputyLeaderRank;
    }

    public LegacySuperGuildWarGateMutation ApplyGate(
        uint npcId,
        int closedMesh,
        int openMesh,
        uint maxHitpoints,
        bool open)
    {
        var boundedMaxHitpoints = Math.Max(maxHitpoints, 1);
        lock (_sync)
        {
            if (!_gates.TryGetValue(npcId, out var state))
            {
                state = new LegacySuperGuildWarGateRuntimeState(
                    IsOpen: false,
                    Mesh: closedMesh,
                    Hitpoints: boundedMaxHitpoints,
                    MaxHitpoints: boundedMaxHitpoints);
                _gates[npcId] = state;
            }
            else
            {
                state.MaxHitpoints = Math.Max(state.MaxHitpoints, boundedMaxHitpoints);
            }

            var previous = state.ToSnapshot(npcId);
            if (open)
            {
                state.IsOpen = true;
                state.Mesh = openMesh;
            }
            else
            {
                state.IsOpen = false;
                state.Mesh = closedMesh;
                if (state.Hitpoints == 0)
                {
                    state.Hitpoints = state.MaxHitpoints;
                }
            }

            var current = state.ToSnapshot(npcId);
            return new LegacySuperGuildWarGateMutation(previous, current);
        }
    }

    public LegacySuperGuildWarRewardClaim TryClaimReward(
        uint characterUid,
        ulong guildId,
        ulong guildRank,
        GuildWarData? superGuildWarInfo)
    {
        if (!TryGetWinnerGuildId(superGuildWarInfo, out var winnerGuildId))
        {
            return new LegacySuperGuildWarRewardClaim(
                LegacySuperGuildWarRewardClaimStatus.MissingWinner,
                LegacySuperGuildWarRewardKind.None,
                RemainingAfterClaim: 0);
        }

        if (!IsWinnerGuildMember(guildId, winnerGuildId))
        {
            return new LegacySuperGuildWarRewardClaim(
                LegacySuperGuildWarRewardClaimStatus.NotWinnerGuild,
                LegacySuperGuildWarRewardKind.None,
                RemainingAfterClaim: 0);
        }

        lock (_sync)
        {
            SeedRewardsIfNeeded(superGuildWarInfo);

            if (guildRank is SourceGuildLeaderRank or SnapshotGuildLeaderRank)
            {
                return TryClaimReward(
                    characterUid,
                    LegacySuperGuildWarRewardKind.Leader,
                    _claimedLeaderRewards,
                    ref _leaderRewardsRemaining);
            }

            if (guildRank is SourceDeputyLeaderRank or SnapshotDeputyLeaderRank)
            {
                return TryClaimReward(
                    characterUid,
                    LegacySuperGuildWarRewardKind.DeputyLeader,
                    _claimedDeputyRewards,
                    ref _deputyRewardsRemaining);
            }
        }

        return new LegacySuperGuildWarRewardClaim(
            LegacySuperGuildWarRewardClaimStatus.UnsupportedRank,
            LegacySuperGuildWarRewardKind.None,
            RemainingAfterClaim: 0);
    }

    public LegacySuperGuildWarScoreEntry UpdateScore(
        ulong guildId,
        string guildName,
        uint damage)
    {
        if (guildId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guildId), guildId, "Guild id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(guildName);

        lock (_sync)
        {
            if (_scores.TryGetValue(guildId, out var existing))
            {
                var updated = existing with
                {
                    GuildName = guildName,
                    Score = SaturatingAdd(existing.Score, damage),
                };
                _scores[guildId] = updated;
                return updated;
            }

            var created = new LegacySuperGuildWarScoreEntry(guildId, guildName, damage);
            _scores[guildId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacySuperGuildWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.GuildId)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _gates.Clear();
            _scores.Clear();
            _claimedLeaderRewards.Clear();
            _claimedDeputyRewards.Clear();
            _rewardSeedKey = null;
            _leaderRewardsRemaining = 0;
            _deputyRewardsRemaining = 0;
        }
    }

    public void ClearScores()
    {
        lock (_sync)
        {
            _scores.Clear();
        }
    }

    private void SeedRewardsIfNeeded(GuildWarData? superGuildWarInfo)
    {
        var seedKey = CreateRewardSeedKey(superGuildWarInfo);
        if (string.Equals(_rewardSeedKey, seedKey, StringComparison.Ordinal))
        {
            return;
        }

        _claimedLeaderRewards.Clear();
        _claimedDeputyRewards.Clear();

        var info = superGuildWarInfo?.Info;
        _leaderRewardsRemaining = Math.Max(info?.LeaderReward ?? 0, 0);
        _deputyRewardsRemaining = Math.Max(info?.DeputiLeaderReward ?? 0, 0);
        SeedClaimedRewards(_claimedLeaderRewards, info?.LeaderTop);
        SeedClaimedRewards(_claimedDeputyRewards, info?.DeputiTop);
        _rewardSeedKey = seedKey;
    }

    private static LegacySuperGuildWarRewardClaim TryClaimReward(
        uint characterUid,
        LegacySuperGuildWarRewardKind kind,
        HashSet<uint> claimedRewards,
        ref int remaining)
    {
        if (claimedRewards.Contains(characterUid))
        {
            return new LegacySuperGuildWarRewardClaim(
                LegacySuperGuildWarRewardClaimStatus.AlreadyClaimed,
                kind,
                remaining);
        }

        if (remaining <= 0)
        {
            return new LegacySuperGuildWarRewardClaim(
                LegacySuperGuildWarRewardClaimStatus.Exhausted,
                kind,
                remaining);
        }

        remaining--;
        claimedRewards.Add(characterUid);
        return new LegacySuperGuildWarRewardClaim(
            LegacySuperGuildWarRewardClaimStatus.Granted,
            kind,
            remaining);
    }

    private static void SeedClaimedRewards(
        HashSet<uint> claimedRewards,
        IReadOnlyDictionary<int, int>? savedTop)
    {
        if (savedTop is null)
        {
            return;
        }

        foreach (var characterUid in savedTop.Values)
        {
            if (characterUid > 0)
            {
                claimedRewards.Add((uint)characterUid);
            }
        }
    }

    private static string CreateRewardSeedKey(GuildWarData? superGuildWarInfo)
    {
        var info = superGuildWarInfo?.Info;
        return string.Join(
            '|',
            superGuildWarInfo?.SourceFile ?? "",
            info?.Id?.ToString() ?? "",
            info?.LeaderReward?.ToString() ?? "",
            info?.DeputiLeaderReward?.ToString() ?? "",
            FormatTop(info?.LeaderTop),
            FormatTop(info?.DeputiTop));
    }

    private static string FormatTop(IReadOnlyDictionary<int, int>? savedTop)
    {
        return savedTop is null
            ? ""
            : string.Join(',', savedTop.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    private static uint SaturatingAdd(uint left, uint right)
    {
        return left > uint.MaxValue - right ? uint.MaxValue : left + right;
    }

    private sealed class LegacySuperGuildWarGateRuntimeState(
        bool IsOpen,
        int Mesh,
        uint Hitpoints,
        uint MaxHitpoints)
    {
        public bool IsOpen { get; set; } = IsOpen;

        public int Mesh { get; set; } = Mesh;

        public uint Hitpoints { get; set; } = Hitpoints;

        public uint MaxHitpoints { get; set; } = MaxHitpoints;

        public LegacySuperGuildWarGateSnapshot ToSnapshot(uint npcId)
        {
            return new LegacySuperGuildWarGateSnapshot(
                npcId,
                IsOpen,
                Mesh,
                Hitpoints,
                MaxHitpoints);
        }
    }
}

internal readonly record struct LegacySuperGuildWarGateMutation(
    LegacySuperGuildWarGateSnapshot Previous,
    LegacySuperGuildWarGateSnapshot Current)
{
    public bool MeshChanged => Previous.Mesh != Current.Mesh;

    public bool HitpointsChanged => Previous.Hitpoints != Current.Hitpoints;

    public bool Changed => MeshChanged ||
        HitpointsChanged ||
        Previous.IsOpen != Current.IsOpen ||
        Previous.MaxHitpoints != Current.MaxHitpoints;
}

internal readonly record struct LegacySuperGuildWarGateSnapshot(
    uint NpcId,
    bool IsOpen,
    int Mesh,
    uint Hitpoints,
    uint MaxHitpoints);

internal readonly record struct LegacySuperGuildWarScoreEntry(
    ulong GuildId,
    string GuildName,
    uint Score);

internal readonly record struct LegacySuperGuildWarRewardClaim(
    LegacySuperGuildWarRewardClaimStatus Status,
    LegacySuperGuildWarRewardKind Kind,
    int RemainingAfterClaim);

internal enum LegacySuperGuildWarRewardClaimStatus
{
    Granted,
    MissingWinner,
    NotWinnerGuild,
    UnsupportedRank,
    AlreadyClaimed,
    Exhausted,
}

internal enum LegacySuperGuildWarRewardKind
{
    None,
    Leader,
    DeputyLeader,
}
