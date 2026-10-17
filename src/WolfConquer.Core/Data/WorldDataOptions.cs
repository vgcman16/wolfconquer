namespace WolfConquer.Core.Data;

public sealed record WorldDataOptions(string RootPath)
{
    public const string SeedJsonEnvironmentVariable = "WOLFCONQUER_SEED_JSON";

    public static WorldDataOptions Default { get; } = new(Path.Combine(".setup_work", "seed-json"));

    public static WorldDataOptions FromEnvironment()
    {
        var rootPath = Environment.GetEnvironmentVariable(SeedJsonEnvironmentVariable);
        return string.IsNullOrWhiteSpace(rootPath)
            ? Default
            : new WorldDataOptions(rootPath);
    }
}
