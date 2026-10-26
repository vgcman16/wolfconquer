namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinjaFile
{
    private static (uint Key, NinjaSprintItem Row) ParseNinjaType(string[] fields, IFormatProvider culture)
    {
        if (fields.Length < 12) throw new InvalidDataException("Native Ninja sigil row is incomplete.");
        var row = new NinjaSprintItem
        {
            ID = Convert.ToUInt32(fields[0], culture), ItemID = Convert.ToUInt32(fields[1], culture),
            Level = Convert.ToUInt32(fields[2], culture), Exp = Convert.ToUInt32(fields[3], culture),
            UK1 = Convert.ToUInt32(fields[4], culture), UK2 = Convert.ToUInt32(fields[5], culture),
            magic1 = Convert.ToUInt32(fields[6], culture), magic2 = Convert.ToUInt32(fields[7], culture),
            Power = Convert.ToInt32(fields[8], culture), Damage = Convert.ToUInt32(fields[9], culture),
            // Source10 is opaque; both preserved fields read physical11.
            Score = Convert.ToUInt32(fields[11], culture), UK5 = Convert.ToUInt32(fields[11], culture),
        };
        return (row.ID, row);
    }

    private static (uint Key, Item Row) ParseNinjaImmortal(string[] fields, IFormatProvider culture)
    {
        if (fields.Length < 16) throw new InvalidDataException("Native Ninja immortal row is incomplete.");
        var row = new Item
        {
            Level = Convert.ToUInt32(fields[0], culture), U1 = Convert.ToUInt32(fields[1], culture),
            MaxHP = Convert.ToUInt32(fields[2], culture), PAttack = Convert.ToUInt32(fields[3], culture),
            PDefense = Convert.ToUInt32(fields[4], culture), MDefense = Convert.ToUInt32(fields[5], culture),
            Chance = Convert.ToUInt32(fields[6], culture), Seconds = Convert.ToUInt32(fields[7], culture), Rate = Convert.ToUInt32(fields[8], culture),
            // Source9..11 are opaque and remain unqueried.
            MovementSpeed = Convert.ToUInt32(fields[12], culture), U13 = Convert.ToUInt32(fields[13], culture),
            U14 = Convert.ToUInt32(fields[14], culture), P = Convert.ToUInt32(fields[15], culture),
        };
        return (row.Level, row);
    }
}
