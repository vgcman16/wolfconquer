using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record JoinedWindWalker(LegacyOrdinarySpellBinding Binding, LegacyNativeStatusFlagsBigVector32 Vector);
    private sealed class WindWalkerOwnerSlot { internal JoinedWindWalker? Owner; }
    private static readonly ConditionalWeakTable<LegacyNativePlayerRuntime, WindWalkerOwnerSlot> WindWalkerOwners = new();

    internal bool HasJoinedWindWalker(WorldSession session) => session.NativeGameClient?.Player is { } player &&
        WindWalkerOwners.TryGetValue(player, out var slot) && slot.Owner is { } joined &&
        ReferenceEquals(joined.Binding.Store, this) && WindWalkerBindingCurrentPlain(joined, player);

    private static bool WindWalkerBindingCurrentPlain(JoinedWindWalker joined, LegacyNativePlayerRuntime player) =>
        !joined.Binding.IsRetired && !joined.Binding.Recovery.IsBlocked &&
        ReferenceEquals(joined.Binding.Client.OrdinarySpellBinding, joined.Binding) &&
        ReferenceEquals(joined.Binding.Client.Player, player) && ReferenceEquals(joined.Binding.Player, player) &&
        ReferenceEquals(player.Owner, joined.Binding.Client) && player.UID == joined.Binding.Uid &&
        ReferenceEquals(player.BitVector, joined.Vector);

    // Wire projection only, after an actual query joined this physical owner.
    // No Store acquisition, timer birth, expiration-on-read or whole-vector adoption.
    internal static ulong[] CaptureWindWalkerVector(LegacyNativePlayerRuntime? player, IReadOnlyList<ulong> words)
    {
        if (words.Count != LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength) throw InvalidSpellPublication();
        var copy = words.ToArray();
        if (player is null || !WindWalkerOwners.TryGetValue(player, out var slot) || slot.Owner is not { } joined) return copy;
        lock (joined.Vector.SyncRoot)
        {
            if (!WindWalkerBindingCurrentPlain(joined, player) || !joined.Vector.TryCaptureWindWalker(out var captured) ||
                captured is null || !joined.Vector.IsWindWalkerCurrentPlain(captured)) throw InvalidSpellPublication();
            int word = LegacyWindWalkerStatusProjection.Word; ulong mask = LegacyWindWalkerStatusProjection.Mask;
            copy[word] = captured.Present ? copy[word] | mask : copy[word] & ~mask;
            return copy;
        }
    }

    private sealed class OwnedWindWalkerProjection : IDisposable
    {
        private readonly JoinedWindWalker joined;
        private readonly LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot captured;
        private bool held;
        internal WorldSession Proposed { get; }

        private OwnedWindWalkerProjection(JoinedWindWalker joined, WorldSession proposed)
        {
            this.joined = joined;
            Monitor.Enter(joined.Vector.SyncRoot, ref held);
            try
            {
                if (!WindWalkerBindingCurrentPlain(joined, joined.Binding.Player) ||
                    !joined.Vector.TryCaptureWindWalker(out var snapshot) || snapshot is null) throw InvalidSpellPublication();
                captured = snapshot;
                var character = LegacyWindWalkerStatusProjection.Apply(proposed.Character, captured);
                Proposed = ReferenceEquals(character, proposed.Character) ? proposed : proposed with { Character = character };
                if (!IsCurrentPlain()) throw InvalidSpellPublication();
            }
            catch { Dispose(); throw; }
        }

        internal static OwnedWindWalkerProjection? PrepareBound(LegacyOrdinarySpellBinding binding, WorldSession proposed)
        {
            if (!WindWalkerOwners.TryGetValue(binding.Player, out var slot) || slot.Owner is not { } joined) return null;
            if (!ReferenceEquals(joined.Binding, binding)) throw InvalidSpellPublication();
            return new(joined, proposed);
        }
        internal bool IsCurrentPlain() => held && WindWalkerBindingCurrentPlain(joined, joined.Binding.Player) &&
            WindWalkerOwners.TryGetValue(joined.Binding.Player, out var slot) && ReferenceEquals(slot.Owner, joined) &&
            joined.Vector.IsWindWalkerCurrentPlain(captured);
        public void Dispose() { if (held) { held = false; Monitor.Exit(joined.Vector.SyncRoot); } }
    }
}
