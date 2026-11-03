using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // DDL is deliberately explicit, not a constructor or ordinary-read side effect.
    public async ValueTask InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        await UseResourceAsync(CreateConnection(), async connection =>
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            int present = await CountOwnedSchemaTablesAsync(connection, cancellationToken).ConfigureAwait(false);
            if (present == 4)
            {
                await VerifySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                return;
            }
            if (present != 0) throw new InvalidOperationException("sqlWorldSchemaPartial");
            foreach (string ddl in SchemaStatements)
                await UseResourceAsync(connection.CreateCommand(), async command =>
                {
                    command.CommandText = ddl;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            await VerifySchemaShapeAsync(connection, cancellationToken).ConfigureAwait(false);
            await UseResourceAsync(connection.CreateCommand(), async seed =>
            {
                seed.CommandText = "INSERT INTO wolf_sql_world_schema (singleton, version) VALUES (1, 1) ON DUPLICATE KEY UPDATE singleton = singleton";
                await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await VerifySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static readonly string[] SchemaStatements =
    [
        "CREATE TABLE IF NOT EXISTS wolf_sql_world_schema (singleton TINYINT NOT NULL PRIMARY KEY, version INT NOT NULL) ENGINE=InnoDB",
        "CREATE TABLE IF NOT EXISTS wolf_sql_world_state (account_id BIGINT NOT NULL PRIMARY KEY, character_uid INT UNSIGNED NOT NULL, revision BIGINT NOT NULL, character_image LONGBLOB NOT NULL, item_image LONGBLOB NOT NULL, UNIQUE KEY ux_world_character (character_uid)) ENGINE=InnoDB",
        "CREATE TABLE IF NOT EXISTS wolf_sql_world_item_uid (item_uid INT UNSIGNED NOT NULL PRIMARY KEY, account_id BIGINT NOT NULL, KEY ix_world_item_account (account_id), CONSTRAINT fk_world_item_account FOREIGN KEY (account_id) REFERENCES wolf_sql_world_state(account_id) ON UPDATE RESTRICT ON DELETE RESTRICT) ENGINE=InnoDB",
        "CREATE TABLE IF NOT EXISTS wolf_sql_world_operation (operation_id BINARY(16) NOT NULL PRIMARY KEY, descriptor_hash BINARY(32) NOT NULL, descriptor LONGBLOB NOT NULL) ENGINE=InnoDB"
    ];

    private static async ValueTask VerifySchemaAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        // Shape checks are required before this owner may use already-existing tables.
        // This method must validate engine, full column/index/FK shapes and version;
        // the schema contract is authored in the companion partial.
        await VerifySchemaContractAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
