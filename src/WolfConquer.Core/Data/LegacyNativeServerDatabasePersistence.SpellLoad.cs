using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private static LegacyNativeMsgSpell[] ReadCompleteSpellFile(LegacyNativeItemRecordFile file,
        LegacyNativeGameClientRuntime client, ref SpellFileOwner? owner)
    {
        Exception? primary = null;
        try
        {
            ArgumentNullException.ThrowIfNull(file);
            owner ??= CaptureSpellFileOwner(client);
            var length = file.SpellFileLength;
            int count = 0;
            if (!file.ReadCount(ref count) || count is < 0 or > 65536 ||
                length != sizeof(int) + (long)count * Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>())
                throw new InvalidDataException("Spell file header/count does not describe a complete bounded image.");
            // 65536 is a defensive ingestion bound from the UInt16 keyspace,
            // not a claim that the legacy file grammar forbids redundant rows.
            var rows = new List<LegacyNativeMsgSpell>();
            for (int index = 0; index < count; index++)
            {
                var record = default(LegacyNativeClientSpells.DBSpell);
                if (!file.ReadSpell(ref record)) throw new InvalidDataException("Spell file contains an incomplete record.");
                rows.Add(record.GetClientSpell());
            }
            return rows.ToArray();
        }
        catch (Exception error) { primary = error; throw; }
        finally { CloseOwnedSpellFile(file, primary); }
    }

    private static void PublishLoadedSpellRows(SpellFileOwner owner, LegacyNativeMsgSpell[] rows)
    {
        var binding = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(owner.Client);
        if (binding is not null) throw new InvalidOperationException("Opened spell input cannot acquire an ordinary binding after parsing.");
        LegacyNativeSpellStorage.RequireDirectEntry(owner.Client);
        lock (owner.Client.SpellAdmissionSyncRoot)
        lock (owner.Player.SpellAdmissionSyncRoot)
        {
            owner.RequireCurrent();
            LegacyNativeSpellStorage.RequireDirectEntry(owner.Client);
            if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(owner.Client) is not null)
                throw new InvalidOperationException("Spell owner was admitted during opened input preparation.");
            var storage = RequireFileSpellStorage(owner);
            lock (storage.MutationGate)
            {
                var captured = storage.CaptureDirectSnapshot(owner.Client, owner.Player);
                // Actual lookup/candidate comparers are guarded against typed
                // reentry. File parsing/closure already completed outside here.
                var metadata = rows.Length == 0 ? null : new SpellFileMetadata(owner.Client, owner.Player);
                var admitted = metadata is null ? rows : metadata.Filter(rows);
                var prepared = captured.PrepareLoadedMap(admitted);
                owner.RequireCurrent();
                metadata?.RequireCurrent();
                prepared.Publish();
            }
        }
    }

    private static void WriteCompleteSpellRows(LegacyNativeItemRecordFile file, LegacyNativeClientSpells.DBSpell[] rows)
    {
        Exception? primary = null;
        try
        {
            ArgumentNullException.ThrowIfNull(file);
            int count = rows.Length;
            if (!file.WriteCount(ref count)) throw new IOException("Spell count write was incomplete.");
            foreach (var value in rows)
            {
                var record = value;
                if (!file.WriteSpell(ref record)) throw new IOException("Spell record write was incomplete.");
            }
        }
        catch (Exception error) { primary = error; throw; }
        finally { CloseOwnedSpellFile(file, primary); }
    }
}
