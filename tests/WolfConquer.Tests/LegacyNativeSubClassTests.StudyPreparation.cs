using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Theory]
    [InlineData("missing-subclass")]
    [InlineData("foreign-subclass")]
    [InlineData("foreign-player")]
    [InlineData("missing-player")]
    [InlineData("foreign-view")]
    [InlineData("missing-map-view")]
    public Task StudyRejectsUnboundActualOwnerGraphBeforeAnyResourceOrOutput(string invalid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(bindStudy: true);
            scope.Runtime.StudyPoints = 1;
            byte[] before = StudyPhysicalBytes(scope.Packet);
            var player = scope.Client.Player;
            switch (invalid)
            {
                case "missing-subclass": player.SubClass = null; break;
                case "foreign-subclass": player.SubClass = new LegacyNativeSubClassRuntime(); break;
                case "foreign-player":
                    scope.Client.Player = new LegacyNativeGameClientRuntime().Player;
                    scope.Client.Player.SubClass = scope.Runtime;
                    break;
                case "missing-player": scope.Client.Player = null!; break;
                case "foreign-view": player.View = new LegacyNativeRoleView(new LegacyNativeGameClientRuntime()); break;
                case "missing-map-view": scope.Client.Map = new LegacyNativeGameMap(36, 36, 1002); break;
            }
            Assert.Throws<InvalidOperationException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet));
            Assert.Equal((ushort)1, scope.Runtime.StudyPoints);
            scope.Unwritten();
            Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
            Assert.Equal(2, LegacyNativePacketRecycle.Count);
        });

    [Fact]
    public Task StudyRejectsMissingClientBeforeNativeRental() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        var before = StudyPhysicalBytes(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => scope.Runtime.AddStudyPoints(null!, 17, scope.Packet));
        Assert.Equal((ushort)0, scope.Runtime.StudyPoints);
        Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
        scope.Unwritten();
        Assert.Equal(2, LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(StudyEntry.Remove)]
    [InlineData(StudyEntry.Notify)]
    public Task StudyOtherActiveEntriesRefuseMissingViewWithoutMutation(StudyEntry entry) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(bindStudy: true);
            scope.Runtime.StudyPoints = 18;
            var before = StudyPhysicalBytes(scope.Packet);
            scope.Client.Player.View = null!;
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (entry == StudyEntry.Remove) scope.Runtime.RemoveStudy(scope.Client, 17, scope.Packet);
                else scope.Runtime.UpdateStudyPacket(scope.Client, scope.Packet);
            });
            Assert.Equal((ushort)18, scope.Runtime.StudyPoints);
            Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
            scope.Unwritten();
            Assert.Equal(2, LegacyNativePacketRecycle.Count);
        });

    public enum StudyEntry { Remove, Notify }

    [Fact]
    public Task StudyNoOpRemoveDoesNotRequireUnusedNotificationGraph() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Runtime.StudyPoints = 17;
        scope.Runtime.RemoveStudy(null!, 17, null!);
        Assert.Equal((ushort)17, scope.Runtime.StudyPoints);
        scope.Unwritten();
        Assert.Equal(2, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task StudyPreparesAnimationCapacityBeforeTakingChatEvenWhenEffectWouldFit() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        using var shortPacket = new LegacyNativePacket(25);
        FillStudyPacket(shortPacket);
        byte[] before = StudyPhysicalBytes(shortPacket);
        Assert.Throws<InvalidOperationException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, shortPacket));
        Assert.Equal((ushort)0, scope.Runtime.StudyPoints);
        Assert.Equal(before, StudyPhysicalBytes(shortPacket));
        Assert.Equal(25, shortPacket.Size);
        Assert.Equal(25, shortPacket.Position);
        Assert.Empty(scope.Queue);
        Assert.Equal(2, LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InvalidNativeRentalRefusesWithoutGameplayCallerOrQueueMutation(bool missingPool) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true, chatCapacity: 8);
        scope.Runtime.StudyPoints = 1;
        byte[] before = StudyPhysicalBytes(scope.Packet);
        if (missingPool) scope.MissingPool();
        Assert.Throws<InvalidOperationException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet));
        Assert.Equal((ushort)1, scope.Runtime.StudyPoints);
        Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
        scope.Unwritten();
        // A real invalid rented slot is consumed; this is allocation, not a point debit.
        if (!missingPool) Assert.Equal(1, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task AliasedNativeChatRentalRestoresOnlyItsIncidentalCallerCursorChange() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        scope.Runtime.StudyPoints = 1;
        byte[] before = StudyPhysicalBytes(scope.Packet);
        scope.PoolContains(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet));
        Assert.Equal((ushort)1, scope.Runtime.StudyPoints);
        Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
        scope.Unwritten();
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData("size")]
    [InlineData("cursor")]
    [InlineData("disposed")]
    public Task InvalidCallerPhysicalStateRefusesBeforeRental(string invalid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        using var target = new LegacyNativePacket(40);
        FillStudyPacket(target);
        byte[] before = StudyPhysicalBytes(target);
        if (invalid == "size") target.Size = 41;
        if (invalid == "cursor") target.Seek(41);
        if (invalid == "disposed") target.Dispose();
        if (invalid == "disposed")
            Assert.Throws<ObjectDisposedException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, target));
        else
        {
            Assert.Throws<InvalidOperationException>(() => scope.Runtime.AddStudyPoints(scope.Client, 17, target));
            Assert.Equal(before, StudyPhysicalBytes(target));
            Assert.Equal(invalid == "size" ? 41 : 40, target.Size);
            Assert.Equal(invalid == "cursor" ? 41 : 40, target.Position);
        }
        Assert.Equal((ushort)0, scope.Runtime.StudyPoints);
        Assert.Empty(scope.Queue);
        Assert.Equal(2, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task StudyPreservesActualCustomSealWithoutByteArraySenderRewriting() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        LegacyNativePacket.SealString = "12345678";
        scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet);
        byte[] Seal(byte[] frame) => frame.Concat("12345678"u8.ToArray()).ToArray();
        scope.Frames(Seal(StudyMessage("17")), Seal(Effect()), Seal(ListFrame(8, 17)));
        scope.CallerIs(Seal(ListFrame(8, 17)));
        scope.ChatWasDisposed(Seal(StudyMessage("17")));
        Assert.Equal((ushort)17, scope.Runtime.StudyPoints);
    });

    [Fact]
    public Task StudyEffectUsesActualPreparedVisibilityRecipientsIncludingNativeDuplicates() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        scope.Client.Map = map;
        scope.Client.Player.Map = 1002;
        scope.Client.Player.X = scope.Client.Player.Y = 18;
        var recipientQueue = new Queue<byte[]>();
        var recipient = NativeSpellTestFixture.CreateOwner(recipientQueue);
        recipient.Player.UID = 18;
        recipient.Player.Map = 1002;
        recipient.Player.X = recipient.Player.Y = 18;
        Assert.True(map.View.EnterMap(recipient.Player));
        Assert.True(map.View.EnterMap(recipient.Player));
        scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet);
        scope.Frames(StudyMessage("17"), Effect(), ListFrame(8, 17));
        var received = recipientQueue.ToArray();
        Assert.Equal(2, received.Length);
        Assert.Equal(Effect(), received[0]);
        Assert.Equal(Effect(), received[1]);
        scope.CallerIs(ListFrame(8, 17));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExhaustedStudyRevisionRefusesSetterOrAwardWithoutResourceOrOutputMutation(bool setter) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        scope.Runtime.StudyPoints = 1;
        byte[] before = StudyPhysicalBytes(scope.Packet);
        typeof(LegacyNativeSubClassRuntime).GetField("studyRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(scope.Runtime, ulong.MaxValue);
        Assert.Throws<OverflowException>(() =>
        {
            if (setter) scope.Runtime.StudyPoints = 18;
            else scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet);
        });
        Assert.Equal((ushort)1, scope.Runtime.StudyPoints);
        Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
        scope.Unwritten();
        Assert.Equal(2, LegacyNativePacketRecycle.Count);
    });

    private static unsafe byte[] StudyPhysicalBytes(LegacyNativePacket packet)
    {
        byte[] bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    private static unsafe void FillStudyPacket(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);
        packet.Size = Math.Min(321, packet.Capacity);
        packet.Seek(Math.Min(237, packet.Capacity));
    }
}
