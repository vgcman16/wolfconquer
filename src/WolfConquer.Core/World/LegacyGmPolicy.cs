using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace WolfConquer.Core.World;

public enum LegacyGmCapability
{
    InspectSelf = 1,
    MailTestKitSelf = 2
}

public sealed class LegacyGmGrant
{
    internal LegacyGmGrant(uint accountId, uint characterUid, LegacyGmCapability[] capabilities)
    {
        AccountId = accountId;
        CharacterUid = characterUid;
        Capabilities = Array.AsReadOnly(capabilities);
    }

    public uint AccountId { get; }
    public uint CharacterUid { get; }
    public ReadOnlyCollection<LegacyGmCapability> Capabilities { get; }
}

/// <summary>Immutable policy data. Matching is not live actor admission or operation authority.</summary>
public sealed class LegacyGmPolicy
{
    public const int MaximumBytes = 16 * 1024;
    public const int MaximumGrants = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private LegacyGmPolicy(Guid expectedDomain, LegacyGmGrant[] grants)
    {
        ExpectedDomain = expectedDomain;
        Grants = Array.AsReadOnly(grants);
    }

    public Guid ExpectedDomain { get; }
    public ReadOnlyCollection<LegacyGmGrant> Grants { get; }

    public static LegacyGmPolicy Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty || utf8.Length > MaximumBytes)
            throw Invalid("policyLengthInvalid");
        if (utf8.Length >= 3 && utf8[0] == 0xef && utf8[1] == 0xbb && utf8[2] == 0xbf)
            throw Invalid("policyBomInvalid");
        try
        {
            _ = StrictUtf8.GetCharCount(utf8);
            using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            return ParseRoot(document.RootElement);
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            throw new InvalidDataException("policyJsonInvalid", error);
        }
    }

    public bool Allows(Guid domain, uint accountId, uint characterUid, LegacyGmCapability capability)
    {
        if (domain == Guid.Empty || domain != ExpectedDomain || accountId == 0 || characterUid == 0 ||
            capability is not (LegacyGmCapability.InspectSelf or LegacyGmCapability.MailTestKitSelf))
            return false;
        foreach (var grant in Grants)
        {
            if (grant.AccountId == accountId && grant.CharacterUid == characterUid)
                return grant.Capabilities.Contains(capability);
        }
        return false;
    }

    private static LegacyGmPolicy ParseRoot(JsonElement root)
    {
        var fields = Fields(root, "schemaVersion", "expectedDomain", "grants");
        if (fields["schemaVersion"].ValueKind != JsonValueKind.Number ||
            fields["schemaVersion"].GetRawText() != "1")
            throw Invalid("policySchemaInvalid");
        var domainText = Text(fields["expectedDomain"]);
        if (domainText.Length != 36 || !Guid.TryParseExact(domainText, "D", out var domain) || domain == Guid.Empty)
            throw Invalid("policyDomainInvalid");
        var rows = fields["grants"];
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > MaximumGrants)
            throw Invalid("policyGrantsInvalid");

        var grants = new LegacyGmGrant[rows.GetArrayLength()];
        var identities = new HashSet<(uint Account, uint Character)>();
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var grant = ParseGrant(row);
            if (!identities.Add((grant.AccountId, grant.CharacterUid)))
                throw Invalid("policyDuplicateGrant");
            grants[index++] = grant;
        }
        return new LegacyGmPolicy(domain, grants);
    }

    private static LegacyGmGrant ParseGrant(JsonElement row)
    {
        var fields = Fields(row, "accountId", "characterUid", "capabilities");
        var accountId = PositiveId(fields["accountId"]);
        var characterUid = PositiveId(fields["characterUid"]);
        var values = fields["capabilities"];
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() is < 1 or > 2)
            throw Invalid("policyCapabilitiesInvalid");

        var capabilities = new LegacyGmCapability[values.GetArrayLength()];
        var seen = new HashSet<LegacyGmCapability>();
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var capability = Text(value) switch
            {
                "inspect.self" => LegacyGmCapability.InspectSelf,
                "mail.testkit.self" => LegacyGmCapability.MailTestKitSelf,
                _ => throw Invalid("policyCapabilityUnknown")
            };
            if (!seen.Add(capability)) throw Invalid("policyCapabilityDuplicate");
            capabilities[index++] = capability;
        }
        return new LegacyGmGrant(accountId, characterUid, capabilities);
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("policyObjectRequired");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal)) throw Invalid("policyPropertyUnknown");
            if (!fields.TryAdd(property.Name, property.Value)) throw Invalid("policyPropertyDuplicate");
        }
        if (fields.Count != expected.Length) throw Invalid("policyPropertyMissing");
        return fields;
    }

    private static uint PositiveId(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var id) || id == 0)
            throw Invalid("policyIdentityInvalid");
        foreach (var digit in value.GetRawText())
            if (digit is < '0' or > '9') throw Invalid("policyIdentityInvalid");
        return id;
    }

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()! : throw Invalid("policyStringRequired");

    private static InvalidDataException Invalid(string reason) => new(reason);
}
