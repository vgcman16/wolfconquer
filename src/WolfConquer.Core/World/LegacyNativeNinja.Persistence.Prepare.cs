using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private static PersistenceState PreparePersistenceState(MailboxRecoveryLine parsed)
    {
        var input = parsed.CopyPersistence();
        var items = new ConcurrentDictionary<uint, Item>();
        foreach (var row in input.Rows)
        {
            var item = new Item { ItemID = row.Id, Level = row.Level, Position = row.Position, Exp = row.Exp, UK = row.UK };
            if (!items.TryAdd(row.Id, item))
                throw new InvalidDataException("Native Ninja persisted item identity is duplicated.");
        }
        return new PersistenceState
        {
            Flag = input.Flag, Items = items,
            Exp = input.Values[0],
            Fire = input.Values[1],
            Water = input.Values[2],
            Earth = input.Values[3],
            Wind = input.Values[4],
            Lightning = input.Values[5],
            tmp_Fire = input.Values[6],
            tmp_Water = input.Values[7],
            tmp_Earth = input.Values[8],
            tmp_Wind = input.Values[9],
            tmp_Lightning = input.Values[10],
            // Stored derived Score is validated by the parser and deliberately ignored.
            FireMastery = input.Values[12],
            WaterMastery = input.Values[13],
            EarthMastery = input.Values[14],
            WindMastery = input.Values[15],
            LightningMastery = input.Values[16],
            UnlockMastery = input.Values[17],
        };
    }
}
