using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private bool TryPersistChangedOrdinarySpells(ConquerTcpConnection connection, WorldSession? previous, WorldSession current)
    {
        if (connection.NativeGameClient?.OrdinarySpellBinding is not { } binding) return false;
        if (previous is null || ReferenceEquals(previous.PlayerSpells, current.PlayerSpells)) return true;
        try
        {
            if (_sessions.TryPersistPublishedOrdinarySpells(connection, current, _tradeRecovery,
                    _snapshotLoader?.PlayerSpellsRoot, out var reason)) return true;
            LogPlayerSpellPersistenceGap(current, "memoryCommitted:" + reason, _snapshotLoader?.PlayerSpellsRoot ?? string.Empty);
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            binding.Recovery.Block("ordinarySpellRecoveryRequired:autosavePublicationUnproven");
            throw;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or InvalidOperationException or FormatException or OverflowException or System.Security.SecurityException)
        {
            LogPlayerSpellPersistenceGap(current, "memoryCommitted:" + error.GetType().Name,
                _snapshotLoader?.PlayerSpellsRoot ?? string.Empty);
        }
        // A definite failed auto-save leaves an honest unsaved memory delta and
        // the old durable anchor. It is not an unchanged gameplay rejection.
        return true;
    }
}
