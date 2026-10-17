namespace WolfConquer.Core.Data;

public static class CharacterSnapshotStartup
{
    public static async Task<CharacterSnapshotLoader?> TryCreateLoaderAsync(TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var options = CharacterSnapshotOptions.FromEnvironment();
        var usersRoot = Path.GetFullPath(options.UsersRoot);
        await log.WriteLineAsync($"Character snapshot users root: {usersRoot}").ConfigureAwait(false);

        if (!Directory.Exists(usersRoot))
        {
            await log.WriteLineAsync(
                    $"Character snapshots not loaded; Users directory is missing. Set {CharacterSnapshotOptions.UsersRootEnvironmentVariable}.")
                .ConfigureAwait(false);
            return null;
        }

        var loader = new CharacterSnapshotLoader(usersRoot);
        await log.WriteLineAsync($"Character snapshots available: {loader.CountSnapshotFiles()}").ConfigureAwait(false);
        await WriteSidecarSnapshotStatusAsync(
                log,
                "Player item snapshots",
                loader.PlayerItemsRoot,
                "*.bin")
            .ConfigureAwait(false);
        await WriteSidecarSnapshotStatusAsync(
                log,
                "Player spell snapshots",
                loader.PlayerSpellsRoot,
                "*.bin")
            .ConfigureAwait(false);
        await WriteSidecarSnapshotStatusAsync(
                log,
                "Player proficiency snapshots",
                loader.PlayerProficienciesRoot,
                "*.bin")
            .ConfigureAwait(false);
        await WriteSidecarSnapshotStatusAsync(
                log,
                "Player combat gear snapshots",
                loader.PlayersCombatGearRoot,
                "*.ini")
            .ConfigureAwait(false);
        await WriteSidecarSnapshotStatusAsync(
                log,
                "Player quest snapshots",
                loader.PlayerQuestsRoot,
                "*.bin")
            .ConfigureAwait(false);
        return loader;
    }

    private static async Task WriteSidecarSnapshotStatusAsync(
        TextWriter log,
        string label,
        string? root,
        string pattern)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            await log.WriteLineAsync($"{label} root: not configured").ConfigureAwait(false);
            await log.WriteLineAsync($"{label} available: 0").ConfigureAwait(false);
            return;
        }

        var fullRoot = Path.GetFullPath(root);
        await log.WriteLineAsync($"{label} root: {fullRoot}").ConfigureAwait(false);
        var count = Directory.Exists(fullRoot)
            ? Directory.EnumerateFiles(fullRoot, pattern).Count()
            : 0;
        await log.WriteLineAsync($"{label} available: {count}").ConfigureAwait(false);
        if (count == 0)
        {
            await log.WriteLineAsync($"{label} diagnostic: no legacy {pattern} files found.").ConfigureAwait(false);
        }
    }
}
