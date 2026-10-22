using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityMessageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task IconKeepsNativeScalarZeroRegionStringListWidthsAndCurrentCulture(bool customCulture) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = new LegacyNativeNobility(17, "not serialized", 99, uint.MaxValue, byte.MaxValue) { Position = 2 };
            string text = "17 99 12 2";
            if (customCulture)
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); culture.NumberFormat.NegativeSign = "\u2212";
                CultureInfo.CurrentCulture = culture;
                row.UID = uint.MaxValue; row.Donation = ulong.MaxValue; row.Position = int.MinValue;
                text = "4294967295 18446744073709551615 5 \u22122147483648";
            }

            Assert.Same(scope.Packet, scope.Packet.NobilityIconCreate(row));

            Assert.Equal(Icon(row.UID, text), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Native.Queue); Assert.Empty(scope.Pool);
            Assert.Equal("not serialized", row.Name); Assert.Equal(uint.MaxValue, row.Mesh); Assert.Equal(byte.MaxValue, row.Gender);
        });

    [Fact]
    public Task NullNobilityFaultLeavesInitWriterAndActionPrefixButNoFinalizeOrZeroFill() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<NullReferenceException>(() => scope.Packet.NobilityIconCreate(null!));
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(8, scope.Packet.Position);
            Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 3, 0, 0, 0, 0xA5 }, NativeSpellTestFixture.Bytes(scope.Packet).Take(9));
            Assert.Empty(scope.Native.Queue); Assert.Empty(scope.Pool);
            Assert.Throws<NullReferenceException>(() => LegacyNativeNobilityPacket.NobilityIconCreate(null!, null!));
        });
}
