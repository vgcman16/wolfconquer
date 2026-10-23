using System.Text;
using WolfConquer.Core.Networking;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Theory]
    [InlineData("equip", false, false)] [InlineData("equip", false, true)]
    [InlineData("equip", true, false)] [InlineData("equip", true, true)]
    [InlineData("unequip", false, false)] [InlineData("unequip", false, true)]
    [InlineData("unequip", true, false)] [InlineData("unequip", true, true)]
    [InlineData("swap", false, false)] [InlineData("swap", false, true)]
    [InlineData("swap", true, false)] [InlineData("swap", true, true)]
    public Task SlotSealBoundsUseTheRealAllocationIncludingSealBeforeAnyMutation(
        string operation, bool sealedPacket, bool oneByteShort) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var (left, right, tail) = ReadySlotPrefix(scope);
        if (sealedPacket) LegacyNativePacket.SealString = "TQServer";
        var expected = Hex(operation switch
        {
            "equip" => "0D0031090800109A0318052000",
            "unequip" => "0C0031090801100018022000",
            _ => "1200310908021000180020003A0408021003",
        });
        if (sealedPacket) expected = expected.Concat(Encoding.ASCII.GetBytes("TQServer")).ToArray();
        Assert.Equal(sealedPacket ? 8 : 0, LegacyNativePacket.SealSize);
        var before = CaptureSlotState(scope, left, right, tail);
        int available = scope.Available;
        using (var pool = new SlotPacketPool("valid", expected.Length - (oneByteShort ? 1 : 0)))
        {
            bool accepted = operation switch
            {
                "equip" => scope.Owner.Equip(Weapon.Blade, 5),
                "unequip" => scope.Owner.Unequip(2),
                _ => scope.Owner.Swap(2, 3),
            };
            Assert.Equal(!oneByteShort, accepted);
            if (oneByteShort) AssertSlotStateUnchanged(scope, before);
            else
            {
                Assert.Equal(operation == "equip" ? 36u : operation == "unequip" ? 0u : 8u, left.BitVector.bits[0]);
                Assert.Equal(operation == "swap" ? 4u : 8u, right.BitVector.bits[0]);
                Assert.Equal(16u, tail.BitVector.bits[0]);
                var frames = scope.Queue.ToArray();
                Assert.Equal(before.Frames.Length + 1, frames.Length);
                Assert.Equal(expected, frames[^1]);
                for (int index = 0; index < before.Frames.Length; index++)
                {
                    Assert.Same(before.Frames[index], frames[index]);
                    Assert.Equal(before.FrameBytes[index], frames[index]);
                }
            }
        }
        Assert.Equal(available, scope.Available);
    });
}
