namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Caller owns actual Frame Shared + Trades; no snapshot DTOs/IDs escape.
    internal bool TryCountPublicHeartbeatSessions(LegacyTradeStateStore trades, out int count)
    {
        count = 0;
        if (!Monitor.IsEntered(trades.SyncRoot)) return false;
        foreach (var pair in _sessions)
        {
            var current = pair.Value.Read();
            if (current is null || trades.HasConnectionEnded(pair.Key)) continue;
            if (current.AccountId is <= 0 or > uint.MaxValue ||
                current.Character is not { Uid: > 0 and <= uint.MaxValue } || !pair.Key.HasSendRoute ||
                !ReferenceEquals(pair.Value.Read(), current)) return false;
            if (pair.Value.SpellBinding is { } binding)
            {
                // Recovery state is reported separately. A blocked world can
                // still have real admitted holders; do not invent zero players.
                if (binding.IsRetired || !ReferenceEquals(binding.Store, this) ||
                    !ReferenceEquals(binding.Connection, pair.Key) ||
                    !ReferenceEquals(pair.Value.NativeClientIdentity, binding.NativeIdentity) ||
                    !ReferenceEquals(pair.Key.NativeGameClient, binding.Client) ||
                    !ReferenceEquals(current.NativeGameClient, binding.Client) ||
                    !ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) ||
                    !ReferenceEquals(binding.Client.Player, binding.Player) ||
                    !ReferenceEquals(binding.Player.Owner, binding.Client) ||
                    binding.Player.UID != current.Character.Uid || binding.AccountId != current.AccountId ||
                    binding.Uid != current.Character.Uid ||
                    !string.Equals(current.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) ||
                    !ReferenceEquals(current.PlayerSpells, binding.Snapshot) ||
                    !ReferenceEquals(current.PlayerProficiencies, binding.ProficiencySnapshot) ||
                    !ReferenceEquals(current.PlayerItems, binding.ItemSnapshot)) return false;
            }
            else if (pair.Key.NativeGameClient is not null || current.NativeGameClient is not null) return false;
            if (!ReferenceEquals(pair.Value.Read(), current)) return false;
            if (count == 1_000_000) return false;
            count++;
        }
        return true;
    }
}
