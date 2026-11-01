using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Pure scoped DATA edit. The raw witness/comparer cover the complete map;
    // only requested runtime fields have case-insensitive schema semantics.
    private sealed class RuntimeTickAttributeEdit
    {
        private readonly CharacterSnapshot source;
        private readonly LegacyNativeAttributeMapProof raw;
        private readonly Dictionary<string, string> values;
        private readonly Dictionary<string, string> originalKeys = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, string> Owned { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal RuntimeTickAttributeEdit(CharacterSnapshot source, IReadOnlyCollection<string> owned)
        {
            this.source = source;
            raw = LegacyNativeAttributeMapProof.CaptureRaw(source.Attributes);
            values = new(raw.Fields, raw.CapturedComparer);
            foreach (var pair in raw.Fields)
                foreach (var key in owned)
                    if (key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!originalKeys.TryAdd(key, pair.Key) || !Owned.TryAdd(key, pair.Value))
                            throw new InvalidDataException("runtimeTickOwnedFieldAmbiguous");
                        break;
                    }
        }

        internal void Set(string key, string value)
        {
            if (!originalKeys.TryGetValue(key, out var original))
            {
                original = key;
                originalKeys.Add(key, key);
            }
            values[original] = value;
            Owned[key] = value;
        }

        internal void Remove(string key)
        {
            if (originalKeys.TryGetValue(key, out var original)) values.Remove(original);
            Owned.Remove(key);
        }

        internal CharacterSnapshot Complete()
        {
            if (!raw.IsCurrentPlain()) throw new InvalidDataException("runtimeTickAttributeSourceChanged");
            return source with { Attributes = new ReadOnlyDictionary<string, string>(values) };
        }
    }
}
