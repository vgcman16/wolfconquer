using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Only the actual owning Frame issues this constructor/catalog context.
    // No global native metadata table or new client-column mapping is installed.
    internal (WorldDataCatalog? Catalog, ConquerProtocolProfile Profile) RequireOwnedNativeItemContext()
    {
        if (_protocolProfile != ConquerProtocolProfile.VisibleDx8D13)
            throw new InvalidOperationException("ordinaryNativeItemsRequireOwnedD13Profile");
        // Absence is genuine Frame provenance. Image preparation determines
        // whether the actual item graph requires Rune constructor metadata.
        return (_worldData, _protocolProfile);
    }
}
