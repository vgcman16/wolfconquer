using System.Globalization;
using System.Text;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionImagePlanTests
{
    [Fact]
    public void AlreadyNormalizedNonTrojanDataHasNoChangesAndIssuesOnlyImmutablePreparationData()
    {
        var before = Bundle(sidecars: AllSidecars()); var original = before.Core.CopyCharacterBytes();
        var plan = Prepare(before);
        Assert.Same(before, plan.Before); Assert.Equal(Deadline, plan.PreparedAt); Assert.False(plan.HasChanges);
        Assert.Equal(Deadline.Kind, plan.PreparedAt.Kind);
        Assert.Equal(original, plan.After.Core.CopyCharacterBytes());
        AssertUnchangedOwnershipAndSidecars(before, plan.After);
        Assert.Same(plan.After.Core, plan.PreparedPrefix.Image);
        Assert.Equal(501L, plan.After.AccountId); Assert.Equal(9001U, plan.After.CharacterUid);
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, plan.After.ItemUids.ToArray());
        Assert.Equal(0U, plan.Pin.Password); Assert.Equal(0U, plan.Pin.OnReset);
        var copied = plan.After.Core.CopyCharacterBytes(); copied.AsSpan().Fill(0);
        Assert.IsType<Dictionary<string, string>>(plan.PreparedPrefix.Snapshot.Attributes)["Name"] = "foreign";
        Assert.Equal(original, before.Core.CopyCharacterBytes()); Assert.Equal(original, plan.After.Core.CopyCharacterBytes());
        Assert.Equal("SqlStateSynthetic", plan.PreparedPrefix.Snapshot.Name);
        Assert.Equal("SqlStateSynthetic", plan.PreparedPrefix.Snapshot.Attributes["Name"]);
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("windows-1252")]
    public void OnlineOnlyProjectionPreservesAllUnselectedBytesEncodingAndPresence(string codec)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = codec == "windows-1252" ? Encoding.GetEncoding(1252) : new UTF8Encoding(codec == "utf8-bom", true);
        var text = Text().Replace("Online=1\r\n", "  oNlInE = \t+7 \t\n; preserve\r", StringComparison.Ordinal)
            .Replace("Sentinel=owned-sql-state\r\n", "Sentinel=café €\nOnline=unrelated", StringComparison.Ordinal);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("\t+7 \t\n", "\t1 \t\n", StringComparison.Ordinal))).ToArray();
        var before = Bundle(bytes: bytes); var plan = Prepare(before);
        Assert.True(plan.HasChanges); Assert.Equal(expected, plan.After.Core.CopyCharacterBytes());
        Assert.Equal(bytes, before.Core.CopyCharacterBytes()); Assert.Equal(1L, plan.PreparedPrefix.Snapshot.Online);
        AssertUnchangedOwnershipAndSidecars(before, plan.After);
        Assert.All(plan.After.Sidecars.ToArray(), row => Assert.Equal(SqlWorldStateSidecarPresence.Missing, row.Presence));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void SecurityPassExpiresOnlyStrictlyAfterItsSavedDeadline(int deltaTicks, bool expired)
    {
        var text = AddFields(Text(), "SecurityPass=" + PinText(Deadline) + "\r\n");
        var before = Bundle(text); var plan = Prepare(before, Deadline.AddTicks(deltaTicks));
        Assert.Equal(expired, plan.HasChanges); Assert.Equal(expired ? 0U : 2468U, plan.Pin.Password);
        Assert.Equal(expired ? 0U : 1U, plan.Pin.OnReset);
        var expected = expired ? text.Replace("SecurityPass=" + PinText(Deadline),
            "SecurityPass=0,0," + Deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal) : text;
        Assert.Equal(Encoding.UTF8.GetBytes(expected), plan.After.Core.CopyCharacterBytes());
        Assert.Equal(Encoding.UTF8.GetBytes(text), before.Core.CopyCharacterBytes());
        AssertUnchangedOwnershipAndSidecars(before, plan.After);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void PaidNobilityExpiryUsesItsOwnInclusiveDeadlineAndRestoresSourceGroundedRegularRank(int deltaTicks, bool expired)
    {
        var text = NormalizingText(); // Existing explicit paid30M restores Knight1; no guessed rank threshold.
        // Leave PIN outside this case so its distinct strict deadline cannot mask the paid predicate.
        text = text.Replace("SecurityPass=" + PinText(Deadline) + "\r\n", "", StringComparison.Ordinal);
        var before = Bundle(text, AllSidecars()); var plan = Prepare(before, Deadline.AddTicks(deltaTicks));
        var expected = expired ? ExpectedNormalizedText(text) : text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), plan.After.Core.CopyCharacterBytes());
        Assert.Equal(expired ? "30000000" : "999999999", plan.PreparedPrefix.Snapshot.Attributes["DonationNobility"]);
        Assert.Equal(expired ? "False" : "True", plan.PreparedPrefix.Snapshot.PaidNobility["IsActive"]);
        Assert.Equal("paid-source", plan.PreparedPrefix.Snapshot.PaidNobility["OpaqueKeep"]);
        AssertUnchangedOwnershipAndSidecars(before, plan.After, spellsMayChange: true);
    }
}
