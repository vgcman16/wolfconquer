using System.Security.Cryptography;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Core.Runtime;

public static class ConquerServerFirstPayloads
{
    public static ReadOnlyMemory<byte> Create(ConquerEndpoint endpoint)
    {
        if (endpoint == ConquerRuntimeDefaults.AccountServer)
        {
            return new LegacyAccountPasswordSeedPacket(CreateRandomSeed()).ToFrameBytes();
        }

        if (endpoint == ConquerRuntimeDefaults.WorldServer)
        {
            var payload = new byte[256];
            RandomNumberGenerator.Fill(payload);
            return payload;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    private static int CreateRandomSeed()
    {
        Span<byte> seedBytes = stackalloc byte[sizeof(int)];
        RandomNumberGenerator.Fill(seedBytes);
        return BitConverter.ToInt32(seedBytes);
    }
}
