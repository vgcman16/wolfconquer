using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using TeamType = WolfConquer.Core.World.LegacyNativeTeamPacket.TeamTypes;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamPrerequisiteTests
{
    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2u)] [InlineData(3u)]
    [InlineData(4u)] [InlineData(5u)] [InlineData(6u)] [InlineData(7u)]
    [InlineData(8u)] [InlineData(9u)] [InlineData(10u)] [InlineData(11u)]
    [InlineData(12u)] [InlineData(13u)] [InlineData(14u)] [InlineData(15u)]
    [InlineData(4294967295u)]
    public Task TeamCreateAndReadPreserveEveryEnumAndUnknownRawValue(uint type) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Same(scope.Packet, scope.Packet.TeamCreate((TeamType)type, uint.MaxValue));
        var expected = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(expected, 12); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2243);
        Put32(expected, 4, type); Put32(expected, 8, uint.MaxValue);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(12, scope.Packet.Size); // Native Finalize rewinds after header.
        scope.Packet.Seek(4); scope.Packet.GetTeamPacket(out var readType, out uint uid);
        Assert.Equal((TeamType)type, readType); Assert.Equal(uint.MaxValue, uid); Assert.Equal(12, scope.Packet.Position);
    });

    [Fact]
    public Task TeamReadUsesCapturedInputAndRefusesIncompleteBodiesWithoutPartialValues() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.Seek(100); scope.Packet.Write(77u); scope.Packet.Write(89u);
        int inputEnd = scope.Packet.Position; scope.Packet.Size = inputEnd;
        scope.Packet.Seek(100); scope.Packet.GetTeamPacket(out var type, out uint uid);
        Assert.Equal((TeamType)77, type); Assert.Equal(89u, uid); Assert.Equal(108, scope.Packet.Position);
        byte[] before = NativeSpellTestFixture.Bytes(scope.Packet);
        scope.Packet.Seek(16380);
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetTeamPacket(out type, out uid));
        Assert.Equal((TeamType)77, type); Assert.Equal(89u, uid); Assert.Equal(16380, scope.Packet.Position);
        Assert.Equal(inputEnd, scope.Packet.Size); Assert.Equal(before, NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2u)] [InlineData(4294967295u)]
    public Task LeadershipCodecCopiesAllTwentyPackedBytesIncludingUnknownFieldAndNegativeCount(uint mode) => NativePacketTestThread.Run(() =>
    {
        LeadershipCodecCase(mode);
    });

    private static unsafe void LeadershipCodecCase(uint mode)
    {
        using var scope = new Scope();
        var query = new LegacyNativeTeamLeadership
        {
            Typ = (LegacyNativeTeamLeadershipPacket.Mode)mode, UID = 0x12345678,
            LeaderUID = uint.MaxValue, Count = int.MinValue, UnKnow = 0xDEADBEEF,
        };
        Assert.Same(scope.Packet, scope.Packet.TeamLeadershipCreate(&query));
        var expected = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(expected, 24); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2022);
        Put32(expected, 4, mode); Put32(expected, 8, 0x12345678); Put32(expected, 12, uint.MaxValue);
        Put32(expected, 16, 0x80000000); Put32(expected, 20, 0xDEADBEEF);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position); // Native Finalize rewinds.
        var copy = new LegacyNativeTeamLeadership();
        scope.Packet.Seek(4); scope.Packet.GetTeamLeadership(&copy);
        Assert.Equal(query.Typ, copy.Typ); Assert.Equal(query.UID, copy.UID); Assert.Equal(query.LeaderUID, copy.LeaderUID);
        Assert.Equal(query.Count, copy.Count); Assert.Equal(query.UnKnow, copy.UnKnow); Assert.Equal(24, scope.Packet.Position);
    }

    [Fact]
    public void NativeTeamRowsAndModesKeepActualWidthsDefaultsAndUnusualFieldNames()
    {
        Assert.Equal(20, Marshal.SizeOf<LegacyNativeTeamLeadership>());
        Assert.Equal(0, Marshal.OffsetOf<LegacyNativeTeamLeadership>(nameof(LegacyNativeTeamLeadership.Typ)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<LegacyNativeTeamLeadership>(nameof(LegacyNativeTeamLeadership.UnKnow)).ToInt32());
        Assert.Equal(new uint[] { 1, 2 }, Enum.GetValues<LegacyNativeTeamLeadershipPacket.Mode>().Select(value => (uint)value));
        Assert.Equal(Enumerable.Range(0, 16).Select(value => (uint)value), Enum.GetValues<TeamType>().Select(value => (uint)value));
        var member = new LegacyNativeTeamMemberInfo();
        Assert.Null(member.Name); Assert.Equal(0u, member.UID); Assert.Equal(0u, member.Mesh);
        Assert.Equal((ushort)0, member.MaxHitpoints); Assert.Equal((ushort)0, member.MinMHitpoints);
        Assert.Equal(typeof(ushort), typeof(LegacyNativeTeamMemberInfo).GetField(nameof(member.MinMHitpoints))!.FieldType);
    }
}
