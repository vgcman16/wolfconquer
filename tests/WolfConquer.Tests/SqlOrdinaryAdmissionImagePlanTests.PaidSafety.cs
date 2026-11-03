using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionImagePlanTests
{
    [Theory]
    [MemberData(nameof(PaidNobilityInputSafetyTests.InvalidPaidInputs), MemberType = typeof(PaidNobilityInputSafetyTests))]
    public void ActualCompleteBundlePlanRefusesMalformedPaidStateWithoutReplacingAnyOfItsSixImages(string caseName, string paid)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));
        var before = Bundle(PaidNobilityInputSafetyTests.CharacterWithPaid(paid), AllSidecars());
        Assert.Equal(501L, before.AccountId); Assert.Equal(9001U, before.CharacterUid);
        var original = before.Core.CopyCharacterBytes(); var items = before.Core.CopyItemBytes(); var uids = before.ItemUids.ToArray();
        var sidecars = before.Sidecars.ToArray().Select(row => (row.Presence, Bytes: row.CopyBytes())).ToArray();
        Assert.False(SqlOrdinaryAdmissionImagePlan.TryPrepare(before, Deadline, out var plan, out var reason));
        Assert.Null(plan); Assert.Equal("sqlAdmissionProjectionInvalid", reason);
        Assert.Equal(original, before.Core.CopyCharacterBytes()); Assert.Equal(items, before.Core.CopyItemBytes());
        Assert.Equal(uids, before.ItemUids.ToArray());
        for (int index = 0; index < sidecars.Length; index++)
        {
            Assert.Equal(sidecars[index].Presence, before.Sidecars[index].Presence);
            Assert.Equal(sidecars[index].Bytes, before.Sidecars[index].CopyBytes());
        }
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(original, out _));
        Assert.Equal("777777777", saved.Attributes["DonationNobility"]); Assert.Equal("7", saved.Attributes["NobilityRank"]);
        Assert.Equal(1000L, saved.Money); Assert.Equal(100L, saved.ConquerPoints);
    }
}
