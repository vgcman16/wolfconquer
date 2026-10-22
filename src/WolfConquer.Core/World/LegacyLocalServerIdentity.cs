using System.Globalization;

namespace WolfConquer.Core.World;

// Explicit identity supplied by the local server owner, never inferred from a
// catalog row, client route, copied Player attribute, or a guessed default.
public sealed record LegacyLocalServerIdentity(uint ServerId)
{
    public const string ServerIdEnvironmentVariable = "WOLFCONQUER_LOCAL_SERVER_ID";

    public static LegacyLocalServerIdentity? FromEnvironment(Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        var value = readEnvironmentVariable(ServerIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!uint.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new FormatException("Local server identity configuration is invalid.");
        return new(id);
    }
}
