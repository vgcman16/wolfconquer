namespace WolfConquer.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ModernSqlAuthFixtureFactAttribute : FactAttribute
{
    internal const string EnabledVariable = "WOLF_SQL_AUTH_FIXTURE_ENABLED";
    internal const string ConnectionVariable = "WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION";

    public ModernSqlAuthFixtureFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            Skip = "Requires explicitly enabled, owned loopback SQL fixture configuration; no database was contacted.";
    }
}
