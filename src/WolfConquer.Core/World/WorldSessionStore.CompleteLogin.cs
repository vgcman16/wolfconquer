using System.Collections.ObjectModel;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // MsgDataPacket.Data132 sets the physical Player's flag only after startup
    // sends. Preserve current state accumulated during those awaits, not the old
    // input record. This publication retains Set's ordinary callback semantics.
    internal bool TryCompleteLegacyLogin(ConquerTcpConnection connection, WorldSession original,
        LegacyCompleteLoginRuntime.Owner? owner, out WorldSession? updated)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(original);
        updated = null;
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || holder.Read() is not { } current ||
                current.AccountId != original.AccountId || current.Character.Uid != original.Character.Uid)
                return false;
            if (owner is null)
            {
                // Compatibility for wholly unbound managed fixtures only. A
                // physical owner can never be fabricated from the saved scalar.
                if (connection.NativeGameClient is not null || current.NativeGameClient is not null ||
                    original.NativeGameClient is not null) return false;
            }
            else if (!LegacyCompleteLoginRuntime.Matches(owner, connection, original) ||
                !LegacyCompleteLoginRuntime.Matches(owner, connection, current)) return false;

            var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in current.Character.Attributes)
                if (!key.Equals("CompleteLogin", StringComparison.OrdinalIgnoreCase)) attrs.Add(key, value);
            attrs.Add("CompleteLogin", "1");
            var next = current with
            {
                Character = current.Character with { Attributes = new ReadOnlyDictionary<string, string>(attrs) },
            };
            if (owner is not null && !LegacyCompleteLoginRuntime.TryComplete(owner, connection, current)) return false;
            holder.Publish(next);
            updated = next;
            _sessionChanged?.Invoke(connection, current, next);
            return true;
        }
    }
}
