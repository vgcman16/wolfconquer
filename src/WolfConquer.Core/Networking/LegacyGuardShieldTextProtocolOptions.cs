namespace WolfConquer.Core.Networking;

public sealed record LegacyGuardShieldTextProtocolOptions(
    LegacyGuardShieldHashSet Hashes,
    Action<LegacyGuardShieldUsageReport>? ReportUsage = null,
    LegacyGuardShieldBanPolicy? BanPolicy = null,
    string? SourcePath = null,
    Action<LegacyGuardShieldCommandReport>? ReportCommand = null)
{
    public const string IniPathEnvironmentVariable = "WOLFCONQUER_GUARDSHIELD_INI";
    public const string MagicEffectHashEnvironmentVariable = "WOLFCONQUER_GUARDSHIELD_MAGIC_EFFECT_HASH";
    public const string MagicTypeHashEnvironmentVariable = "WOLFCONQUER_GUARDSHIELD_MAGIC_TYPE_HASH";
    public const string StrResHashEnvironmentVariable = "WOLFCONQUER_GUARDSHIELD_STR_RES_HASH";
    public const string ConquerExeHashEnvironmentVariable = "WOLFCONQUER_GUARDSHIELD_CONQUER_EXE_HASH";
    public const string BundledGuardShieldIniPath = @"config/GuardShield.ini";

    public static LegacyGuardShieldTextProtocolOptions Default { get; } =
        new(LegacyGuardShieldHashSet.Empty);

    public LegacyGuardShieldBanPolicy EffectiveBanPolicy => BanPolicy ?? LegacyGuardShieldBanPolicy.Empty;

    public static LegacyGuardShieldTextProtocolOptions FromEnvironment(
        Action<LegacyGuardShieldUsageReport>? reportUsage = null,
        Action<LegacyGuardShieldCommandReport>? reportCommand = null)
    {
        var hashes = LegacyGuardShieldHashSet.Empty;
        var banPolicy = LegacyGuardShieldBanPolicy.Empty;
        string? sourcePath = null;
        var iniPath = Environment.GetEnvironmentVariable(IniPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(iniPath) && File.Exists(BundledGuardShieldIniPath))
        {
            iniPath = BundledGuardShieldIniPath;
        }

        if (!string.IsNullOrWhiteSpace(iniPath) && TryLoadHashSetFromIni(iniPath, out var iniHashes))
        {
            hashes = iniHashes;
            sourcePath = Path.GetFullPath(iniPath);
        }

        if (!string.IsNullOrWhiteSpace(iniPath) && TryLoadBanPolicyFromIni(iniPath, out var iniBanPolicy))
        {
            banPolicy = iniBanPolicy;
            sourcePath = Path.GetFullPath(iniPath);
        }

        hashes = hashes with
        {
            MagicEffect = GetEnvironmentOverride(MagicEffectHashEnvironmentVariable) ?? hashes.MagicEffect,
            MagicType = GetEnvironmentOverride(MagicTypeHashEnvironmentVariable) ?? hashes.MagicType,
            StrRes = GetEnvironmentOverride(StrResHashEnvironmentVariable) ?? hashes.StrRes,
            ConquerExe = GetEnvironmentOverride(ConquerExeHashEnvironmentVariable) ?? hashes.ConquerExe,
        };

        return new LegacyGuardShieldTextProtocolOptions(hashes, reportUsage, banPolicy, sourcePath, reportCommand);
    }

    public static bool TryLoadHashSetFromIni(string path, out LegacyGuardShieldHashSet hashes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        hashes = LegacyGuardShieldHashSet.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            if (!string.Equals(section, "HashSystem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            values[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..].Trim();
        }

        hashes = new LegacyGuardShieldHashSet(
            values.GetValueOrDefault("MagicEffectHash") ?? string.Empty,
            values.GetValueOrDefault("MagicTypeHash") ?? string.Empty,
            values.GetValueOrDefault("StrResHash") ?? string.Empty,
            values.GetValueOrDefault("ConquerEXEHash") ?? string.Empty);
        return true;
    }

    public static bool TryLoadBanPolicyFromIni(string path, out LegacyGuardShieldBanPolicy banPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        banPolicy = LegacyGuardShieldBanPolicy.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            if (!string.Equals(section, "BanHourSystem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            values[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..].Trim();
        }

        banPolicy = new LegacyGuardShieldBanPolicy(
            ParseLegacyUInt16(values.GetValueOrDefault("UseSpeed")),
            ParseLegacyUInt16(values.GetValueOrDefault("OpenProgram")),
            ParseLegacyUInt16(values.GetValueOrDefault("AutoClicker")));
        return true;
    }

    private static uint ParseLegacyUInt16(string? value)
    {
        return ushort.TryParse(value, out var parsed) ? parsed : 0u;
    }

    private static string? GetEnvironmentOverride(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
