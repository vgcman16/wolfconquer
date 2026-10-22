namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Theory]
    [InlineData(1, 17, 18, "17")]
    [InlineData(65530, 17, 65535, "17")]
    [InlineData(0, 0, 0, "0")]
    [InlineData(0, 65535, 65535, "65535")]
    public Task AddStudyClampsThenQueuesExactTalkEffectAndAnimationInNativeOrder(
        int before, int study, int expected, string messageAmount) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Runtime.StudyPoints = (ushort)before;
            scope.Runtime.AddStudyPoints(scope.Client, (ushort)study, scope.Packet);
            Assert.Equal((ushort)expected, scope.Runtime.StudyPoints);
            scope.Frames(StudyMessage(messageAmount), Effect(), ListFrame(8, (ushort)expected));
            scope.CallerIs(ListFrame(8, (ushort)expected));
            scope.ChatWasDisposed(StudyMessage(messageAmount));
            Assert.Null(scope.Client.Map); // Actual View.SendView sends self then enumerates no map roles.
        });

    [Theory]
    [InlineData(18, 17, 1, true)]
    [InlineData(17, 17, 17, false)]
    [InlineData(16, 17, 16, false)]
    [InlineData(18, 0, 18, true)]
    public Task RemoveStudyUsesStrictGreaterThanIncludingEqualAndZeroCosts(
        int before, int cost, int expected, bool sends) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Runtime.StudyPoints = (ushort)before;
            scope.Runtime.RemoveStudy(scope.Client, (ushort)cost, scope.Packet);
            Assert.Equal((ushort)expected, scope.Runtime.StudyPoints);
            if (sends)
            {
                scope.Frames(Effect(), ListFrame(8, (ushort)expected));
                scope.CallerIs(ListFrame(8, (ushort)expected));
            }
            else scope.Unwritten();
        });

    [Fact]
    public Task UpdateStudyPacketUsesCurrentPlayerUidAndRetainedStudyWithoutMutatingEither() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.UID = 0x12345678;
        scope.Runtime.StudyPoints = 65535;
        scope.Runtime.UpdateStudyPacket(scope.Client, scope.Packet);
        scope.Frames(Effect(0x12345678), ListFrame(8, 65535));
        scope.CallerIs(ListFrame(8, 65535));
        Assert.Equal(ushort.MaxValue, scope.Runtime.StudyPoints);
        Assert.Equal(0x12345678u, scope.Client.Player.UID);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LateStudyFaultRetainsPointsAndAlreadyQueuedTalkBeforeEffectOrAnimation(bool nullView) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Runtime.StudyPoints = 1;
        if (nullView) scope.Client.Player.View = null!;
        Assert.Throws<NullReferenceException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17,
            nullView ? scope.Packet : null!));
        Assert.Equal((ushort)18, scope.Runtime.StudyPoints);
        scope.Frames(StudyMessage("17"));
        scope.ChatWasDisposed(StudyMessage("17"));
        if (nullView) scope.CallerIs(Effect()); // Argument serialization precedes null receiver call.
        else
        {
            Assert.Equal(321, scope.Packet.Size);
            Assert.Equal(237, scope.Packet.Position);
        }
    });
}
