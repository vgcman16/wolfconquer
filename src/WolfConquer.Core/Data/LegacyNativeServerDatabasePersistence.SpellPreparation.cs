using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private sealed class SpellFileOwner
    {
        internal readonly LegacyNativeGameClientRuntime Client;
        internal readonly LegacyNativePlayerRuntime Player;
        internal readonly uint Uid;
        internal SpellFileOwner(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
        { Client = client; Player = player; Uid = player.UID; }
        internal void RequireCurrent()
        {
            if (Uid == 0 || !ReferenceEquals(Client.Player, Player) || !ReferenceEquals(Player.Owner, Client) || Player.UID != Uid)
                throw new InvalidOperationException("Spell file requires its current physical player.");
        }
    }

    private static SpellFileOwner CaptureSpellFileOwner(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        LegacyNativeSpellStorage.RequireDirectEntry(client);
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player ?? throw new InvalidOperationException("Spell file requires its physical player.");
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                var owner = new SpellFileOwner(client, player);
                owner.RequireCurrent();
                return owner;
            }
        }
    }

    private static LegacyNativeSpellStorage RequireFileSpellStorage(SpellFileOwner owner)
    {
        owner.RequireCurrent();
        var storage = owner.Client.MySpells;
        if (storage is null || !storage.IsOwnedBy(owner.Client))
            throw new InvalidOperationException("Spell file requires the actual current spell storage.");
        return storage;
    }

    private static void CloseOwnedSpellFile(LegacyNativeItemRecordFile? file, Exception? primary)
    {
        try { if (file?.Success == true) file.Close(); }
        catch (Exception cleanup)
        {
            if (primary is null) throw;
            throw new AggregateException("Spell file operation and closure failed.", primary, cleanup);
        }
    }

    private static LegacyNativeClientSpells.DBSpell[] CaptureSpellSaveRows(SpellFileOwner owner)
    {
        LegacyNativeSpellStorage.RequireDirectEntry(owner.Client);
        lock (owner.Client.SpellAdmissionSyncRoot)
        lock (owner.Player.SpellAdmissionSyncRoot)
        {
            LegacyNativeSpellStorage.RequireDirectEntry(owner.Client);
            var storage = RequireFileSpellStorage(owner);
            LegacyNativeSpellStorage.DirectSpellSnapshot captured;
            lock (storage.MutationGate) captured = storage.CaptureDirectSnapshot(owner.Client, owner.Player);
            var copied = captured.CopyValues();
            var rows = new LegacyNativeClientSpells.DBSpell[copied.Length];
            for (int index = 0; index < rows.Length; index++) rows[index].GetDBSpell(copied[index]);
            lock (storage.MutationGate) { owner.RequireCurrent(); captured.RequireCurrent(); }
            return rows;
        }
    }
}
