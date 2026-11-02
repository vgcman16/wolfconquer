using WolfConquer.Core.Client;

namespace WolfConquer.Core.Data;

public static partial class WorldDataStartup
{
    public const string Chinese7937XuanbaoInfoPathEnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_INFO_PATH";
    public const string Chinese7937XuanbaoInfoSha256EnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_INFO_SHA256";

    /// <summary>Optional pinned plain-data binding; does not change protocol profiles or execute client code.</summary>
    internal static bool TryBindConfiguredChinese7937Attributes(WorldDataCatalog catalog, out string reason,
        Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        readEnvironment ??= Environment.GetEnvironmentVariable;
        var path = readEnvironment(Chinese7937XuanbaoInfoPathEnvironmentVariable);
        var pinText = readEnvironment(Chinese7937XuanbaoInfoSha256EnvironmentVariable);
        reason = string.Empty;
        if (path is null && pinText is null) return true;
        if (string.IsNullOrWhiteSpace(path) || pinText is null || pinText.Length != 64 || !pinText.All(char.IsAsciiHexDigit))
        { reason = "selected7937AttributePathAndFullPinRequired"; return false; }
        var pin = Convert.FromHexString(pinText);
        if (!Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(path, Chinese7937XuanbaoInfoVersion.Catalog7937,
                pin, out var selected, out var failure))
        { reason = "selected7937AttributeFileRefused:" + failure; return false; }
        return catalog.TryBindChinese7937XuanbaoAttributes(selected!, out reason);
    }
}
