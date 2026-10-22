using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Invocation-local proof, never session state or a reusable skip switch.
internal sealed record LegacyRootFirstFamilyInvocation(
    ConquerTcpConnection Connection, long OwnerUid, ushort SpellId, ushort Level,
    ushort SoulSpellId, bool NativeCanUseEntered, bool Prepared)
{
    internal object? NativeClientIdentity { get; init; } // Existing invocation-local proof, never session state.
}
