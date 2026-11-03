using System.Text;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionImagePlanTests
{
    [Fact]
    public void MissingCompleteBundleCannotProduceAPartialPlan()
    {
        Assert.False(SqlOrdinaryAdmissionImagePlan.TryPrepare(null, Deadline, out var plan, out var reason));
        Assert.Null(plan); Assert.Equal("sqlAdmissionImageMissing", reason);
    }

    [Theory]
    [InlineData("RelicSpirit=bad\r\n", "capturedPrefixOptionalValueInvalid")]
    [InlineData("Spouse=\"Partner\"\r\n", "capturedPrefixQuotedOrControlValue")]
    [InlineData("SecurityPass=bad\r\n", "secondaryPasswordStorageInvalid")]
    [InlineData("SecurityPass=2468,1,not-a-date,\r\n", "secondaryPasswordResetTimeInvalid")]
    [InlineData("SecurityPass=0,0,0,\r\nsecuritypass=0,0,0,\r\n", "capturedPrefixCharacterFieldsAmbiguous")]
    public void ActualInvalidImportOrPinStateRefusesWithoutChangingAnyRetainedImage(string fields, string reason) =>
        AssertRefused(Bundle(AddFields(Text(), fields), AllSidecars()), reason);

    [Theory]
    [InlineData("Body=1003", "Body=65536")]
    [InlineData("Face=0", "Face=-1")]
    [InlineData("Online=1", "Online=4294967296")]
    public void NativeScalarWidthRefusesARealOtherwiseValidCoreBeforeProjection(string previous, string replacement) =>
        AssertRefused(Bundle(Text().Replace(previous, replacement, StringComparison.Ordinal), AllSidecars()),
            "capturedPrefixRequiredValueInvalid");

    [Fact]
    public void DuplicatePaidSectionCannotBeNormalizedByChoosingOneInterpretation() =>
        AssertRefused(Bundle(Text() + "[PaidNobility]\r\nIsActive=False\r\n[PaidNobility]\r\nPosition=1\r\n", AllSidecars()),
            "sqlAdmissionProjectionInvalid");

    [Fact]
    public void RealPostProjectionCharacterBoundRefusesWithoutReturningPartialCoreOrSidecars()
    {
        // One real bounded image, not fabricated factory authority or millions of spell rows.
        // Expired paid state inserts missing Character fields, so a complete exact-limit input must refuse growth.
        var prefix = Text() + PaidBefore(Deadline) + "[Padding]\r\nBytes=";
        var bytes = Encoding.UTF8.GetBytes(prefix + new string('x', SqlWorldStateImage.MaximumImageBytes - Encoding.UTF8.GetByteCount(prefix)));
        Assert.Equal(SqlWorldStateImage.MaximumImageBytes, bytes.Length);
        var before = Bundle(bytes: bytes);
        Assert.True(LegacyCapturedCharacterPrefix.TryCreate(before.Core, out var captured, out var captureReason), captureReason);
        Assert.NotNull(captured);
        AssertRefused(before, "sqlAdmissionProjectionInvalid", Deadline.AddTicks(1));
    }

    private static void AssertRefused(SqlWorldStateBundle before, string expectedReason, DateTime? at = null)
    {
        var original = before.Core.CopyCharacterBytes();
        var itemBytes = before.Core.CopyItemBytes();
        var sidecars = before.Sidecars.ToArray().Select(row => (row.Presence, Bytes: row.CopyBytes())).ToArray();
        Assert.False(SqlOrdinaryAdmissionImagePlan.TryPrepare(before, at ?? Deadline, out var plan, out var reason));
        Assert.Null(plan); Assert.Equal(expectedReason, reason);
        Assert.Equal(original, before.Core.CopyCharacterBytes()); Assert.Equal(itemBytes, before.Core.CopyItemBytes());
        for (int index = 0; index < sidecars.Length; index++)
        {
            Assert.Equal(sidecars[index].Presence, before.Sidecars[index].Presence);
            Assert.Equal(sidecars[index].Bytes, before.Sidecars[index].CopyBytes());
        }
    }
}
