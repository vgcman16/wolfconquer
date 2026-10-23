using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    private sealed class SlotPacketPool : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        private readonly LegacyNativePacket? owned;
        internal SlotPacketPool(string fault, int capacity = LegacyNativePacket.MAX_SIZE)
        {
            if (fault == "missing") { Bin.SetValue(null, null); return; }
            if (fault == "null") { Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([null!])); return; }
            owned = new LegacyNativePacket(fault == "undersized" ? 8 : capacity);
            if (fault == "disposed") owned.Dispose();
            Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([owned]));
        }
        public void Dispose() { Bin.SetValue(null, prior); owned?.Dispose(); }
    }

    [Theory]
    [InlineData("equip", "missing")] [InlineData("equip", "null")]
    [InlineData("equip", "undersized")] [InlineData("equip", "disposed")]
    [InlineData("unequip", "missing")] [InlineData("unequip", "null")]
    [InlineData("unequip", "undersized")] [InlineData("unequip", "disposed")]
    [InlineData("swap", "missing")] [InlineData("swap", "null")]
    [InlineData("swap", "undersized")] [InlineData("swap", "disposed")]
    public Task PublicationV2RealRentalFailurePreservesRowsAndEveryEarlierPacket(string operation, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var (left, right, tail) = ReadySlotPrefix(scope);
        var before = CaptureSlotState(scope, left, right, tail); int available = scope.Available;
        using (var pool = new SlotPacketPool(fault))
        {
            Assert.False(operation switch
            {
                "equip" => scope.Owner.Equip(Weapon.Blade, 5),
                "unequip" => scope.Owner.Unequip(2),
                _ => scope.Owner.Swap(2, 3),
            });
            AssertSlotStateUnchanged(scope, before);
        }
        Assert.Equal(available, scope.Available);
    });

    [Fact]
    public Task PublicationV2NoUpdateOperationsDoNotRequireAnOptionalPacketPool() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var left = scope.Item(Weapon.Blade); var right = scope.Item(Weapon.Sword);
        right.BitVector.Add(3);
        using (var pool = new SlotPacketPool("missing"))
        {
            Assert.True(scope.Owner.Equip(Weapon.Blade, 2, false));
            Assert.True(scope.Owner.Swap(2, 3, false));
            Assert.True(scope.Owner.Unequip(3, false));
        }
        Assert.Equal(0u, left.BitVector.bits[0]); Assert.Equal(4u, right.BitVector.bits[0]); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PublicationV2LostTransportAfterCommitDoesNotReportAnUncommittedRejection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade);
        var queueField = typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var queue = queueField.GetValue(scope.Client.Socket);
        queueField.SetValue(scope.Client.Socket, null);
        try
        {
            Assert.True(scope.Owner.Equip(Weapon.Blade, 2)); Assert.True(row.BitVector.Contain(2));
            Assert.Empty(scope.Queue);
        }
        finally { queueField.SetValue(scope.Client.Socket, queue); }
    });
}
