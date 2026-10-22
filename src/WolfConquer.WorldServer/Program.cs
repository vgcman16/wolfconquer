using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

var endpoint = ConquerRuntimeDefaults.WorldServer;
var protocolProfile = ConquerProtocolProfileConfiguration.FromEnvironment();
LegacyLocalServerIdentity? localServerIdentity;
try
{
    localServerIdentity = LegacyLocalServerIdentity.FromEnvironment();
}
catch (FormatException)
{
    Console.Error.WriteLine(
        "Invalid WOLFCONQUER_LOCAL_SERVER_ID; expected an unsigned 32-bit decimal server ID.");
    return 1;
}
RuntimeConsoleLog.ConfigureFromEnvironment(endpoint.Role);
Console.Out.WriteLine($"Protocol profile: {protocolProfile}");
var worldData = await WorldDataStartup.TryLoadAsync(Console.Out);
var listenRequested = args.Any(argument =>
    string.Equals(argument, "--listen", StringComparison.OrdinalIgnoreCase));
if (listenRequested && worldData is null)
{
    Console.Error.WriteLine(
        "WorldServer cannot start in --listen mode because world data failed to load. " +
        $"Export the world seed data or set {WorldDataOptions.SeedJsonEnvironmentVariable} to a valid seed root.");
    return 1;
}

var snapshotLoader = await CharacterSnapshotStartup.TryCreateLoaderAsync(Console.Out);
var handoffService = await AccountHandoffStartup.TryCreateServiceAsync(Console.Out);
var accountEmailMySql = Environment.GetEnvironmentVariable("WOLFCONQUER_ACCOUNT_EMAIL_MYSQL");
if (string.IsNullOrWhiteSpace(accountEmailMySql))
{
    accountEmailMySql = Environment.GetEnvironmentVariable("WOLFCONQUER_ACCOUNT_MYSQL");
}

IAccountEmailVerifier? accountEmailVerifier = string.IsNullOrWhiteSpace(accountEmailMySql)
    ? null
    : new MySqlAccountRepository(accountEmailMySql);
Console.Out.WriteLine(accountEmailVerifier is null
    ? "Account email verifier: unavailable; Security Anima email checks will fail closed."
    : "Account email verifier: MySQL authority configured.");
var runtimeUpdateLoop = LegacyRuntimeUpdateLoopOptions.EnabledDefault with
{
    LocalServerIdentity = localServerIdentity,
};
Console.Out.WriteLine(localServerIdentity is { } identity
    ? $"Native local server identity configured: {identity.ServerId}."
    : "Native local server identity unavailable; own-server-dependent native behavior remains held.");
var clientCompatibility = LegacyClientCompatibilityOptions.FromEnvironment();
var worldEntryStartupProbe = LegacyWorldEntryStartupProbeOptions.FromEnvironment();
if (clientCompatibility.EnableNewerClientRevelatorStartupEffect)
{
    Console.Out.WriteLine("Newer-client Revelator startup compatibility enabled.");
}
if (clientCompatibility.EnableNewerClientOverwhelmStartupEffect)
{
    Console.Out.WriteLine("Newer-client Overwhelm startup compatibility enabled.");
}
if (clientCompatibility.EnableNewerClientCatalogItems)
{
    Console.Out.WriteLine("Newer-client catalog item compatibility enabled.");
}
if (clientCompatibility.EnableNewerClientAppearanceMapping)
{
    Console.Out.WriteLine("Newer-client appearance compatibility enabled.");
}
if (clientCompatibility.DisabledLegacyCombatGearMonsterHitPassiveSpellIds.Count > 0)
{
    Console.Out.WriteLine(
        "Legacy CombatGear monster-hit passive spell blocklist enabled for client compatibility: " +
        $"{string.Join(",", clientCompatibility.DisabledLegacyCombatGearMonsterHitPassiveSpellIds.Order())}.");
}
if (clientCompatibility.DisableLegacyProactiveMonsterAttacks)
{
    Console.Out.WriteLine("Legacy proactive monster attacks disabled for client compatibility.");
}
if (clientCompatibility.DisableLegacyCombatGearMonsterHitPassives)
{
    Console.Out.WriteLine("Legacy CombatGear monster-hit passives disabled for client compatibility.");
}
if (clientCompatibility.DisableLegacyWeaponProcMonsterAttacks)
{
    Console.Out.WriteLine("Legacy weapon-proc monster attacks disabled for client compatibility.");
}
if (!worldEntryStartupProbe.IsDefault)
{
    Console.Out.WriteLine($"World-entry startup prefix probe enabled: {worldEntryStartupProbe.PrefixMode}.");
}

var runtimeClockStart = Environment.GetEnvironmentVariable("WOLFCONQUER_RUNTIME_CLOCK_START_UTC");
if (!string.IsNullOrWhiteSpace(runtimeClockStart))
{
    if (DateTimeOffset.TryParse(
            runtimeClockStart,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var anchoredRuntimeStart))
    {
        runtimeUpdateLoop = runtimeUpdateLoop with
        {
            TimeProvider = new AnchoredRuntimeTimeProvider(anchoredRuntimeStart),
        };
        Console.Out.WriteLine($"Runtime update clock anchor: {anchoredRuntimeStart:O}");
    }
    else
    {
        Console.Out.WriteLine(
            "Ignoring invalid WOLFCONQUER_RUNTIME_CLOCK_START_UTC value; expected an ISO-8601 UTC timestamp.");
    }
}

var handler = new WorldServerFrameHandler(
    snapshotLoader,
    handoffService,
    worldData,
    runtimeUpdateLoop,
    Console.Out,
    clientCompatibility,
    worldEntryStartupProbe,
    accountEmailVerifier,
    protocolProfile: protocolProfile);

return await ConquerServerRunner.RunAsync(endpoint, args, handler.HandleFrameAsync);
