using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Fact]
    public Task ActualSecondFanEquipAddsAfterPerfectionDuplicateDoesNotRenewAndUnequipRemoves() => Run(async f =>
    {
        AssertJoined(f, false); int first = f.Sent.Count; var started = DateTime.Now;
        await f.EquipAsync(); var finished = DateTime.Now;
        AssertJoined(f, true); var row = f.Row!; Assert.InRange(row.Timer, started, finished);
        var equipped = f.Sent.Skip(first).ToArray(); AssertOnlyFinalVector(equipped, true);
        Assert.Equal(Convert.FromHexString("3100800808D4BB920112260819180018001800180018001800180418001800180018001800180018001800180018001800"), equipped[^1]);
        var files = f.Owner.Data.Files(); var timer = row.Timer; var expiry = row.Timer.AddSeconds(row.Secounds);
        first = f.Sent.Count; await f.QueryAsync();
        Assert.Equal(first + 12, f.Sent.Count); LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, first + 2, Uid);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer); Assert.Equal(expiry, row.Timer.AddSeconds(row.Secounds));
        AssertJoined(f, true); AssertUnchangedSaved(f, files);
        first = f.Sent.Count; await f.UnequipAsync(); AssertJoined(f, false);
        AssertOnlyFinalVector(f.Sent.Skip(first).ToArray(), false);
        files = f.Owner.Data.Files(); first = f.Sent.Count; await f.QueryAsync();
        Assert.Equal(first + 12, f.Sent.Count); Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        AssertUnchangedSaved(f, files);
    });

    [Theory]
    [InlineData(16051L, true)] [InlineData(7051L, true)] [InlineData(10050L, false)]
    public Task ActualSavedPairUsesSourceLeftSelectionWithoutAnExtraPostludeClassGate(long job, bool expected) => Run(async f =>
    {
        // Saved shape is declared before bootstrap. This tests QueryEquipment's
        // selection; it does not claim live Equip permission for another class.
        AssertJoined(f, expected); var row = f.Row; var files = f.Owner.Data.Files(); int first = f.Sent.Count;
        await f.QueryAsync(); Assert.Equal(first + 12, f.Sent.Count);
        Assert.Same(row, f.Row); AssertJoined(f, expected); AssertUnchangedSaved(f, files);
    }, mode: "pair", job: job);

    [Theory]
    [InlineData("broken")] [InlineData("broken-right")]
    public Task ActualBrokenFanDoesNotCreateTheQueryRegistration(string mode) => Run(async f =>
    {
        AssertJoined(f, false); var files = f.Owner.Data.Files(); int first = f.Sent.Count;
        await f.QueryAsync(); AssertJoined(f, false); Assert.Equal(first + 12, f.Sent.Count);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _)); AssertUnchangedSaved(f, files);
    }, mode: mode);

    [Theory]
    [InlineData("alternate")] [InlineData("fallback")]
    public Task RealAlternateToggleUsesTheCurrentNormalAlternateOrFallbackPair(string mode) => Run(async f =>
    {
        AssertJoined(f, true); var row = f.Row!; var timer = row.Timer; int first = f.Sent.Count;
        await f.Owner.ToggleAsync(Uid); Assert.True(f.Current.AlternateEquipmentActive);
        Assert.True(f.Native.Equipment!.Alternante); await f.QueryAsync(); AssertJoined(f, true);
        var alternate = LegacyNativeMagicQueryClient.Read(f.Store, f.Owner.Connection(Uid), Uid);
        Assert.Equal(Dragon, alternate.RightWeaponItemId);
        Assert.Equal(mode == "alternate" ? Pride : BetterPride, alternate.LeftWeaponItemId);
        Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        first = f.Sent.Count; await f.Owner.ToggleAsync(Uid); Assert.False(f.Current.AlternateEquipmentActive);
        Assert.False(f.Native.Equipment.Alternante); await f.QueryAsync(); AssertJoined(f, true); Assert.Same(row, f.Row);
        var normal = LegacyNativeMagicQueryClient.Read(f.Store, f.Owner.Connection(Uid), Uid);
        Assert.Equal(Pride, normal.RightWeaponItemId); Assert.Equal(BetterPride, normal.LeftWeaponItemId);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
    }, mode: mode);

    [Fact]
    public Task ActualOwnerThenVisibleViewerReceiveTheSameVectorAfterOwnerPerfection() => Run(async f =>
    {
        f.Order.Clear(); await f.EquipAsync(); AssertJoined(f, true);
        var vectors = f.Order.Select((value, index) => (value, index)).Where(item => IsVector(item.value.Bytes, out _)).ToArray();
        Assert.Equal(new uint[] { Uid, Peer }, vectors.Select(item => item.value.Recipient));
        Assert.Equal(VectorGolden(Words(true)), vectors[0].value.Bytes); Assert.Equal(vectors[0].value.Bytes, vectors[1].value.Bytes);
        int perfection = f.Order.FindLastIndex(value => value.Recipient == Uid &&
            LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(value.Bytes), out _));
        Assert.True(perfection >= 0 && perfection < vectors[0].index);
    }, peer: true);

    [Fact]
    public Task ManagedOpaqueUpperBitsAndUnrelatedFlagsSurviveThePhysicalFanOverlay() => Run(async f =>
    {
        AssertJoined(f, false); var files = f.Owner.Data.Files(); int first = f.Sent.Count;
        await f.EquipAsync(); AssertJoined(f, true);
        var words = Words(true); words[0] = 1UL << 63; words[17] = 0xF123456789ABCDEFUL;
        var vector = Assert.Single(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.Equal(VectorGolden(words), vector);
        // Native physical uint words are not replaced by the managed ulong
        // compatibility vector; only the genuine registration194 was added.
        Assert.Equal(0U, f.Vector.bits[0]); Assert.Equal(0U, f.Vector.bits[17]);
        var after = f.Owner.Data.Files(); first = f.Sent.Count; await f.QueryAsync();
        AssertUnchangedSaved(f, after); Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.Equal(files[Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Uid))],
            after[Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Uid))]);
    }, flags: "9223372036854775808/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/17375808098319191535");

    [Fact]
    public Task FineRainDeniedQueryKeepsTheOldRegistrationUntilARealAdmittedQuery() => Run(async f =>
    {
        Assert.Equal(10000, LegacyFineRainEquipmentAdmission.ForOwner(f.Current).StampMilliseconds);
        AssertJoined(f, false); int first = f.Sent.Count; await f.EquipAsync();
        AssertJoined(f, false); Assert.Contains(f.Sent.Skip(first), bytes =>
            LegacyMsgMessagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var wait) && wait.Content.Contains("You Shoud wait 6 seconds."));
        var files = f.Owner.Data.Files(); f.Clock.Advance(TimeSpan.FromSeconds(6)); first = f.Sent.Count;
        // The declared saved flag212 shares word6 with fan194. It survives
        // both overlays; its 3-byte varint makes this complete frame51 bytes.
        await f.QueryAsync(); AssertJoined(f, true); AssertOnlyFinalVector(f.Sent.Skip(first).ToArray(), true, 1UL << 20);
        AssertUnchangedSaved(f, files); var row = f.Row!; var timer = row.Timer;
        first = f.Sent.Count; await f.UnequipAsync(); Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
        AssertJoined(f, true); f.Clock.Advance(TimeSpan.FromSeconds(6)); first = f.Sent.Count;
        await f.QueryAsync(); AssertJoined(f, false); AssertOnlyFinalVector(f.Sent.Skip(first).ToArray(), false, 1UL << 20);
    }, fineRain: true);

    [Fact]
    public Task RealNativeDurationShorteningThenActualFrameExpiryRemovesWithoutRegrantOrDiskWrites() => Run(async f =>
    {
        AssertJoined(f, true); var old = f.Row!;
        // The real existing native API shortens a genuinely query-created row.
        // This is not evidence that thirty days elapsed and supplies no clock.
        Assert.True(f.Native.Player!.UpdateFlag((LegacyNativeMsgUpdate.Flags)Fan, 0, true, 0));
        Assert.Same(old, f.Row); Assert.Equal(0, old.Secounds);
        var files = f.Owner.Data.Files(); int first = f.Sent.Count;
        await f.Owner.Handler.SendDueLegacyWindWalkerFanExpirationsAsync(CancellationToken.None);
        AssertJoined(f, false); Assert.Equal(first + 1, f.Sent.Count);
        Assert.Equal(VectorGolden(Words()), f.Sent[first]); AssertUnchangedSaved(f, files);
        first = f.Sent.Count; await f.Owner.Handler.SendDueLegacyWindWalkerFanExpirationsAsync(CancellationToken.None);
        Assert.Equal(first, f.Sent.Count); AssertJoined(f, false);
        var earliest = DateTime.Now; await f.QueryAsync(); var latest = DateTime.Now;
        AssertJoined(f, true); Assert.NotSame(old, f.Row); Assert.InRange(f.Row!.Timer, earliest, latest);
        AssertUnchangedSaved(f, files);
    }, mode: "pair");
}
