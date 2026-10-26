using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Explicit operator configuration, never authority inferred from a discovered ledger.
public sealed partial record LegacyOwnedAdmissionOptions(Guid ExpectedDomain, string UsersRoot,
    string ItemsRoot, string SpellsRoot, string ProficienciesRoot, string QuestsRoot,
    string CombatGearRoot, string MailboxRoot, string RedeemFile, string ClaimFile,
    uint HistoricalHighWater, string HistoricalEvidence)
{
    public const string ConfigurationEnvironmentVariable = "WOLFCONQUER_OWNED_ADMISSION_CONFIG";
    public const int MaximumConfigurationBytes = 65_536;

    public CharacterSnapshotLoader CreateSnapshotLoader()
    {
        if (!TryNormalize(out var configured, out var reason)) throw new ArgumentException(reason);
        return new(configured.UsersRoot, configured.ItemsRoot, configured.SpellsRoot,
            configured.ProficienciesRoot, configured.CombatGearRoot, configured.QuestsRoot);
    }

    internal LegacyOrdinaryAdmissionRoots Roots => new(UsersRoot, ItemsRoot, SpellsRoot,
        ProficienciesRoot, QuestsRoot, CombatGearRoot);
    internal LegacyItemUidRegistryOptions RegistryOptions => new(UsersRoot, ItemsRoot,
        MailboxRoot, RedeemFile, ClaimFile, HistoricalHighWater, HistoricalEvidence);
}
