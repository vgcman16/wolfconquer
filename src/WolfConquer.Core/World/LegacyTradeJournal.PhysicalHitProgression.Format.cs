using System.Buffers;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Isolated DATA codec. Neither a parsed object nor its bytes grant a hit,
    // a journal producer, persistence permission, or recovery custody.
    internal static byte[] SerializePhysicalHitProgressionData(LegacyPhysicalHitProgressionOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var actorItems = operation.ActorItems;
        var death = operation.Death;
        RequireHitProgressionWriteBudget(actorItems, death);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("ActorAccountId", operation.ActorAccountId);
            writer.WriteNumber("ActorUid", operation.ActorUid);
            writer.WriteNumber("TargetAccountId", operation.TargetAccountId);
            writer.WriteNumber("TargetUid", operation.TargetUid);
            writer.WriteNumber("RawExperience", operation.RawExperience);
            writer.WriteNumber("AppliedDamage", operation.AppliedDamage);
            writer.WriteBoolean("ActorAlternate", operation.ActorAlternate);
            writer.WriteBase64String("ActorItems", actorItems);
            writer.WritePropertyName("Death");
            if (death is null) writer.WriteNullValue();
            else JsonSerializer.Serialize(writer, death);
            writer.WriteEndObject();
        }
        if (output.WrittenCount > MaxJournalBytes) throw InvalidHitProgressionJson();
        PreflightHitProgressionData(output.WrittenSpan);
        return output.WrittenSpan.ToArray();
    }

    internal static LegacyPhysicalHitProgressionOperation ParsePhysicalHitProgressionData(ReadOnlyMemory<byte> bytes)
    {
        // Scan encoded token sizes and cumulative clone budget BEFORE either
        // DOM construction or any base64 decoding and reference cloning.
        PreflightHitProgressionData(bytes.Span);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        var fields = GetProperties(document.RootElement, "ActorAccountId", "ActorUid", "TargetAccountId", "TargetUid",
            "RawExperience", "AppliedDamage", "ActorAlternate", "ActorItems", "Death");
        uint actorAccount = ReadHitProgressionUInt32(fields["ActorAccountId"]);
        uint actor = ReadHitProgressionUInt32(fields["ActorUid"]);
        uint targetAccount = ReadHitProgressionUInt32(fields["TargetAccountId"]);
        uint target = ReadHitProgressionUInt32(fields["TargetUid"]);
        uint raw = ReadHitProgressionUInt32(fields["RawExperience"]);
        uint applied = ReadHitProgressionUInt32(fields["AppliedDamage"]);
        try
        {
            var death = fields["Death"].ValueKind == JsonValueKind.Null ? null : ReadPhysicalDeath(fields["Death"]);
            return new(actorAccount, actor, targetAccount, target, raw, applied,
                ReadPhysicalDeathBoolean(fields["ActorAlternate"]), ReadBytes(fields["ActorItems"], allowNull: false)!, death);
        }
        catch (Exception error) when (error is InvalidDataException or FormatException or OverflowException or ArgumentException)
        { throw new JsonException("Invalid physical hit progression24 DATA.", error); }
    }

    private static uint ReadHitProgressionUInt32(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint number)) throw InvalidHitProgressionJson();
        var raw = value.GetRawText();
        if (raw.Length is 0 or > 10 || raw.Any(character => character is < '0' or > '9')) throw InvalidHitProgressionJson();
        return number;
    }

    private static void PreflightHitProgressionData(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is <= 0 or > MaxJournalBytes) throw InvalidHitProgressionJson();
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 8 });
        long budget = 4096; bool image = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                image = reader.ValueTextEquals("ActorItems"u8) || reader.ValueTextEquals("ItemsBefore"u8);
                AddHitProgressionBudget(ref budget, 64L + 6L * reader.ValueSpan.Length);
                continue;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                long length = reader.ValueSpan.Length;
                if (image && length > ((long)MaxTargetBytes + 2) / 3 * 4) throw InvalidHitProgressionJson();
                AddHitProgressionBudget(ref budget, 64 + (image ? 3 * length : 6 * length));
            }
            else AddHitProgressionBudget(ref budget, 64);
            image = false;
        }
    }

    private static void RequireHitProgressionWriteBudget(byte[] actorItems, LegacyPhysicalDeathOperation? death)
    {
        long budget = 4096;
        AddHitProgressionImageBudget(ref budget, actorItems);
        if (death is null) return;
        AddHitProgressionImageBudget(ref budget, death.ItemsBefore);
        foreach (var row in death.RankingRows)
        {
            AddHitProgressionBudget(ref budget, 1024);
            foreach (string field in row.Fields) AddHitProgressionStringBudget(ref budget, field);
        }
        foreach (var row in death.DerivedSpells) AddHitProgressionBudget(ref budget, 2048);
        foreach (var row in death.GrantedRows)
        {
            AddHitProgressionBudget(ref budget, 2048);
            AddHitProgressionStringBudget(ref budget, row.Name);
            foreach (string field in row.Fields) AddHitProgressionStringBudget(ref budget, field);
        }
    }

    private static void AddHitProgressionImageBudget(ref long budget, byte[] image)
    {
        if (image.Length is 0 or > MaxTargetBytes) throw InvalidHitProgressionJson();
        long encoded = ((long)image.Length + 2) / 3 * 4;
        AddHitProgressionBudget(ref budget, 64 + 3 * encoded);
    }
    private static void AddHitProgressionStringBudget(ref long budget, string value) =>
        AddHitProgressionBudget(ref budget, 256L + 6L * value.Length);
    private static void AddHitProgressionBudget(ref long budget, long amount)
    {
        budget = checked(budget + amount);
        if (budget > MaxJournalBytes) throw InvalidHitProgressionJson();
    }
    private static JsonException InvalidHitProgressionJson() => new("Invalid physical hit progression24 DATA field or bound.");
}
