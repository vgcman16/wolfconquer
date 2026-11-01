using System.Buffers.Binary;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualWeaponHitCommitsProficiencyBeforeItsReplyAndPreservesOpaqueSavedRecordBytes(bool existing) =>
        RunProficiencyAsync(existing, f => AssertProficiencyHitAsync(f, existing));

    [Fact]
    public Task ARealProficiencyReadLeaseRefusesBeforeHpActionStampOrRepeatAndAllowsSameClockRetry() =>
        RunProficiencyAsync(true, async f =>
        {
            var before = CaptureProficiencyRefusal(f);
            using (var held = new FileStream(ProficiencyPath(f), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await f.AttackAsync();
                AssertProficiencyRefusal(f, before);
            }
            await AssertProficiencyHitAsync(f, existing: true);
        });

    [Fact]
    public Task AnExternallyChangedProficiencyBaselineCannotCommitTheHitOrArmRepeat() => RunProficiencyAsync(true, async f =>
    {
        var original = File.ReadAllBytes(ProficiencyPath(f));
        var changed = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(16), 101);
        // Declared NEGATIVE external file mutation, never an accepted Store
        // baseline/positive map or a post-admission permission fabrication.
        File.WriteAllBytes(ProficiencyPath(f), changed);
        try
        {
            var before = CaptureProficiencyRefusal(f);
            await f.AttackAsync(); AssertProficiencyRefusal(f, before);
        }
        finally { File.WriteAllBytes(ProficiencyPath(f), original); }
        await AssertProficiencyHitAsync(f, existing: true);
    });
}
