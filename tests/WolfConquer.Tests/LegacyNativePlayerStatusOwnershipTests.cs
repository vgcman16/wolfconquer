using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerStatusOwnershipTests
{
    [Fact]
    public void EachPhysicalPlayerOwnsOneGenuine576BitVectorWithoutTheOldSidecar()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var other = new LegacyNativeGameClientRuntime();
        var original = owner.Player.BitVector;
        Assert.Equal(576, original.Size);
        Assert.Equal(new uint[18], original.bits);
        Assert.Empty(original.ArrayFlags);
        Assert.NotSame(original, other.Player.BitVector);
        Assert.NotSame(original.bits, other.Player.BitVector.bits);
        Assert.Null(typeof(LegacyNativePlayerRuntime).GetProperty("Flags"));
        Assert.Null(typeof(LegacyNativePlayerRuntime).Assembly.GetType("WolfConquer.Core.World.LegacyNativePlayerFlag6State"));
        owner.Player.BitVector = other.Player.BitVector; // Native mutable-reference prestate, not owner repair.
        Assert.Same(other.Player.BitVector, owner.Player.BitVector);
        Assert.NotSame(original, owner.Player.BitVector);
        Assert.Same(owner, owner.Player.Owner);
    }

    [Fact]
    public void CompatibilityProjectionRemainsRawWhileMembershipRejectsIncoherentStatus()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        ulong[] input = Enumerable.Range(0, 18).Select(index => ulong.MaxValue - (ulong)index).ToArray();
        var original = input.ToArray();
        var absent = LegacyNativePlayerFlag6Lifecycle.CaptureVector(player, input);
        Assert.Equal(input[0] & ~64ul, absent[0]);
        Assert.Equal(input[1..], absent[1..]);
        player.BitVector.Add(6); // Projection is a documented raw escape, not membership authority.
        Assert.Throws<InvalidDataException>(() => player.ContainFlag(Flag.TeamLeader));
        var visible = LegacyNativePlayerFlag6Lifecycle.CaptureVector(player, input);
        Assert.Equal(input[0] | 64ul, visible[0]);
        player.BitVector.Remove(6); // Explicit fixture restoration to a coherent empty vector.
        Assert.True(player.BitVector.TryAdd(6, 1, false, 0));
        var row = player.BitVector.ArrayFlags[6];
        player.BitVector.Remove(6); // Corrupt registration-only state must not report presence.
        Assert.Throws<InvalidDataException>(() => player.ContainFlag(Flag.TeamLeader));
        Assert.Same(row, player.BitVector.ArrayFlags[6]);
        Assert.Equal(absent, LegacyNativePlayerFlag6Lifecycle.CaptureVector(player, input));
        Assert.Equal(original, input);
        Assert.Equal(input, LegacyNativePlayerFlag6Lifecycle.CaptureVector(null, input));
        Assert.NotSame(input, visible);
    }

    [Fact]
    public void StatusWrappersUseCurrentPhysicalVectorAndNativeClockNotTheHoldingClient()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var holder = new LegacyNativeGameClientRuntime();
        holder.Player = owner.Player;
        var foreignVector = new LegacyNativeStatusFlagsBigVector32(576);
        owner.Player.BitVector = foreignVector;
        Assert.False(holder.Player.UpdateFlag(Flag.TeamLeader, 3, true, 5));
        Assert.False(holder.Player.CheckInvokeFlag(Flag.TeamLeader, DateTime.MaxValue));
        foreignVector.TryAdd(6, 7, false, 0);
        var registration = foreignVector.ArrayFlags[6];
        var before = DateTime.Now;
        Assert.True(holder.Player.UpdateFlag(Flag.TeamLeader, 13, true, 1));
        var after = DateTime.Now;
        Assert.InRange(registration.Timer, before, after);
        Assert.Equal(13, registration.Secounds);
        Assert.Same(registration, foreignVector.ArrayFlags[6]);
        Assert.True(holder.Player.CheckInvokeFlag(Flag.TeamLeader, DateTime.MinValue));
        Assert.True(holder.Player.ContainFlag(Flag.TeamLeader));
        Assert.Same(owner, holder.Player.Owner);
        Assert.Same(foreignVector, holder.Player.BitVector);
    }

    [Fact]
    public Task UpdateFlagOffsetSendsTheActualMutableArrayWithoutEnforcingEighteenWords() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        uint[] words = [0, 1, uint.MaxValue];
        f.Owner.Player.BitVector.bits = words;
        Assert.False(f.Owner.FullLoading); // Native method has no FullLoading gate.
        Assert.Null(f.Owner.Map); // Real RoleView has no recipients, not an invented host map.
        f.Owner.Player.UpdateFlagOffset();
        words[1] = 99;
        var expected = new LegacyMsgUpdatePacket(17,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, 0, Values: [0, 1, uint.MaxValue])]);
        Assert.Equal(expected.ToFrameBytes(), Assert.Single(f.Queue));
        Assert.Same(words, f.Owner.Player.BitVector.bits);
        Assert.Equal(96, f.Owner.Player.BitVector.Size);
    });

    [Fact]
    public Task OffsetViewFaultRetainsTheOwnerSendAndTheSameVector() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var vector = f.Owner.Player.BitVector;
        vector.Add(6);
        f.Owner.Player.View = null!;
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.UpdateFlagOffset());
        Assert.Single(f.Queue);
        Assert.Same(vector, f.Owner.Player.BitVector);
        Assert.True(vector.Contain(6));
        Assert.Throws<InvalidDataException>(() => vector.ContainFlag(6));
    });

    [Fact]
    public Task NullVectorFaultsWithoutSilentRepairOrQueuePublication() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Owner.Player.BitVector = null!;
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.ContainFlag(Flag.TeamLeader));
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.CheckInvokeFlag(Flag.TeamLeader, DateTime.Now));
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.UpdateFlag(Flag.TeamLeader, 1, true, 1));
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.UpdateFlagOffset());
        Assert.Throws<NullReferenceException>(() => LegacyNativePlayerFlag6Lifecycle.CaptureVector(f.Owner.Player, new ulong[18]));
        Assert.Null(f.Owner.Player.BitVector);
        Assert.Empty(f.Queue);
    });
}
