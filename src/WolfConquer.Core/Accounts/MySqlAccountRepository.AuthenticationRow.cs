using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class MySqlAccountRepository
{
    private static AccountRecord ReadAuthenticationRow(MySqlDataReader reader)
    {
        long id = ReadAuthenticationInteger(reader.GetValue(0));
        if (id <= 0) throw new InvalidDataException("sqlAccountAuthenticationStoredIdentityInvalid");
        if (reader.GetValue(1) is not string username || string.IsNullOrWhiteSpace(username) ||
            reader.GetValue(2) is not string password)
            throw new InvalidDataException("sqlAccountAuthenticationStoredCredentialsInvalid");
        long entityId = reader.IsDBNull(3) ? 0 : ReadAuthenticationInteger(reader.GetValue(3));
        if (entityId is < 0 or > uint.MaxValue)
            throw new InvalidDataException("sqlAccountAuthenticationStoredEntityInvalid");
        long bannedId = ReadAuthenticationInteger(reader.GetValue(4));
        bool online = ReadAuthenticationOnline(reader.GetValue(5));
        return new(id, username, password, entityId, bannedId, online);
    }

    // Required numeric fields accept only actual provider primitive integers.
    // No bool/decimal/string conversion, truncation or permission default exists.
    private static long ReadAuthenticationInteger(object? value) => value switch
    {
        sbyte number => number,
        byte number => number,
        short number => number,
        ushort number => number,
        int number => number,
        uint number => number,
        long number => number,
        ulong number when number <= long.MaxValue => (long)number,
        _ => throw new InvalidDataException("sqlAccountAuthenticationStoredIntegerInvalid"),
    };

    // Online has no invented binary enum: every present nonzero integer remains
    // online, including unsigned values that need not fit the AccountRecord ID.
    private static bool ReadAuthenticationOnline(object? value) => value switch
    {
        sbyte number => number != 0,
        byte number => number != 0,
        short number => number != 0,
        ushort number => number != 0,
        int number => number != 0,
        uint number => number != 0,
        long number => number != 0,
        ulong number => number != 0,
        _ => throw new InvalidDataException("sqlAccountAuthenticationStoredOnlineInvalid"),
    };
}
