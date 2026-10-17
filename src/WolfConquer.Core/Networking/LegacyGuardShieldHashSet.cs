namespace WolfConquer.Core.Networking;

public sealed record LegacyGuardShieldHashSet(
    string MagicEffect,
    string MagicType,
    string StrRes,
    string ConquerExe)
{
    public static LegacyGuardShieldHashSet Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    public string ToLegacyResponse()
    {
        return $"{MagicEffect} | {MagicType} | {StrRes} | {ConquerExe}";
    }
}
