using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool UsesOrdinarySpellBinding => _protocolProfile == ConquerProtocolProfile.VisibleDx8D13;
}
