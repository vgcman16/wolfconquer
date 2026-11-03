using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionImagePlanTests
{
    internal static readonly DateTime Deadline = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Unspecified);
    internal static byte[] OpaqueSpell() => Convert.FromHexString("01000000EA03020078563412030401A5");
    internal static string Text(uint uid = 9001) => Encoding.UTF8.GetString(SqlWorldStateTestData.Character(uid));
    internal static string AddFields(string text, string fields) => text.Replace("[Unrelated]", fields + "[Unrelated]", StringComparison.Ordinal);
    internal static string PinText(DateTime deadline) => "2468,1," + deadline.ToBinary().ToString(CultureInfo.InvariantCulture) + ",";

    internal static SqlWorldStateBundle Bundle(string? text = null, SqlWorldStateSidecarInput[]? sidecars = null,
        long account = 501, uint uid = 9001, PlayerItemSnapshot? items = null, byte[]? bytes = null)
    {
        Assert.True(SqlWorldStateImage.TryCreate(account, uid, bytes ?? Encoding.UTF8.GetBytes(text ?? Text(uid)),
            PlayerItemSnapshotLoader.ToLegacyBytes(items ?? SqlWorldStateTestData.FirstItems()), out var core, out var failure), failure.ToString());
        return SqlWorldStateBundleTests.Bundle(Assert.IsType<SqlWorldStateImage>(core), sidecars ?? SqlWorldStateBundleTests.Missing());
    }

    internal static SqlWorldStateSidecarInput[] AllSidecars(byte[]? spells = null) =>
    [
        new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, spells ?? OpaqueSpell()),
        new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.Proficiencies()),
        new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.Quests()),
        new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.EmptyGear()),
    ];

    internal static SqlOrdinaryAdmissionImagePlan Prepare(SqlWorldStateBundle before, DateTime? at = null)
    {
        Assert.True(SqlOrdinaryAdmissionImagePlan.TryPrepare(before, at ?? Deadline, out var plan, out var reason), reason);
        Assert.Equal(string.Empty, reason); return Assert.IsType<SqlOrdinaryAdmissionImagePlan>(plan);
    }

    internal static void AssertUnchangedOwnershipAndSidecars(SqlWorldStateBundle before, SqlWorldStateBundle after, bool spellsMayChange = false)
    {
        Assert.Equal(before.AccountId, after.AccountId); Assert.Equal(before.CharacterUid, after.CharacterUid);
        Assert.Equal(before.Core.CopyItemBytes(), after.Core.CopyItemBytes());
        Assert.Equal(before.ItemUids.ToArray(), after.ItemUids.ToArray());
        foreach (var sidecar in before.Sidecars)
        {
            if (spellsMayChange && sidecar.Kind == SqlWorldStateSidecarKind.Spells) continue;
            var actual = SqlWorldStateBundleTests.Sidecar(after, sidecar.Kind);
            Assert.Equal(sidecar.Presence, actual.Presence); Assert.Equal(sidecar.CopyBytes(), actual.CopyBytes());
        }
    }

    internal static string PaidBefore(DateTime deadline) => "[PaidNobility]\r\nIdentifier=19\r\nLastNobilityDonation=30000000\r\n" +
        "PaidRank=7\r\nPeriodTime=" + deadline.ToBinary().ToString(CultureInfo.InvariantCulture) +
        "\r\nIsActive=True\r\nRank=King\r\nPosition=1\r\nOpaqueKeep=paid-source\r\n";
    internal const string PaidAfter = "[PaidNobility]\r\nIdentifier=0\r\nLastNobilityDonation=0\r\nPaidRank=0\r\nPeriodTime=0\r\n" +
        "IsActive=False\r\nRank=Serf\r\nPosition=-1\r\nOpaqueKeep=paid-source\r\n";

    internal static string NormalizingText(uint uid = 9001) => AddFields(Text(uid),
        "DonationNobility=999999999\r\nLastNobilityDonation=999999999\r\nNobilityRank=7\r\nNobilityPosition=1\r\n" +
        "SecurityPass=" + PinText(Deadline) + "\r\n").Replace("\r\nClass=10\r\n", "\r\nClass=1005\r\n", StringComparison.Ordinal)
        .Replace("Online=1\r\n", "Online=7\r\n", StringComparison.Ordinal) + PaidBefore(Deadline);

    internal static string ExpectedNormalizedText(string source) => source.Replace(PaidBefore(Deadline), PaidAfter, StringComparison.Ordinal)
        .Replace("DonationNobility=999999999", "DonationNobility=30000000", StringComparison.Ordinal)
        .Replace("LastNobilityDonation=999999999", "LastNobilityDonation=30000000", StringComparison.Ordinal)
        .Replace("NobilityRank=7\r\n", "NobilityRank=1\r\n", StringComparison.Ordinal)
        .Replace("NobilityPosition=1\r\n", "NobilityPosition=-1\r\n", StringComparison.Ordinal)
        .Replace("SecurityPass=" + PinText(Deadline), "SecurityPass=0,0," + Deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal)
        .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal);
}
