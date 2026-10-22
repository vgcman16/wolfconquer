using System.Collections.Concurrent;
using System.Drawing;

namespace WolfConquer.Core.World;

// Native GameMap constructor/storage prefix only (GameMap.cs485..491).
// LoadMap and its View publication are not implemented; no host map is adopted.
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
