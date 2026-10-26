using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Flag);
        writeLine.Add(Exp);
        writeLine.Add(Fire);
        writeLine.Add(Water);
        writeLine.Add(Earth);
        writeLine.Add(Wind);
        writeLine.Add(Lightning);
        writeLine.Add(tmp_Fire);
        writeLine.Add(tmp_Water);
        writeLine.Add(tmp_Earth);
        writeLine.Add(tmp_Wind);
        writeLine.Add(tmp_Lightning);
        writeLine.Add(Score);
        writeLine.Add(FireMastery);
        writeLine.Add(WaterMastery);
        writeLine.Add(EarthMastery);
        writeLine.Add(WindMastery);
        writeLine.Add(LightningMastery);
        writeLine.Add(UnlockMastery);
        writeLine.Add(Items.Count);
        foreach (Item value in Items.Values)
        {
            writeLine.Add(value.ItemID);
            writeLine.Add(value.Level);
            writeLine.Add(value.Position);
            writeLine.Add(value.Exp);
            writeLine.Add(value.UK);
        }
        return writeLine.Close();
    }

    public void Load(string? line)
    {
        // Optional absent data has no mutation authority. A fresh native owner
        // is already empty; ServerDatabase's explicit missing-field sentinel is0.
        if (string.IsNullOrEmpty(line)) return;
        PersistenceState prepared;
        if (line == "0") prepared = new();
        else
        {
            if (!MailboxRecoveryLine.TryRead(line, out var parsed))
                throw new InvalidDataException("Native Ninja persisted state is incomplete or invalid.");
            prepared = PreparePersistenceState(parsed!);
        }
        lock (persistenceLoadGate)
        {
            if (ReadPersistenceState().Items is null)
                throw new InvalidDataException("Native Ninja storage owner is unavailable.");
            // No setters, metadata queries or existing comparer/row callbacks.
            // All rows and scalars are prepared before one aggregate publication.
            Volatile.Write(ref persistenceState, prepared);
        }
    }
}
