using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyNobilityRankListEntry(
    uint Uid,
    uint Gender,
    uint Mesh,
    string Name,
    ulong Donation,
    byte Rank,
    int Position);

public readonly record struct LegacyNobilityThresholds(
    ulong DukeDonation,
    ulong PrinceDonation,
    ulong KingDonation);

public readonly record struct LegacyNobilityState(
    ulong Uid,
    string Name,
    uint Gender,
    uint Mesh,
    ulong Donation,
    byte Rank,
    int Position)
{
    public LegacyNobilityRankListEntry ToRankListEntry()
    {
        return new LegacyNobilityRankListEntry(
            CheckedUInt32(Uid),
            Gender,
            Mesh,
            Name,
            Donation,
            Rank,
            Position);
    }

    private static uint CheckedUInt32(ulong value)
    {
        return value > uint.MaxValue ? uint.MaxValue : (uint)value;
    }
}

public static class LegacyNobilityRanking
{
    public const ulong KnightDonation = 30_000_000;
    public const ulong BaronDonation = 100_000_000;
    public const ulong EarlDonation = 200_000_000;
    public const byte RankSerf = 0;
    public const byte RankKnight = 1;
    public const byte RankBaron = 3;
    public const byte RankEarl = 5;
    public const byte RankDuke = 7;
    public const byte RankPrince = 9;
    public const byte RankKing = 12;
    public const int DefaultPosition = -1;

    private static readonly Encoding SnapshotEncoding = CreateSnapshotEncoding();

    public static LegacyNobilityState ResolveForSnapshot(
        CharacterSnapshot snapshot,
        string? usersRoot = null,
        WorldSessionStore? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (TryResolveActivePaidSnapshot(snapshot, out var paidState))
        {
            return paidState;
        }

        var donation = ReadRegularDonation(snapshot);
        var state = CreateUnrankedState(snapshot, donation);
        if (donation <= EarlDonation)
        {
            return state with { Rank = ComputeRank(donation, DefaultPosition) };
        }

        if (!HasLocalRankingData(usersRoot, sessions))
        {
            if (TryReadByte(snapshot.Attributes, "NobilityRank", out var cachedRank) &&
                cachedRank != RankSerf)
            {
                return state with
                {
                    Rank = cachedRank,
                    Position = TryReadInt32(snapshot.Attributes, "NobilityPosition", out var cachedPosition)
                        ? cachedPosition
                        : DefaultPosition,
                };
            }

            return state with { Rank = ComputeRank(donation, DefaultPosition) };
        }

        foreach (var ranked in BuildRankedStates(usersRoot, sessions, snapshot))
        {
            if (ranked.Uid == state.Uid)
            {
                return ranked;
            }
        }

        return state with { Rank = ComputeRank(donation, DefaultPosition) };
    }

    public static IReadOnlyList<LegacyNobilityRankListEntry> CreateRankListEntries(
        string? usersRoot,
        WorldSessionStore? sessions = null)
    {
        return BuildRankedStates(usersRoot, sessions)
            .Select(state => state.ToRankListEntry())
            .ToArray();
    }

    public static LegacyNobilityThresholds CreateThresholds(
        string? usersRoot,
        WorldSessionStore? sessions = null)
    {
        var ranked = BuildRankedStates(usersRoot, sessions).ToArray();
        return new LegacyNobilityThresholds(
            DonationAtPosition(ranked, 49),
            DonationAtPosition(ranked, 14),
            DonationAtPosition(ranked, 2));
    }

    public static string RankName(byte rank)
    {
        return rank switch
        {
            RankKnight => "Knight",
            RankBaron => "Baron",
            RankEarl => "Earl",
            RankDuke => "Duke",
            RankPrince => "Prince",
            RankKing => "King",
            _ => "Serf",
        };
    }

    public static ulong ReadRegularDonation(CharacterSnapshot snapshot)
    {
        if (TryReadUInt64(snapshot.Attributes, "DonationNobility", out var donation))
        {
            return donation;
        }

        return TryReadUInt64(snapshot.Attributes, "LastNobilityDonation", out donation)
            ? donation
            : 0;
    }

    public static byte ComputeRank(ulong donation, int position)
    {
        if (position >= 0 && position < 3)
        {
            return RankKing;
        }

        if (position >= 3 && position < 17)
        {
            return RankPrince;
        }

        if (position >= 17 && position < 50)
        {
            return RankDuke;
        }

        if (donation >= EarlDonation)
        {
            return RankEarl;
        }

        if (donation >= BaronDonation)
        {
            return RankBaron;
        }

        if (donation >= KnightDonation)
        {
            return RankKnight;
        }

        return RankSerf;
    }

    private static IReadOnlyList<LegacyNobilityState> BuildRankedStates(
        string? usersRoot,
        WorldSessionStore? sessions = null,
        CharacterSnapshot? overrideSnapshot = null)
    {
        var states = new Dictionary<ulong, LegacyNobilityState>();

        if (!string.IsNullOrWhiteSpace(usersRoot) &&
            Directory.Exists(usersRoot))
        {
            foreach (var path in Directory.EnumerateFiles(usersRoot, "*.ini"))
            {
                if (TryLoadState(path, out var state))
                {
                    states[state.Uid] = state;
                }
            }
        }

        if (sessions is not null)
        {
            foreach (var active in sessions.Snapshot())
            {
                AddSnapshotState(states, active.Session.Character);
            }
        }

        if (overrideSnapshot is not null)
        {
            AddSnapshotState(states, overrideSnapshot);
        }

        return states.Values
            .Where(state => state.Donation > EarlDonation)
            .OrderByDescending(state => state.Donation)
            .ThenBy(state => state.Uid)
            .Take(50)
            .Select((state, index) => state with
            {
                Position = index,
                Rank = ComputeRank(state.Donation, index),
            })
            .ToArray();
    }

