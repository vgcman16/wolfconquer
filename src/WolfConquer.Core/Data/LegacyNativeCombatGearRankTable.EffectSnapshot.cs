namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearRankTable
{
    // This is a detached read result, not a rank-publication transaction or
    // authority to repair any source table. No live arrays leave this owner.
    internal sealed class EffectRankSnapshot
    {
        private readonly Type[] types;
        private readonly uint[] ordinals;

        internal EffectRankSnapshot(Type[] types, uint[] ordinals)
        {
            this.types = types;
            this.ordinals = ordinals;
        }

        internal uint GetMyRank(Type type)
        {
            for (var index = 0; index < types.Length; index++)
                if (types[index] == type) return ordinals[index];
            throw new InvalidDataException("The combat-gear effect requested an uncaptured rank family.");
        }
    }

    internal static EffectRankSnapshot CaptureEffectRanks(Type[] requiredTypes, uint uid)
    {
        ArgumentNullException.ThrowIfNull(requiredTypes);
        var types = requiredTypes.ToArray();
        if (types.Length == 0) return new(types, []); // Unsupported class: no rank owner is consulted.
        var root = Ranks ?? throw InvalidEffectRanks();
        try
        {
            var owners = ResolveEffectOwners(root, types);
            var captured = new EffectRankCapture[owners.Length];
            var ordinals = new uint[owners.Length];
            for (var index = 0; index < owners.Length; index++)
            {
                var rank = owners[index];
                var gate = rank.SynRoot ?? throw InvalidEffectRanks();
                lock (gate)
                {
                    if (!ReferenceEquals(rank.SynRoot, gate) || rank._Type != types[index])
                        throw InvalidEffectRanks();
                    var list = rank.RankingList ?? throw InvalidEffectRanks();
                    var rows = list.ToArray();
                    var uids = new uint[rows.Length];
                    for (var row = 0; row < rows.Length; row++)
                    {
                        var entry = rows[row].Value ?? throw InvalidEffectRanks();
                        uids[row] = entry.UID;
                        if (ordinals[index] == 0 && uids[row] == uid)
                            ordinals[index] = checked((uint)row + 1);
                    }
                    captured[index] = new(rank, gate, list, rows, uids);
                }
            }

            // Existing cooperating writers use one rank's SynRoot at a time.
            // Never nest these public gates or invoke dictionary comparers.
            // Reject observed replacement/drift instead of returning a mixed
            // live-owner prefix; raw writes after this read remain separate.
            if (!ReferenceEquals(Ranks, root)) throw InvalidEffectRanks();
            var current = ResolveEffectOwners(root, types);
            for (var index = 0; index < current.Length; index++)
                ValidateEffectCapture(current[index], types[index], captured[index]);
            if (!ReferenceEquals(Ranks, root)) throw InvalidEffectRanks();
            return new(types, ordinals);
        }
        catch (InvalidOperationException error)
        {
            // Dictionary enumeration can detect a concurrent raw writer. That
            // is unavailable data, not rank zero and not a published result.
            throw new InvalidDataException("Combat-gear ranks changed during effect preparation.", error);
        }
    }

    private static Rank[] ResolveEffectOwners(Dictionary<Type, Rank> root, Type[] types)
    {
        var owners = new Rank[types.Length];
        // Physical enum keys determine the requested family. A caller-supplied
        // comparer cannot run code or bless a different rank at this boundary.
        foreach (var pair in root)
            for (var index = 0; index < types.Length; index++)
                if (pair.Key == types[index])
                {
                    if (owners[index] is not null || pair.Value is null || pair.Value._Type != types[index])
                        throw InvalidEffectRanks();
                    owners[index] = pair.Value;
                }
        foreach (var owner in owners)
            if (owner is null) throw InvalidEffectRanks();
        return owners;
    }

    private static void ValidateEffectCapture(Rank current, Type type, EffectRankCapture capture)
    {
        if (!ReferenceEquals(current, capture.Owner) || current._Type != type ||
            !ReferenceEquals(current.SynRoot, capture.Gate)) throw InvalidEffectRanks();
        lock (capture.Gate)
        {
            if (!ReferenceEquals(current.SynRoot, capture.Gate) ||
                !ReferenceEquals(current.RankingList, capture.List)) throw InvalidEffectRanks();
            var index = 0;
            foreach (var pair in capture.List)
            {
                if (index >= capture.Rows.Length || pair.Key != capture.Rows[index].Key ||
                    !ReferenceEquals(pair.Value, capture.Rows[index].Value) || pair.Value is null ||
                    pair.Value.UID != capture.Uids[index]) throw InvalidEffectRanks();
                index++;
            }
            if (index != capture.Rows.Length || current._Type != type) throw InvalidEffectRanks();
        }
    }

    private static InvalidDataException InvalidEffectRanks() =>
        new("A complete current combat-gear rank family is required before applying rank effects.");

    private sealed record EffectRankCapture(Rank Owner, object Gate, Dictionary<uint, Entry> List,
        KeyValuePair<uint, Entry>[] Rows, uint[] Uids);
}
