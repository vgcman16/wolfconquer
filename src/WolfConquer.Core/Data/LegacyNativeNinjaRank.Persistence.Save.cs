using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    private static byte[] CapturePersistenceImage(CultureInfo culture)
    {
        var registry = Ranks ?? throw PersistenceOwnerChanged();
        var registryGate = registry.SyncRoot ?? throw PersistenceOwnerChanged();
        var held = new List<object>((int)Type.Count + 1);
        try
        {
            EnterPersistenceGate(registryGate, held);
            if (!ReferenceEquals(Ranks, registry) || !ReferenceEquals(registry.SyncRoot, registryGate))
                throw PersistenceOwnerChanged();
            var owners = CaptureOwners(registry);
            foreach (var owner in owners)
                EnterPersistenceGate(owner.SynRoot ?? throw PersistenceOwnerChanged(), held);
            if (!ReferenceEquals(Ranks, registry)) throw PersistenceOwnerChanged();

            var rows = new List<string>(MaximumPersistenceRows);
            for (var category = 0; category < owners.Length; category++)
                CapturePersistenceRows(owners[category], (Type)category, culture, rows);
            var builder = new StringBuilder();
            builder.Append("Count=").Append(rows.Count.ToString(culture)).Append(Environment.NewLine);
            foreach (var row in rows) builder.Append(row).Append(Environment.NewLine);
            byte[] image;
            try { image = new UTF8Encoding(false, true).GetBytes(builder.ToString()); }
            catch (EncoderFallbackException error)
            {
                throw new InvalidOperationException("Ninja rank graph contains invalid Unicode text.", error);
            }
            if (image.Length > MaximumPersistenceBytes)
                throw new InvalidOperationException("Ninja rank graph exceeds its offline byte bound.");
            return image;
        }
        finally
        {
            for (var index = held.Count - 1; index >= 0; index--) Monitor.Exit(held[index]);
        }
    }

    private static void CapturePersistenceRows(Rank owner, Type category, CultureInfo culture, List<string> rows)
    {
        if (owner._Type != category || owner.MaxItems != 10 || owner.RankingList is not { } map || map.Count > 10)
            throw new InvalidOperationException("Ninja rank graph is incomplete or has an invalid category owner.");
        uint? previous = null;
        foreach (var pair in map)
        {
            var entry = pair.Value;
            if (pair.Key == 0 || entry is null || entry.UID != pair.Key || entry.Type != category ||
                previous is { } points && entry.TotalPoints > points)
                throw new InvalidOperationException("Ninja rank graph contains an invalid or unsorted row.");
            previous = entry.TotalPoints;
            var row = PersistenceRow(entry, culture);
            if (row.Length > MaximumPersistenceLineCharacters)
                throw new InvalidOperationException("Ninja rank row exceeds its offline character bound.");
            rows.Add(row);
        }
    }

    private static string PersistenceRow(Entry entry, CultureInfo culture)
    {
        RequirePersistenceText(entry.Name);
        RequirePersistenceText(entry.GuildName);
        var values = new object[]
        {
            entry.UID, entry.Name, (byte)entry.Type, entry.TotalPoints, entry.Class, entry.Level,
            entry.Mesh, entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon,
            entry.LefttWeaponAccessory, entry.RightWeapon, entry.RightWeaponAccessory,
            entry.MountArmor, entry.Armor, entry.Wing, entry.WingPlus, entry.Title, entry.Flag,
            entry.GuildName,
        };
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            if (value is IFormattable formattable) builder.Append(formattable.ToString(null, culture));
            else builder.Append((string)value);
            builder.Append('/');
        }
        return builder.ToString();
    }

    private static void RequirePersistenceText(string value)
    {
        if (value is null || value.Contains('/') || value.Any(char.IsControl))
            throw new InvalidOperationException("Ninja rank text cannot be represented by the legacy row format.");
        try { _ = new UTF8Encoding(false, true).GetByteCount(value); }
        catch (EncoderFallbackException error)
        {
            throw new InvalidOperationException("Ninja rank text contains invalid Unicode.", error);
        }
    }

    private static void EnterPersistenceGate(object gate, List<object> held)
    {
        Monitor.Enter(gate);
        held.Add(gate);
    }

    private static void WritePersistenceImage(PersistenceLocation location, byte[] image)
    {
        RequirePersistenceLocationCurrent(location);
        var destination = Path.GetFullPath(location.Path);
        var stage = destination + "." + Guid.NewGuid().ToString("N") + ".ninja-stage";
        var ownsStage = false;
        Exception? primary = null;
        try
        {
            using (var file = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsStage = true;
                file.Write(image);
                file.Flush(flushToDisk: true);
            }
            if (!PersistenceImageMatches(stage, image))
                throw new IOException("Ninja rank stage image verification failed.");
            RequirePersistenceLocationCurrent(location);
            if (File.Exists(destination)) File.Replace(stage, destination, null, ignoreMetadataErrors: false);
            else File.Move(stage, destination, overwrite: false);
            ownsStage = false;
            if (!PersistenceImageMatches(destination, image))
                throw new IOException("Ninja rank destination image verification failed.");
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            if (ownsStage)
            {
                try { File.Delete(stage); }
                catch (Exception cleanup) when (primary is not null)
                {
                    throw new AggregateException("Ninja rank save and stage cleanup both failed.", primary, cleanup);
                }
            }
        }
    }

    private static bool PersistenceImageMatches(string path, byte[] expected)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != expected.Length) return false;
        var actual = new byte[expected.Length];
        file.ReadExactly(actual);
        return file.ReadByte() == -1 && actual.AsSpan().SequenceEqual(expected);
    }
}
