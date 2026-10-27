using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    internal bool IsOwnedBy(LegacyNativeGameClientRuntime client) => ReferenceEquals(Owner, client);

    internal uint PreparePrestigeScore(LegacyNativeGameClientRuntime client)
    {
        if (!IsOwnedBy(client) || !ReferenceEquals(client.HundredWeapons, this))
            throw new InvalidOperationException("Prestige preparation requires the real hundred-weapons owner.");
        if (!Unlocked || !Valid()) return 0;
        lock (slotSyncRoot)
        {
            if (!TrySnapshotSlotStorage(requireBitmap: true, out var rows))
                throw new InvalidOperationException("Prestige preparation requires valid hundred-weapons rows.");
            try
            {
                checked
                {
                    long totalScore = 0;
                    foreach (var row in rows) totalScore += PrepareHundredWeaponScore(row);
                    var total = checked((uint)totalScore);
                    var equipped = rows.FirstOrDefault(row => row.BitVector.Contain(1));
                    uint bonus = 0;
                    if (equipped is not null)
                    {
                        var score = PrepareHundredWeaponScore(equipped);
                        bonus = score - score * 2 / 100;
                    }
                    return total / 50 + bonus;
                }
            }
            catch (OverflowException error)
            {
                throw new InvalidOperationException("Hundred-weapons prestige preparation exceeded a declared arithmetic width.", error);
            }
        }
    }

    private static uint PrepareHundredWeaponScore(LegacyNativeHundredWeaponInfo row)
    {
        var levelScore = checked(row.Level * 1000);
        var attributes = row.Attributes
            ?? throw new InvalidOperationException("Hundred-weapons prestige preparation requires real attributes.");
        var attributeScore = attributes.Values.Sum();
        var metadataRows = LegacyNativeHundredWeaponsTable.HundredWeaponsList
            ?? throw new InvalidOperationException("Hundred-weapons prestige preparation requires its real metadata table.");
        if (metadataRows.Values.Any(value => value is null))
            throw new InvalidOperationException("Hundred-weapons prestige preparation found an invalid metadata row.");
        var metadata = row.DBInfo
            ?? throw new InvalidOperationException("Hundred-weapons prestige preparation requires matching metadata.");
        var metadataAttributes = metadata.Attributes
            ?? throw new InvalidOperationException("Hundred-weapons prestige preparation requires metadata attributes.");
        var metadataScore = metadataAttributes.Values.Sum();
        var score = checked(levelScore + checked(checked(attributeScore + metadataScore) * 5));
        if (score < 0)
            throw new InvalidOperationException("Hundred-weapons prestige preparation found a negative score.");
        return checked((uint)score);
    }
}
