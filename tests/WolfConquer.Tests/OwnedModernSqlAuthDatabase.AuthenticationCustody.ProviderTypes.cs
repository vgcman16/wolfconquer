using System.Globalization;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal Task UseTinyAuthenticationPermissionColumnsAsync() => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "ALTER TABLE accounts MODIFY BannedID TINYINT(1) NULL, MODIFY Online TINYINT(1) NULL";
            await command.ExecuteNonQueryAsync();
            return true;
        }));

    internal Task<string[]> SnapshotTinyAuthenticationPermissionsAsync() => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            // Explicit SQL integer projections preserve the actual stored 2,
            // rather than letting this observation's provider setting render
            // both 1 and 2 as the same CLR bool. The repository uses raw columns.
            command.CommandText = "SELECT ID,CAST(BannedID AS SIGNED),CAST(Online AS SIGNED) FROM accounts ORDER BY ID";
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
            {
                var rows = new List<string>();
                while (await reader.ReadAsync())
                    rows.Add(string.Join('|', reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                        reader.IsDBNull(1) ? "<NULL>" : reader.GetInt64(1).ToString(CultureInfo.InvariantCulture),
                        reader.IsDBNull(2) ? "<NULL>" : reader.GetInt64(2).ToString(CultureInfo.InvariantCulture)));
                return rows.ToArray();
            });
        }));
}
