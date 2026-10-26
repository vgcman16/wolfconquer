using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinjaFile
{
    // Source ElTabakh/NinjaFile.Load paths/schema, staged before publication.
    // No normal startup/Host caller is installed by this bounded loader change.
    public static void Load()
    {
        lock (tableLoadGate)
        {
            var prefix = LegacyNativeServerConfig.DbLocation;
            var firstPath = prefix + "gouyu_type.txt";
            var secondPath = prefix + "gouyu_immortal.txt";
            var directory = Environment.CurrentDirectory;
            var firstAbsolute = Path.GetFullPath(firstPath, directory);
            var secondAbsolute = Path.GetFullPath(secondPath, directory);
            var culture = CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());
            if (!File.Exists(firstAbsolute)) { Console.WriteLine("!File.Exists: " + firstPath); return; }
            if (!File.Exists(secondAbsolute)) { Console.WriteLine("!File.Exists: " + secondPath); return; }
            using var first = new FileStream(firstAbsolute, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var second = new FileStream(secondAbsolute, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (first.Length > MaximumTableBytes || second.Length > MaximumTableBytes - first.Length)
                throw new InvalidDataException("Native Ninja table byte bound exceeded.");
            var remaining = MaximumTableRows;
            var types = ReadNinjaTable(first, fields => ParseNinjaType(fields, culture), ref remaining);
            var immortals = ReadNinjaTable(second, fields => ParseNinjaImmortal(fields, culture), ref remaining);
            // Both actual file leases are still held. No earlier valid row or
            // table became live while a later parse/identity/IO check could fail.
            Volatile.Write(ref tables, new NinjaTables(types, immortals));
        }
    }
}
