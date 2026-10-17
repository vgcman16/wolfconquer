using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed record AccountServerStartupOptions(ConquerEndpoint? WorldEndpoint, string[] ServerArgs)
{
    public const string WorldEndpointOption = "--world-endpoint";

    public static AccountServerStartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ConquerEndpoint? worldEndpoint = null;
        var serverArgs = new List<string>(args.Count);
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (!string.Equals(argument, WorldEndpointOption, StringComparison.OrdinalIgnoreCase))
            {
                serverArgs.Add(argument);
                continue;
            }

            if (worldEndpoint is not null)
            {
                throw new ArgumentException($"Only one {WorldEndpointOption} can be specified.", nameof(args));
            }

            if (index == args.Count - 1)
            {
                throw new ArgumentException($"Missing value for {WorldEndpointOption}.", nameof(args));
            }

            worldEndpoint = ParseWorldEndpoint(args[++index]);
        }

        return new AccountServerStartupOptions(worldEndpoint, serverArgs.ToArray());
    }

    private static ConquerEndpoint ParseWorldEndpoint(string value)
    {
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
        {
            throw new FormatException($"{WorldEndpointOption} must use address:port format.");
        }

        var address = value[..colonIndex];
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new FormatException($"{WorldEndpointOption} address cannot be empty.");
        }

        if (!int.TryParse(value[(colonIndex + 1)..], out var port) || port is < 0 or > ushort.MaxValue)
        {
            throw new FormatException($"{WorldEndpointOption} port must be between 0 and 65535.");
        }

        return new ConquerEndpoint(ConquerRuntimeDefaults.WorldServer.Role, address, port);
    }
}
