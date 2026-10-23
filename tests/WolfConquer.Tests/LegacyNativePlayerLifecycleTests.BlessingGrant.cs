using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(0, "0 hours")] [InlineData(3600, "1 hours")] [InlineData(86400, "24 hours")]
    [InlineData(86401, "1 days")] [InlineData(-3600, "-1 hours")]
    public Task WholeBlessingGrantUsesStrictDayBoundaryAndNativeMessageBeforeState(int seconds, string phrase)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); DateTime before = DateTime.Now;
        s.Player.AddHeavenBlessing(s.Packet, seconds); DateTime after = DateTime.Now;
        var frames = s.Queue.ToArray(); var talk = Talk(frames[0]);
        Assert.Equal("You`ve received " + phrase + "` blessing time.", talk.Content[3]);
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Talk, talk.ChatType);
        Assert.Equal(seconds, s.Player.HeavenBlessing);
        Assert.InRange(s.Player.HeavenBlessTime, before.AddSeconds(seconds), after.AddSeconds(seconds));
        Assert.Equal(seconds > 0 ? 5 : 1, frames.Length);
    });

    [Theory]
    [InlineData(10000, 1u)] [InlineData(9999, 0u)] [InlineData(-10000, uint.MaxValue)]
    public Task GenuineMentorAndMemberCreditUsesIntegerTenThousandUnitsAndUnsignedWrap(int seconds, uint credit)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.HeavenBlessing); s.Player.HeavenBlessing = 20000;
        s.Player.HeavenBlessTime = DateTime.Now; var mentor = new LegacyNativeAssociateRuntime.MyAsociats(25);
        var member = new LegacyNativeAssociateRuntime.Member { UID = s.Player.UID };
        mentor.Add(5, member); s.Player.MyMentor = mentor;
        Assert.Same(mentor, LegacyNativeAssociateRuntime.Associates[25]);
        s.Player.AddHeavenBlessing(s.Packet, seconds);
        Assert.Equal(credit, mentor.Mentor_Blessing); Assert.Equal(credit, member.Blessing);
        Assert.Same(member, mentor.Associat[5][s.Player.UID]); Assert.Equal(20000 + seconds, s.Player.HeavenBlessing);
    });

    [Fact]
    public Task MissingMentorMemberStillCreditsMentorAndDoesNotCreateFakeAssociation() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.MyMentor = new LegacyNativeAssociateRuntime.MyAsociats(25);
        s.Player.AddHeavenBlessing(s.Packet, 10000);
        Assert.Equal(1u, s.Player.MyMentor.Mentor_Blessing); Assert.Empty(s.Player.MyMentor.Associat);
    });

    [Fact]
    public Task DateOverflowOccursAfterChatButBeforeDurationAndMentorChanges() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.HeavenBlessing); s.Player.HeavenBlessing = 20;
        s.Player.HeavenBlessTime = DateTime.MaxValue; s.Player.MyMentor = new LegacyNativeAssociateRuntime.MyAsociats(25);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Player.AddHeavenBlessing(s.Packet, 1));
        Assert.Equal(20, s.Player.HeavenBlessing); Assert.Equal(DateTime.MaxValue, s.Player.HeavenBlessTime);
        Assert.Equal(0u, s.Player.MyMentor.Mentor_Blessing); _ = Talk(Assert.Single(s.Queue));
    });

    [Fact]
    public Task ChatRecycleFaultRetainsInitialStampResetAndNothingAfterIt() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NullBin(); DateTime before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => s.Player.AddHeavenBlessing(s.Packet, 10000));
        Assert.InRange(s.Player.HeavenBlessTime, before, DateTime.Now); Assert.Equal(0, s.Player.HeavenBlessing);
        Assert.Empty(s.Player.BitVector.ArrayFlags); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task NullOutputStreamFaultLeavesDurationAndFlagButDoesNotCreditMentor() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.MyMentor = new LegacyNativeAssociateRuntime.MyAsociats(25);
        Assert.Throws<NullReferenceException>(() => s.Player.AddHeavenBlessing(null!, 10000));
        Assert.Equal(10000, s.Player.HeavenBlessing); Assert.True(s.Player.ContainFlag(Flag.HeavenBlessing));
        Assert.Equal(0u, s.Player.MyMentor.Mentor_Blessing); Assert.Equal(2, s.Queue.Count);
    });

    [Fact]
    public Task MentorCollectionFaultIsLastAndRetainsRealMentorCreditAndAllPackets() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.MyMentor = new LegacyNativeAssociateRuntime.MyAsociats(25) { Associat = null! };
        Assert.Throws<NullReferenceException>(() => s.Player.AddHeavenBlessing(s.Packet, 10000));
        Assert.Equal(1u, s.Player.MyMentor.Mentor_Blessing); Assert.Equal(10000, s.Player.HeavenBlessing);
        Assert.True(s.Player.ContainFlag(Flag.HeavenBlessing)); Assert.Equal(5, s.Queue.Count);
    });

    [Fact]
    public Task SignedBlessingOverflowIsNotSaturatedAndCanSuppressPublication() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.HeavenBlessing); s.Player.HeavenBlessing = int.MaxValue;
        s.Player.HeavenBlessTime = DateTime.Now;
        s.Player.AddHeavenBlessing(s.Packet, 1); Assert.Equal(int.MinValue, s.Player.HeavenBlessing);
        _ = Talk(Assert.Single(s.Queue)); Assert.Equal(default, s.Player.OnlineTrainingTime);
    });
}
