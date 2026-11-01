using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyPhysicalAttackRangeTests
{
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(7, 0, 3, 9)]
    [InlineData(0, 7, 3, 9)]
    [InlineData(7, 4, 3, 7)]
    [InlineData(65535, 65534, 127, 65598)]
    [InlineData(0, 0, -128, -62)]
    [InlineData(0, 0, -2, 1)]
    public void PureFormulaRetainsSignedDivisionAndWideAverage(int right, int left, int size, int expected) =>
        Assert.Equal(expected, LegacyNativeMagicQueryClient.CalculateAttackRange((ushort)right, (ushort)left, size));

    [Theory]
    [InlineData(-129)] [InlineData(128)]
    public void TargetSizeOutsideTheActualSignedFamilyFieldRefuses(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeMagicQueryClient.CalculateAttackRange(1, 1, size));

    [Fact]
    public Task ActualAdmittedQueryIssuesTheRangeChildAndRetirementInvalidatesIt() =>
        OwnedEquipmentBehaviorFixture.RunAsync(new(OwnedEquipmentBehaviorFixture.Character(classCode: 2001),
            WolfConquer.Core.Data.PlayerItemSnapshot.Empty), OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackRangeCache(binding, out var first));
            Assert.NotNull(first); Assert.Equal((ushort)0, first.Right); Assert.Equal((ushort)0, first.Left);
            Assert.Equal(3, first.GetAttackRange(4));
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, LegacyRuneStoragePacket.Startup().ToFrameBytes());
            Assert.False(first.IsCurrentPlain(binding));
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackRangeCache(binding, out var second));
            Assert.NotNull(second); Assert.NotSame(first, second); Assert.Equal(3, second.GetAttackRange(4));
            var files = f.Data.Files();
            await f.Connection(OwnedEquipmentBehaviorFixture.Actor).DisconnectAsync();
            // Disconnect requests transport closure; the real transport EOF
            // producer performs the registered session retirement separately.
            await f.Connection(OwnedEquipmentBehaviorFixture.Actor).CompleteTransportCloseAsync();
            Assert.True(binding.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.False(second.IsCurrentPlain(binding));
            f.Data.AssertFiles(files);
        });
}
