namespace WolfConquer.Core.Data;

public sealed record CharacterSnapshotOptions(string UsersRoot)
{
    public const string UsersRootEnvironmentVariable = "WOLFCONQUER_USERS_ROOT";

    public static CharacterSnapshotOptions Default { get; } = new(@"data/Users");

    public static CharacterSnapshotOptions FromEnvironment()
    {
        var usersRoot = Environment.GetEnvironmentVariable(UsersRootEnvironmentVariable);
        return string.IsNullOrWhiteSpace(usersRoot)
            ? Default
            : new CharacterSnapshotOptions(usersRoot);
    }
}
