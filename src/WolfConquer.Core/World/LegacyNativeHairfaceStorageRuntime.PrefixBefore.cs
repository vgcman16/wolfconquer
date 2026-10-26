namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
    internal sealed class PrefixBefore
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativeHairfaceStorageRuntime? owner;
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface>? items;
        private readonly LegacyNativeHairfaceStorageTable.Hairface[]? objects;
        private readonly PrefixHairfaceRow[] rows = [];

        internal PrefixBefore(LegacyNativeGameClientRuntime client)
        {
            this.client = client; owner = client.HairfaceStorage;
            if (owner is null) return;
            items = owner.items; objects = owner.objects;
            if (!ReferenceEquals(owner.Owner, client) || items is null || objects is null || items.Count > 4096 || items.Count != objects.Length)
                throw new InvalidDataException("Hairface constructor storage is invalid.");
            rows = new PrefixHairfaceRow[objects.Length];
            for (var index = 0; index < rows.Length; index++)
            {
                if (!ReferenceEquals(items[index], objects[index])) throw new InvalidDataException("Hairface cached rows are incoherent.");
                rows[index] = new(objects[index]);
            }
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(client.HairfaceStorage, owner)) return false;
            if (owner is null) return true;
            if (!ReferenceEquals(owner.Owner, client) || !ReferenceEquals(owner.items, items) || !ReferenceEquals(owner.objects, objects) ||
                items!.Count != rows.Length || objects!.Length != rows.Length) return false;
            for (var index = 0; index < rows.Length; index++)
                if (!ReferenceEquals(items[index], objects[index]) || !rows[index].Matches(objects[index])) return false;
            return true;
        }
    }
}
