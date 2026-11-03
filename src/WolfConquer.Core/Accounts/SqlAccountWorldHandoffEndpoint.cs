using System.Text;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

internal static class SqlAccountWorldHandoffEndpoint
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] Validate(ConquerEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (string.IsNullOrWhiteSpace(endpoint.Address) || endpoint.Port is < 1 or > 65535)
            throw new ArgumentException("Invalid world endpoint.", nameof(endpoint));
        byte[] address;
        try { address = StrictUtf8.GetBytes(endpoint.Address); }
        catch (EncoderFallbackException error) { throw new ArgumentException("Invalid world endpoint.", nameof(endpoint), error); }
        if (address.Length is 0 or > 1024)
            throw new ArgumentException("Invalid world endpoint.", nameof(endpoint));
        return address;
    }
}