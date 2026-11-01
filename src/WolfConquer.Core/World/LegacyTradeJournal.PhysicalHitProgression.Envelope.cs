using System.Buffers;
using System.Text;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void RequireHitProgressionEnvelopeBudget(IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyPhysicalHitProgressionOperation operation, HitProgressionJournalDomain? domain)
    {
        long budget = 16384;
        foreach (var change in changes)
        {
            if (change is null || change.After is null) throw new JsonException("Missing hit progression image.");
            AddHitProgressionImageBudget(ref budget, change.After);
            if (change.Before is { } before) AddHitProgressionImageBudget(ref budget, before);
        }
        AddHitProgressionImageBudget(ref budget, operation.ActorItems);
        if (operation.Death is { } death)
        {
            AddHitProgressionImageBudget(ref budget, death.ItemsBefore);
            foreach (var row in death.RankingRows)
            {
                AddHitProgressionBudget(ref budget, 1024);
                foreach (var field in row.Fields) AddHitProgressionStringBudget(ref budget, field);
            }
            foreach (var row in death.DerivedSpells) AddHitProgressionBudget(ref budget, 2048);
            foreach (var row in death.GrantedRows)
            {
                AddHitProgressionBudget(ref budget, 2048); AddHitProgressionStringBudget(ref budget, row.Name);
                foreach (var field in row.Fields) AddHitProgressionStringBudget(ref budget, field);
            }
        }
        if (domain is null) throw new JsonException("Missing hit progression domain.");
        foreach (var root in new[] { domain.Users, domain.Items, domain.Spells, domain.Proficiencies })
        {
            if (root.Length is 0 or > 32768) throw new JsonException("Invalid hit progression root length.");
            AddHitProgressionStringBudget(ref budget, root);
        }
    }

    private static byte[] SerializeHitProgressionRecord(JournalRecord record)
    {
        RequireHitProgressionEnvelopeBudget(record.Changes, record.PhysicalHitProgression!, record.HitProgressionDomain);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject(); writer.WriteNumber("Version", 24); writer.WriteString("TransactionId", record.TransactionId);
            writer.WritePropertyName("Changes"); JsonSerializer.Serialize(writer, record.Changes);
            writer.WritePropertyName("HitProgressionDomain"); JsonSerializer.Serialize(writer, record.HitProgressionDomain);
            writer.WritePropertyName("PhysicalHitProgression");
            writer.WriteRawValue(SerializePhysicalHitProgressionData(record.PhysicalHitProgression!));
            writer.WriteEndObject();
        }
        if (output.WrittenCount > MaxJournalBytes) throw new JsonException("Hit progression envelope is too large.");
        PreflightHitProgressionEnvelope(output.WrittenSpan);
        return output.WrittenSpan.ToArray();
    }

    // No DOM, base64 decode or image clone occurs until the aggregate encoded
    // envelope plus conservative decoded/cloned references fits the same cap.
    private static void PreflightHitProgressionEnvelope(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 8 });
        bool versionField = false, isHitProgression = false, image = false;
        long budget = 16384;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                versionField = reader.CurrentDepth == 1 && reader.ValueTextEquals("Version"u8);
                image = reader.ValueTextEquals("Before"u8) || reader.ValueTextEquals("After"u8) ||
                    reader.ValueTextEquals("ActorItems"u8) || reader.ValueTextEquals("ItemsBefore"u8);
                budget = checked(budget + 64L + 6L * reader.ValueSpan.Length); continue;
            }
            if (versionField && reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int version) && version == 24)
                isHitProgression = true;
            versionField = false;
            if (reader.TokenType == JsonTokenType.String)
            {
                long length = reader.ValueSpan.Length;
                budget = checked(budget + 64 + (image ? 3 * length : 6 * length));
            }
            else budget = checked(budget + 64);
            image = false;
        }
        if (isHitProgression && budget > MaxJournalBytes) throw new JsonException("Hit progression aggregate envelope exceeds bound.");
    }

    private static LegacyPhysicalHitProgressionOperation ReadHitProgressionOperation(JsonElement element) =>
        ParsePhysicalHitProgressionData(Encoding.UTF8.GetBytes(element.GetRawText()));
}
