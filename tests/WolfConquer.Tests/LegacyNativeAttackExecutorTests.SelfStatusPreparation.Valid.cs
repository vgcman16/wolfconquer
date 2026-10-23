using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public unsafe Task CompletePreparedEffectsKeepExactWireAndNativeSuccessfulCallerPostconditions(bool stigma, bool existing, bool sealedPacket)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        if (existing) Assert.True(s.Player.BitVector.TryAdd((int)SelfStatusFlag(stigma), 9, false, 0));
        var oldRegistry = s.Player.BitVector.ArrayFlags; var oldWords = s.Player.BitVector.bits;
        object? oldCache = SelfStatusCache.GetValue(s.Player.BitVector);
        var oldRow = existing ? oldRegistry[(int)SelfStatusFlag(stigma)] : null;
        s.Player.XPBooster = 5; s.Player.XPKiller = 6;
        PrepareEffectResultCallerPacket(s); var callerBefore = CopyEffectResultPacket(s.Packet);
        byte[] seal = sealedPacket ? Encoding.ASCII.GetBytes("TQServer") : [];
        if (sealedPacket) LegacyNativePacket.SealString = "TQServer";
        byte[] animation = SelfStatusAnimation(stigma, existing).Concat(seal).ToArray();
        int statusLength = NativeAddFlagTestFixture.Status(SelfStatusFlag(stigma)).Length + seal.Length;
        using (var pool = new SelfStatusPoolScope("exact", existing, s.Packet, animation.Length, statusLength))
            Assert.True(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        var frames = s.Queue.ToArray();
        Assert.Equal(existing ? new ushort[] { 2239 } : new ushort[] { 2176, 2239 }, frames.Select(Opcode));
        Assert.Equal(animation, frames[^1]);
        if (!existing) Assert.Equal(NativeAddFlagTestFixture.Status(SelfStatusFlag(stigma)).Concat(seal), frames[0]);
        var callerExpected = callerBefore.ToArray(); animation.CopyTo(callerExpected, 0);
        Assert.Equal(callerExpected, CopyEffectResultPacket(s.Packet));
        Assert.Equal(animation.Length, s.Packet.Size); Assert.Equal(4, s.Packet.Position);
        var row = s.Player.BitVector.ArrayFlags[(int)SelfStatusFlag(stigma)];
        Assert.Equal(existing ? 9 : stigma ? 90 : 40, row.Secounds); Assert.Equal(!existing, row.RemoveOnDead);
        if (existing)
        {
            Assert.Same(oldRow, row); Assert.Same(oldRegistry, s.Player.BitVector.ArrayFlags);
            Assert.Same(oldWords, s.Player.BitVector.bits); Assert.Same(oldCache, SelfStatusCache.GetValue(s.Player.BitVector));
        }
        Assert.Equal((byte)5, s.Player.XPBooster); Assert.Equal((byte)6, s.Player.XPKiller);
    });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public Task ActualNearbyPeerReceivesPreparedStatusThenAnimationWithOriginalTargets(bool stigma, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var map = new LegacyNativeGameMap(128, 128, 1002) { View = new LegacyNativeMapView(128, 128) };
        s.Client.Map = map; s.Player.Map = 1002;
        var peerQueue = new Queue<byte[]>(); var peer = s.Peer(peerQueue); peer.Map = map;
        peer.Player.Map = 1002; peer.Player.X = 33; peer.Player.Y = 42;
        Assert.True(map.View.EnterMap(s.Player)); Assert.True(map.View.EnterMap(peer.Player));
        if (existing) Assert.True(s.Player.BitVector.TryAdd((int)SelfStatusFlag(stigma), 9, false, 0));
        Assert.True(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        var ownerFrames = s.Queue.ToArray(); var peerFrames = peerQueue.ToArray();
        Assert.Equal(existing ? new ushort[] { 2239 } : new ushort[] { 2176, 2239 }, ownerFrames.Select(Opcode));
        Assert.Equal(ownerFrames.Length, peerFrames.Length);
        for (int i = 0; i < ownerFrames.Length; i++) Assert.Equal(ownerFrames[i], peerFrames[i]);
        Assert.Equal(SelfStatusAnimation(stigma, existing), ownerFrames[^1]);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualDisconnectedSenderDoesNotRefundAnAcceptedPreparedFlag(bool stigma) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Socket!.Alive = false;
        Assert.True(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        Assert.True(s.Player.ContainFlag(SelfStatusFlag(stigma))); Assert.Empty(s.Queue);
        Assert.Equal(SelfStatusAnimation(stigma, false), NativeSpellTestFixture.Bytes(s.Packet));
        Assert.Equal(4, s.Packet.Position);
    });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public Task ExactCallerAllocationIncludesCapturedSealBeforeAcceptance(bool stigma, bool sealedPacket)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        byte[] seal = sealedPacket ? Encoding.ASCII.GetBytes("TQServer") : [];
        if (sealedPacket) LegacyNativePacket.SealString = "TQServer";
        byte[] expected = SelfStatusAnimation(stigma, false).Concat(seal).ToArray();
        using var caller = new LegacyNativePacket(expected.Length);
        caller.InitWriter();
        Assert.True(LegacyNativeSelfStatusEffect.TryExecute(s.Client, caller, stigma));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(caller)); Assert.Equal(4, caller.Position);
        Assert.Equal(expected, s.Queue.Last());
    });
}
