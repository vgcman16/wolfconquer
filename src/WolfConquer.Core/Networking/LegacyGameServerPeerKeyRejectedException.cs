using System.Security.Cryptography;

namespace WolfConquer.Core.Networking;

// Expected rejection of an unusable peer contribution, not a game-dispatch fault.
public sealed class LegacyGameServerPeerKeyRejectedException : CryptographicException
{
    public LegacyGameServerPeerKeyRejectedException() : base("Legacy GameServer DH peer key rejected.") { }
}
