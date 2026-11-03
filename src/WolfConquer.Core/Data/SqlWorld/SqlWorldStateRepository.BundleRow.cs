using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static async ValueTask<SqlWorldStateBundleReadResult> ReadBundleRecordAsync(MySqlDataReader reader,
        long accountId, CancellationToken token)
    {
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
            return new(SqlWorldStateReadStatus.Missing, null, SqlWorldStateFailure.MissingState);
        byte format = reader.GetByte(2);
        if (format == 1)
            return InvalidBundleRead(Enumerable.Range(5, 4).All(reader.IsDBNull)
                ? SqlWorldStateFailure.StateFormatConflict : SqlWorldStateFailure.InvalidStoredState);
        if (format != 2) return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);

        // Length columns precede payload columns. Inspect every bound before
        // asking the provider for any byte array, even though SQL also guards it.
        var lengths = new long?[6];
        long total = 0;
        for (int index = 0; index < lengths.Length; index++)
        {
            if (reader.IsDBNull(3 + index))
            {
                if (index < 2) return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
                continue;
            }
            long length = reader.GetInt64(3 + index);
            if (length is <= 0 or > SqlWorldStateBundle.MaximumImageBytes)
                return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
            lengths[index] = length;
            total += length;
        }
        if (total > SqlWorldStateBundle.MaximumBundleBytes)
            return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);

        var bytes = new byte[]?[6];
        for (int index = 0; index < bytes.Length; index++)
        {
            if (lengths[index] is not { } length)
            {
                if (!reader.IsDBNull(9 + index)) return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
                continue;
            }
            if (reader.IsDBNull(9 + index)) return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
            bytes[index] = reader.GetFieldValue<byte[]>(9 + index);
            if (bytes[index]!.LongLength != length) return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
        }
        if (!SqlWorldStateImage.TryCreate(accountId, reader.GetUInt32(0), bytes[0]!, bytes[1]!, out var core, out _))
            return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
        var sidecars = new SqlWorldStateSidecarInput[4];
        for (int index = 0; index < sidecars.Length; index++)
        {
            var image = bytes[index + 2];
            sidecars[index] = new((SqlWorldStateSidecarKind)(index + 1),
                image is null ? SqlWorldStateSidecarPresence.Missing : SqlWorldStateSidecarPresence.Present,
                image is null ? ReadOnlyMemory<byte>.Empty : image);
        }
        if (!SqlWorldStateBundle.TryCreate(core, sidecars, out var bundle, out _) ||
            !SqlWorldStateBundleRecord.TryCreate(bundle, reader.GetInt64(1), out var record, out _))
            return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
        return new(SqlWorldStateReadStatus.Found, record, SqlWorldStateFailure.None);
    }

    // The common predicate guards ALL six payload projections. A single empty,
    // oversized or over-budget Present image prevents any blob transfer.
    private const string BundleImageBoundsSql = """
        record_format=2
        AND OCTET_LENGTH(character_image) BETWEEN 1 AND 16777216
        AND OCTET_LENGTH(item_image) BETWEEN 1 AND 16777216
        AND (spell_image IS NULL OR OCTET_LENGTH(spell_image) BETWEEN 1 AND 16777216)
        AND (proficiency_image IS NULL OR OCTET_LENGTH(proficiency_image) BETWEEN 1 AND 16777216)
        AND (quest_image IS NULL OR OCTET_LENGTH(quest_image) BETWEEN 1 AND 16777216)
        AND (combat_gear_image IS NULL OR OCTET_LENGTH(combat_gear_image) BETWEEN 1 AND 16777216)
        AND OCTET_LENGTH(character_image)+OCTET_LENGTH(item_image)
          +COALESCE(OCTET_LENGTH(spell_image),0)+COALESCE(OCTET_LENGTH(proficiency_image),0)
          +COALESCE(OCTET_LENGTH(quest_image),0)+COALESCE(OCTET_LENGTH(combat_gear_image),0) <= 33554432
        """;

    private static readonly string BundleReadSql = $"""
        SELECT character_uid, revision, record_format,
          OCTET_LENGTH(character_image), OCTET_LENGTH(item_image), OCTET_LENGTH(spell_image),
          OCTET_LENGTH(proficiency_image), OCTET_LENGTH(quest_image), OCTET_LENGTH(combat_gear_image),
          CASE WHEN {BundleImageBoundsSql} THEN character_image ELSE NULL END,
          CASE WHEN {BundleImageBoundsSql} THEN item_image ELSE NULL END,
          CASE WHEN {BundleImageBoundsSql} THEN spell_image ELSE NULL END,
          CASE WHEN {BundleImageBoundsSql} THEN proficiency_image ELSE NULL END,
          CASE WHEN {BundleImageBoundsSql} THEN quest_image ELSE NULL END,
          CASE WHEN {BundleImageBoundsSql} THEN combat_gear_image ELSE NULL END
        FROM wolf_sql_world_state WHERE account_id=@account
        """;
}
