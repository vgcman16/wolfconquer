using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Never run tiny/disposed cases on the old native CreateNinjaUser writer.
// The gate cases also require the new genuine SpellStorage mutation gate.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNinjaPointsTests
{
    [Theory]
    [InlineData(1, 3U)]
    [InlineData(8, 3U)]
    [InlineData(51, 3U)]
    [InlineData(51, 0U)]
    [InlineData(52, 3U)]
    public Task RealRentalCapacityIsCheckedBeforeAnyPointOrNativeWrite(int capacity, uint amount) =>
        LegacyNativeNinjaMutationTests.RunCorrectedOnlyPointStorage(capacity, false, amount);

    [Fact]
    public Task DisposedActualRentalCannotCreditOrDereferenceNativeMemory() =>
        LegacyNativeNinjaMutationTests.RunCorrectedOnlyPointStorage(LegacyNativePacket.MAX_SIZE, true, 3);

    [Theory]
    [InlineData(0U)] [InlineData(3U)]
    public Task HeldRealSpellPreparationRefusesPointCreditWithoutInvertedActorLocks(uint amount) =>
        LegacyNativeNinjaMutationTests.RunCorrectedOnlyPointSpellGate(amount);
}
