using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
    internal sealed class PrefixLoad
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeHairfaceStorageRuntime? previous;
        internal LegacyNativeHairfaceStorageRuntime Target { get; }
        private readonly PrefixCatalog? catalog;
        private readonly PrefixHairfaceRow[] oldRows;
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface> oldItems;
        private readonly LegacyNativeHairfaceStorageTable.Hairface[] oldObjects;
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface> loadedItems;
        private readonly LegacyNativeHairfaceStorageTable.Hairface[] loadedObjects;

        private PrefixLoad(LegacyNativeGameClientRuntime client, LegacyNativeHairfaceStorageRuntime? previous,
            List<LegacyNativeHairfaceStorageTable.Hairface> loaded, PrefixCatalog? catalog)
        {
            this.client = client; player = client.Player; this.previous = previous; Target = previous ?? new(client);
            this.catalog = catalog;
            oldItems = Target.items; oldObjects = Target.objects;
            if (oldItems is null || oldObjects is null || oldItems.Count != oldObjects.Length || oldItems.Count > 4096)
                throw new InvalidDataException("Hairface storage is incomplete.");
            oldRows = new PrefixHairfaceRow[oldObjects.Length];
            for (var index = 0; index < oldRows.Length; index++) oldRows[index] = new PrefixHairfaceRow(oldObjects[index]);
            loadedItems = loaded.Count == 0 && oldRows.Length == 0 ? oldItems : loaded;
            loadedObjects = ReferenceEquals(loadedItems, oldItems) ? oldObjects : loaded.ToArray();
        }

        internal static bool TryPrepare(LegacyNativeGameClientRuntime client, string line, out PrefixLoad? prepared)
        {
            prepared = null;
            try
            {
                if (client?.Player is not { } player || !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake ||
                    line is null || line.Length is 0 or > 256 * 1024) return false;
                var old = client.HairfaceStorage;
                if (old is not null && !ReferenceEquals(old.Owner, client)) return false;
                var fields = line.Split('/');
                if (fields.Length < 2 || fields[^1] != "" || !Read(fields[0], out var count) || count > 4096) return false;
                PrefixCatalog? catalog = null;
                if (count != 0 && !PrefixCatalog.TryCapture(out catalog)) return false;
                var loaded = new List<LegacyNativeHairfaceStorageTable.Hairface>();
                var seen = new HashSet<(uint Id, LegacyHairFaceStorageType Type)>();
                int offset = 1;
                for (var index = 0U; index < count; index++)
                {
                    if (fields.Length - offset < 4 || !Read(fields[offset++], out var id) || id == 0 ||
                        !Read(fields[offset++], out var equipped) || equipped > byte.MaxValue ||
                        !Read(fields[offset++], out var colors) || colors > 256 || fields.Length - offset <= colors) return false;
                    var flags = new byte[colors];
                    for (var color = 0; color < flags.Length; color++)
                    {
                        if (!Read(fields[offset++], out var value) || value > byte.MaxValue) return false;
                        flags[color] = (byte)value;
                    }
                    var row = catalog!.Find(id);
                    if (row is null || !seen.Add((id, row.Type))) return false;
                    loaded.Add(row.CopyLoaded((byte)equipped, flags));
                }
                if (offset != fields.Length - 1) return false;
                var candidate = new PrefixLoad(client, old, loaded, catalog);
                if (!candidate.IsCurrent()) return false;
                prepared = candidate; return true;
            }
            catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException)
            { return false; }
        }

        private static bool Read(string value, out uint number) =>
            uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
            value == number.ToString(CultureInfo.InvariantCulture);

        internal bool IsCurrent()
        {
            if (catalog is not null && !catalog.IsCurrent() || !IsOwnerCurrent()) return false;
            for (var index = 0; index < oldRows.Length; index++)
                if (!ReferenceEquals(oldItems[index], oldObjects[index]) || !oldRows[index].Matches(oldObjects[index])) return false;
            return IsOwnerCurrent();
        }

        internal bool IsOwnerCurrent() => ReferenceEquals(client.HairfaceStorage, previous) && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && ReferenceEquals(Target.Owner, client) && !client.FullLoading && !client.Fake &&
            ReferenceEquals(Target.items, oldItems) && ReferenceEquals(Target.objects, oldObjects) && oldItems.Count == oldRows.Length &&
            oldObjects.Length == oldRows.Length;

        internal void PublishPrepared()
        {
            Target.items = loadedItems; Target.objects = loadedObjects;
            client.HairfaceStorage = Target;
        }
    }
}
