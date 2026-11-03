namespace WolfConquer.Tests;

internal enum OwnedSqlHandoffSchemaFault { MarkerOnly, Version, Engine, PrimaryKey }

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal Task ApplySqlHandoffSchemaFaultAsync(OwnedSqlHandoffSchemaFault fault) =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = fault switch
                {
                    OwnedSqlHandoffSchemaFault.MarkerOnly => "CREATE TABLE wolf_sql_auth_handoff_schema (singleton TINYINT UNSIGNED NOT NULL PRIMARY KEY,version INT NOT NULL) ENGINE=InnoDB",
                    OwnedSqlHandoffSchemaFault.Version => "UPDATE wolf_sql_auth_handoff_schema SET version=99 WHERE singleton=1",
                    OwnedSqlHandoffSchemaFault.Engine => "ALTER TABLE wolf_sql_auth_handoff ENGINE=MyISAM",
                    OwnedSqlHandoffSchemaFault.PrimaryKey => "ALTER TABLE wolf_sql_auth_handoff DROP PRIMARY KEY",
                    _ => throw new ArgumentOutOfRangeException(nameof(fault))
                };
                int changed = await command.ExecuteNonQueryAsync();
                if (fault == OwnedSqlHandoffSchemaFault.Version && changed != 1)
                    throw new InvalidOperationException("Owned handoff marker mutation did not address one row.");
                return true;
            }));
}
