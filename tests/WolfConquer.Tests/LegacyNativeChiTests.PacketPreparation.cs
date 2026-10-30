using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public unsafe Task ChiMessageUsesExactWholeFrameCapacityAndPreservesShortTargets(bool sealedFrame, bool tooShort) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var seal = new ChiPreparationSealScope(sealedFrame);
            var attribute = new Attr(Stat.CriticalStrike, 17, false);
            byte[] body = ChiMessage(Kind.Dragon, attribute, "Wolf");
            byte[] expected = sealedFrame ? body.Concat("TQServer"u8.ToArray()).ToArray() : body;
            using var target = new LegacyNativePacket(expected.Length - (tooShort ? 1 : 0));
            byte[] initialized = Enumerable.Repeat((byte)0xA5, target.Capacity).ToArray();
            Marshal.Copy(initialized, 0, (nint)target.Memory, initialized.Length);
            target.Size = 3; target.Seek(2);
            if (tooShort)
            {
                Assert.Throws<InvalidOperationException>(() => target.ChiMessageCreate(Kind.Dragon, attribute, "Wolf"));
                Assert.Equal(3, target.Size); Assert.Equal(2, target.Position);
                Assert.Equal(initialized, SnapshotChiPacket(target));
            }
            else
            {
                Assert.Same(target, target.ChiMessageCreate(Kind.Dragon, attribute, "Wolf"));
                Assert.Equal(expected.Length, target.Size); Assert.Equal(4, target.Position);
                Assert.Equal(expected, SnapshotChiPacket(target));
            }
            Assert.Empty(scope.Queue);
        });

    private static unsafe byte[] SnapshotChiPacket(LegacyNativePacket packet)
    {
        byte[] result = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, result, 0, result.Length);
        return result;
    }

    private sealed class ChiPreparationSealScope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Seal.GetValue(null);
        internal ChiPreparationSealScope(bool enabled) => Seal.SetValue(null, enabled ? "TQServer" : null);
        public void Dispose() => Seal.SetValue(null, previous);
    }
}
