namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public bool CheckProf(ushort ID, byte level)
    {
        lock (mutationGate)
        {
            var map = ClientProf ?? throw UnavailableProficiencies();
            RequireOrdinaryAnchorCurrent();
            var rows = CaptureRows(map);
            RequireRows(map, rows);
            if (!ReferenceEquals(map, ClientProf)) throw UnavailableProficiencies();
            foreach (var row in rows)
                if (row.Key == ID) return row.Value.Level >= level;
            return false;
        }
    }
}
