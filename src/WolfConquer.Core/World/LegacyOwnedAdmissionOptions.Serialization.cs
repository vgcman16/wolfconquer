using System.Text.Json;

namespace WolfConquer.Core.World;

public sealed partial record LegacyOwnedAdmissionOptions
{
    public byte[] ToConfigurationBytes()
    {
        if (!TryNormalize(out var value, out var reason)) throw new ArgumentException(reason);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1, ExpectedDomain = value.ExpectedDomain.ToString("N"), value.UsersRoot,
            value.ItemsRoot, value.SpellsRoot, value.ProficienciesRoot, value.QuestsRoot, value.CombatGearRoot,
            value.MailboxRoot, value.RedeemFile, value.ClaimFile, value.HistoricalHighWater, value.HistoricalEvidence,
        });
        if (bytes.Length > MaximumConfigurationBytes) throw new ArgumentException("ownedAdmissionConfigurationTooLarge");
        return bytes;
    }
}
