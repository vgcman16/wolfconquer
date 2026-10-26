using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial record LegacyOwnedAdmissionOptions
{
    private static readonly string[] ConfigurationKeys = ["Version", "ExpectedDomain", "UsersRoot", "ItemsRoot",
        "SpellsRoot", "ProficienciesRoot", "QuestsRoot", "CombatGearRoot", "MailboxRoot", "RedeemFile",
        "ClaimFile", "HistoricalHighWater", "HistoricalEvidence"];

    public static bool TryLoadFromEnvironment(out LegacyOwnedAdmissionOptions? options, out string reason)
    {
        var path = Environment.GetEnvironmentVariable(ConfigurationEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(path)) return TryReadFile(path, out options, out reason);
        options = null; reason = string.Empty; return true;
    }

    public static bool TryReadFile(string path, out LegacyOwnedAdmissionOptions? options, out string reason)
    {
        options = null; reason = "ownedAdmissionConfigurationInvalid";
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
                !CharacterSnapshotFileBaseline.TryValidateSafePath(path, "ownedAdmissionConfiguration", out reason)) return false;
            reason = "ownedAdmissionConfigurationInvalid";
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is < 1 or > MaximumConfigurationBytes) return false;
            var bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
            if (input.ReadByte() != -1 || input.Length != bytes.Length) return false;
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!ConfigurationKeys.Contains(property.Name, StringComparer.Ordinal) || !keys.Add(property.Name)) return false;
            if (keys.Count != ConfigurationKeys.Length || !root.GetProperty("Version").TryGetInt32(out var version) || version != 1 ||
                !Guid.TryParseExact(Read(root, "ExpectedDomain"), "N", out var domain) ||
                !root.GetProperty("HistoricalHighWater").TryGetUInt32(out var floor)) return false;
            var candidate = new LegacyOwnedAdmissionOptions(domain, Read(root, "UsersRoot"), Read(root, "ItemsRoot"),
                Read(root, "SpellsRoot"), Read(root, "ProficienciesRoot"), Read(root, "QuestsRoot"),
                Read(root, "CombatGearRoot"), Read(root, "MailboxRoot"), Read(root, "RedeemFile"),
                Read(root, "ClaimFile"), floor, Read(root, "HistoricalEvidence"));
            if (!candidate.TryNormalize(out var normalized, out reason)) return false;
            options = normalized; reason = string.Empty; return true;
        }
        catch (Exception error) when (IsConfigurationFailure(error))
        { reason = "ownedAdmissionConfigurationUnreadable:" + error.GetType().Name; return false; }
    }

    private static string Read(JsonElement root, string key) => root.GetProperty(key).ValueKind == JsonValueKind.String
        ? root.GetProperty(key).GetString()! : throw new FormatException("A configured string is required.");
}
