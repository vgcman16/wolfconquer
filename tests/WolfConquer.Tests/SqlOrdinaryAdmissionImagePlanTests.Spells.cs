using System.Text;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionImagePlanTests
{
    [Theory]
    [InlineData(1000, 0, 0, true)]
    [InlineData(1057, 0, 0, true)]
    [InlineData(10, 1001, 0, true)]
    [InlineData(10, 0, 1056, true)]
    [InlineData(999, 0, 0, false)]
    [InlineData(1058, 0, 0, false)]
    public void ActualTrojanLineageAloneGrants1110WhileEveryOpaqueExistingSpellByteSurvives(int current, int first, int second, bool grant)
    {
        var text = Text().Replace("\r\nClass=10\r\n", "\r\nClass=" + current + "\r\n", StringComparison.Ordinal)
            .Replace("FirstClass=10\r\n", "FirstClass=" + first + "\r\n", StringComparison.Ordinal)
            .Replace("SecoundeClass=0\r\n", "SecoundeClass=" + second + "\r\n", StringComparison.Ordinal);
        var before = Bundle(text, AllSidecars()); var plan = Prepare(before);
        Assert.Equal((long)current, plan.PreparedPrefix.Snapshot.Class);
        Assert.Equal((long)first, plan.PreparedPrefix.Snapshot.FirstClass);
        Assert.Equal((long)second, plan.PreparedPrefix.Snapshot.SecondClass);
        var spell = SqlWorldStateBundleTests.Sidecar(plan.After, SqlWorldStateSidecarKind.Spells);
        var expected = grant ? Convert.FromHexString("02000000EA03020078563412030401A5560400000000000000000000") : OpaqueSpell();
        Assert.Equal(expected, spell.CopyBytes()); Assert.Equal(SqlWorldStateSidecarPresence.Present, spell.Presence);
        Assert.Equal(grant, plan.HasChanges); Assert.Equal(Encoding.UTF8.GetBytes(text), plan.After.Core.CopyCharacterBytes());
        Assert.Equal(OpaqueSpell(), SqlWorldStateBundleTests.Sidecar(before, SqlWorldStateSidecarKind.Spells).CopyBytes());
        AssertUnchangedOwnershipAndSidecars(before, plan.After, spellsMayChange: true);
    }

    [Fact]
    public void Existing1110RetainsUnsortedRawRowsAndOpaquePaddingWithoutAppendingADuplicate()
    {
        var text = Text().Replace("\r\nClass=10\r\n", "\r\nClass=1005\r\n", StringComparison.Ordinal);
        var raw = SqlWorldStateBundleTests.Spells(); var before = Bundle(text, AllSidecars(raw)); var plan = Prepare(before);
        Assert.False(plan.HasChanges);
        Assert.Equal(raw, SqlWorldStateBundleTests.Sidecar(plan.After, SqlWorldStateSidecarKind.Spells).CopyBytes());
        AssertUnchangedOwnershipAndSidecars(before, plan.After);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MissingAndPresentEmptySpellBooksRemainDistinctUnlessAnActual1110GrantCreatesPresentData(bool trojan, bool present)
    {
        var text = trojan ? Text().Replace("\r\nClass=10\r\n", "\r\nClass=1005\r\n", StringComparison.Ordinal) : Text();
        var inputs = present ? SqlWorldStateBundleTests.Replace(SqlWorldStateSidecarKind.Spells, new byte[4]) : SqlWorldStateBundleTests.Missing();
        var before = Bundle(text, inputs); var plan = Prepare(before);
        var spell = SqlWorldStateBundleTests.Sidecar(plan.After, SqlWorldStateSidecarKind.Spells);
        Assert.Equal(trojan || present ? SqlWorldStateSidecarPresence.Present : SqlWorldStateSidecarPresence.Missing, spell.Presence);
        Assert.Equal(trojan ? Convert.FromHexString("01000000560400000000000000000000") : present ? new byte[4] : [], spell.CopyBytes());
        Assert.Equal(trojan, plan.HasChanges);
        AssertUnchangedOwnershipAndSidecars(before, plan.After, spellsMayChange: true);
        foreach (var sidecar in plan.After.Sidecars.ToArray().Where(row => row.Kind != SqlWorldStateSidecarKind.Spells))
        { Assert.Equal(SqlWorldStateSidecarPresence.Missing, sidecar.Presence); Assert.Empty(sidecar.CopyBytes()); }
    }
}
