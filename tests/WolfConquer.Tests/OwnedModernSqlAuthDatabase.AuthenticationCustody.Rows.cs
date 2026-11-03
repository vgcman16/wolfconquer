namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal enum AuthenticationAccountColumn { Id, Username, Password, EntityId, BannedId, Online }

    internal Task SetAuthenticationAccountColumnAsync(long accountId, AuthenticationAccountColumn column, object? value) =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                string name = column switch
                {
                    AuthenticationAccountColumn.Id => "ID",
                    AuthenticationAccountColumn.Username => "Username",
                    AuthenticationAccountColumn.Password => "Password",
                    AuthenticationAccountColumn.EntityId => "EntityID",
                    AuthenticationAccountColumn.BannedId => "BannedID",
                    AuthenticationAccountColumn.Online => "Online",
                    _ => throw new ArgumentOutOfRangeException(nameof(column))
                };
                command.CommandText = "UPDATE accounts SET `" + name + "`=@value WHERE ID=@id";
                command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", accountId);
                if (await command.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("The owned account mutation did not address exactly one row.");
                return true;
            }));

    internal Task AddAuthenticationDuplicateAsync(long accountId) => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = """
                INSERT INTO accounts (ID,Username,Password,EntityID,BannedID,Online,Email)
                VALUES (@id,@username,@password,@entity,2,0,@email)
                """;
            command.Parameters.AddWithValue("@id", accountId);
            command.Parameters.AddWithValue("@username", FirstUsername);
            command.Parameters.AddWithValue("@password", "different_owned_fixture_password");
            command.Parameters.AddWithValue("@entity", accountId + 1000);
            command.Parameters.AddWithValue("@email", "duplicate@fixture.invalid");
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("The owned duplicate account was not inserted.");
            return true;
        }));

    internal Task AllowNullAuthenticationCredentialsAsync() => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "ALTER TABLE accounts MODIFY Username VARCHAR(64) NULL, MODIFY Password VARCHAR(128) NULL";
            await command.ExecuteNonQueryAsync();
            return true;
        }));

    internal Task MakeAuthenticationNumericColumnTextAsync(AuthenticationAccountColumn column) =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                string declaration = column switch
                {
                    AuthenticationAccountColumn.Id => "ID VARCHAR(64) NOT NULL",
                    AuthenticationAccountColumn.EntityId => "EntityID VARCHAR(64) NULL",
                    AuthenticationAccountColumn.BannedId => "BannedID VARCHAR(64) NULL",
                    AuthenticationAccountColumn.Online => "Online VARCHAR(64) NULL",
                    _ => throw new ArgumentOutOfRangeException(nameof(column))
                };
                command.CommandText = "ALTER TABLE accounts MODIFY " + declaration;
                await command.ExecuteNonQueryAsync();
                return true;
            }));

    internal Task RemoveOwnedAuthenticationAccountAsync(long accountId) => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "DELETE FROM accounts WHERE ID=@id";
            command.Parameters.AddWithValue("@id", accountId);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("The owned account deletion did not address exactly one row.");
            return true;
        }));

    internal Task RemoveOwnedAuthenticationTableAsync() => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "DROP TABLE accounts";
            await command.ExecuteNonQueryAsync();
            return true;
        }));
}
