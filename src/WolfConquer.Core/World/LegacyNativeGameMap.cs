using System.Collections.Concurrent;
using System.Drawing;

namespace WolfConquer.Core.World;

// Native GameMap constructor/storage (GameMap.cs485..491). Whole terrain loading,
// membership, collision and lazy population live in partials; host startup is not adopted.
internal partial class LegacyNativeGameMap
{
    public Size bounds;

    public LegacyNativeMapView? View;

    private ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Clients;

    public LegacyNativeMapFlagType[,] cells { get; set; }

    public uint ID { get; private set; }

    public LegacyNativeGameMap(int width, int height, int m_id)
    {
        Clients = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
        cells = new LegacyNativeMapFlagType[width, height];
        bounds = new Size(width, height);
        ID = (uint)m_id;
    }
}