    private static bool HasLocalRankingData(string? usersRoot, WorldSessionStore? sessions)
    {
        if (!string.IsNullOrWhiteSpace(usersRoot) &&
            Directory.Exists(usersRoot))
        {
            return true;
        }

        return sessions?.Snapshot().Count > 0;
    }

    private static void AddSnapshotState(
        IDictionary<ulong, LegacyNobilityState> states,
        CharacterSnapshot snapshot)
    {
        if (TryIsActivePaid(snapshot.PaidNobility, out var activePaid) &&
            activePaid)
        {
            states.Remove(CheckedUInt64(snapshot.Uid));
            return;
        }

        var state = CreateUnrankedState(snapshot, ReadRegularDonation(snapshot));
        states[state.Uid] = state;
    }

    private static bool TryLoadState(string path, out LegacyNobilityState state)
    {
        state = default;
        try
        {
            var snapshot = CharacterSnapshotParser.ParseText(
                SnapshotEncoding.GetString(File.ReadAllBytes(path)),
                Path.GetFileName(path));
            if (TryIsActivePaid(snapshot.PaidNobility, out var activePaid) &&
                activePaid)
            {
                return false;
            }

            state = CreateUnrankedState(snapshot, ReadRegularDonation(snapshot));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static LegacyNobilityState CreateUnrankedState(CharacterSnapshot snapshot, ulong donation)
    {
        return new LegacyNobilityState(
            CheckedUInt64(snapshot.Uid),
            snapshot.Name,
            GenderFromBody(snapshot.Body),
            MeshFromSnapshot(snapshot),
            donation,
            ComputeRank(donation, DefaultPosition),
            DefaultPosition);
    }

    private static bool TryResolveActivePaidSnapshot(
        CharacterSnapshot snapshot,
        out LegacyNobilityState state)
    {
        state = default;
        if (TryIsActivePaid(snapshot.PaidNobility, out var activePaid) &&
            activePaid)
        {
            if (!TryReadPaidRank(snapshot.PaidNobility, out var rank))
            {
                return false;
            }

            state = CreatePaidState(snapshot, rank);
            return true;
        }

        if (!snapshot.PaidNobility.ContainsKey("IsActive") &&
            !snapshot.PaidNobility.ContainsKey("PaidRank") &&
            TryReadUInt64(snapshot.PaidNobility, "LastNobilityDonation", out var donation) &&
            TryReadPaidRank(snapshot.PaidNobility, out var legacyRank))
        {
            state = CreateUnrankedState(snapshot, donation) with
            {
                Rank = legacyRank,
                Position = DefaultPosition,
            };
            return true;
        }

        return false;
    }

    private static LegacyNobilityState CreatePaidState(CharacterSnapshot snapshot, byte rank)
    {
        return CreateUnrankedState(snapshot, donation: 0) with
        {
            Rank = rank,
            Position = rank switch
            {
                RankKing => 0,
                RankPrince => 4,
                RankDuke => 15,
                _ => DefaultPosition,
            },
        };
    }

    private static bool TryReadPaidRank(
        IReadOnlyDictionary<string, string> paidNobility,
        out byte rank)
    {
        if (TryReadByte(paidNobility, "PaidRank", out rank) &&
            rank != RankSerf)
        {
            return true;
        }

        if (paidNobility.TryGetValue("Rank", out var rankName))
        {
            rank = rankName.Trim() switch
            {
                "Knight" => RankKnight,
                "Baron" => RankBaron,
                "Earl" => RankEarl,
                "Duke" => RankDuke,
                "Prince" => RankPrince,
                "King" => RankKing,
                _ => RankSerf,
            };
            return rank != RankSerf;
        }

        rank = RankSerf;
        return false;
    }

    private static bool TryIsActivePaid(
        IReadOnlyDictionary<string, string> paidNobility,
        out bool active)
    {
        active = false;
        if (!paidNobility.TryGetValue("IsActive", out var raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out active))
        {
            return true;
        }

        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            active = numeric != 0;
            return true;
        }

        return false;
    }

    private static ulong DonationAtPosition(IReadOnlyList<LegacyNobilityState> ranked, int position)
    {
        return ranked.Count > position ? ranked[position].Donation : 0;
    }

    private static uint GenderFromBody(long body)
    {
        return body % 10 < 3 ? 1u : 0u;
    }

    private static uint MeshFromSnapshot(CharacterSnapshot snapshot)
    {
        var mesh = checked(snapshot.Face * 10_000 + snapshot.Body);
        return mesh <= 0 ? 0 : mesh > uint.MaxValue ? uint.MaxValue : (uint)mesh;
    }

    private static bool TryReadUInt64(
        IReadOnlyDictionary<string, string> values,
        string key,
        out ulong parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryReadByte(
        IReadOnlyDictionary<string, string> values,
        string key,
        out byte parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) &&
            byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryReadInt32(
        IReadOnlyDictionary<string, string> values,
        string key,
        out int parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static ulong CheckedUInt64(long value)
    {
        return value < 0 ? 0 : (ulong)value;
    }

    private static Encoding CreateSnapshotEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
