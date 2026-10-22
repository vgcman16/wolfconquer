using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Verification belongs to a physical current Player on one connection, never to
// serialized IsCheckedPass attributes or a UID cache. The binding is adapter
// provenance for the native fields, not a claim of complete native Player parity.
internal static class LegacySecondaryPasswordRuntime
{
    internal static object SyncRoot { get; } = new();
    private static readonly ConditionalWeakTable<LegacyNativePlayerRuntime, Binding> Bindings = new();

    private sealed record Binding(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client,
        long AccountId, long Uid, bool HasRaw, string? Raw, LegacySecondaryPasswordState State, bool IsChecked);

    internal static bool TryInitialize(ConquerTcpConnection connection, WorldSession session,
        LegacySecondaryPasswordState state)
    {
        lock (SyncRoot)
        {
            if (!TryPlayer(connection, session, out var player) ||
                !TryRaw(session, out var hasRaw, out var raw) ||
                !LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var stored, out _) ||
                stored.Password != state.Password || stored.OnReset != state.OnReset ||
                stored.OnReset == 1 && stored.ResetAt != state.ResetAt) return false;
            Write(player, state, state.Password == 0);
            Bindings.Remove(player);
            Bindings.Add(player, new(connection, player.Owner, session.AccountId, session.Character.Uid,
                hasRaw, raw, state, player.IsCheckedPass));
            return true;
        }
    }

    internal static void Invalidate(ConquerTcpConnection connection, WorldSession session)
    {
        lock (SyncRoot)
        {
            if (!TryPlayer(connection, session, out var player)) return;
            Bindings.Remove(player);
            player.IsCheckedPass = false;
        }
    }

    internal static bool TryGet(ConquerTcpConnection connection, WorldSession session,
        out LegacySecondaryPasswordState state, out bool isChecked)
    {
        lock (SyncRoot)
        {
            state = null!;
            isChecked = false;
            if (!TryPlayer(connection, session, out var player) || !Bindings.TryGetValue(player, out var binding) ||
                !ReferenceEquals(binding.Connection, connection) || !ReferenceEquals(binding.Client, player.Owner) ||
                binding.AccountId != session.AccountId || binding.Uid != session.Character.Uid ||
                !TryRaw(session, out var hasRaw, out var raw) || binding.HasRaw != hasRaw || binding.Raw != raw ||
                player.SecurityPassword != binding.State.Password || player.OnReset != binding.State.OnReset ||
                player.ResetSecurityPassowrd != binding.State.ResetAt || player.IsCheckedPass != binding.IsChecked)
                return false;
            state = binding.State;
            isChecked = binding.IsChecked;
            return true;
        }
    }

    internal static bool TryPublish(ConquerTcpConnection connection, WorldSession original, WorldSession updated,
        LegacySecondaryPasswordState state, bool isChecked)
    {
        lock (SyncRoot)
        {
            if (original.AccountId != updated.AccountId || !TryGet(connection, original, out _, out _) ||
                !TryPlayer(connection, updated, out var player) ||
                !TryRaw(updated, out var hasRaw, out var raw)) return false;
            Write(player, state, isChecked);
            Bindings.Remove(player);
            Bindings.Add(player, new(connection, player.Owner, updated.AccountId, updated.Character.Uid,
                hasRaw, raw, state, isChecked));
            return true;
        }
    }

    internal static bool IsAdmitted(ConquerTcpConnection connection, WorldSession session)
    {
        lock (SyncRoot)
        {
            if (!LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var state, out _)) return false;
            // A real Player's flag remains authoritative even if an in-session
            // PIN removal leaves Password=0 but IsCheckedPass=false. Only wholly
            // unbound managed fixtures retain the pre-existing zero-PIN fallback.
            if (TryGet(connection, session, out _, out var isChecked)) return isChecked;
            return connection.NativeGameClient is null && session.NativeGameClient is null && state.Password == 0;
        }
    }

    private static bool TryPlayer(ConquerTcpConnection connection, WorldSession session,
        out LegacyNativePlayerRuntime player)
    {
        player = null!;
        if (session.Character.Uid is <= 0 or > uint.MaxValue || connection.NativeGameClient is not { } client ||
            !ReferenceEquals(session.NativeGameClient, client) || client.Player is not { } current ||
            !ReferenceEquals(current.Owner, client) || current.UID != session.Character.Uid) return false;
        player = current;
        return true;
    }

    private static bool TryRaw(WorldSession session, out bool hasRaw, out string? raw)
    {
        hasRaw = false;
        raw = null;
        foreach (var pair in session.Character.Attributes)
        {
            if (!pair.Key.Equals("SecurityPass", StringComparison.OrdinalIgnoreCase)) continue;
            if (hasRaw || pair.Value is null) return false;
            hasRaw = true;
            raw = pair.Value;
        }
        return true;
    }

    private static void Write(LegacyNativePlayerRuntime player, LegacySecondaryPasswordState state, bool isChecked)
    {
        player.SecurityPassword = state.Password;
        player.OnReset = state.OnReset;
        player.ResetSecurityPassowrd = state.ResetAt;
        player.IsCheckedPass = isChecked;
    }
}
