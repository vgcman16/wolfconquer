using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;
using Static = WolfConquer.Core.World.LegacyNativeMsgStaticMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDialogPacketTests
{
    [Fact]
    public void NpcPhysicalContractAndBothNativeEnumsRetainExactWidthsFieldsAndRequiredFlags()
    {
        Assert.NotNull(typeof(Reply.NpcRequestProto).GetCustomAttribute<ProtoContractAttribute>());
        (string Name, Type Type, int Tag, bool Required)[] expected =
        [
            ("Action", typeof(ushort), 1, true), ("NpcID", typeof(uint), 2, true),
            ("OptionID", typeof(byte), 3, true), ("InteractType", typeof(byte), 4, true), ("Input", typeof(string), 5, false)
        ];
        FieldInfo[] fields = typeof(Reply.NpcRequestProto).GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(expected.Length, fields.Length);
        foreach (var row in expected)
        {
            FieldInfo field = Assert.Single(fields, value => value.Name == row.Name);
            Assert.Equal(row.Type, field.FieldType); Assert.False(field.IsInitOnly);
            var member = field.GetCustomAttribute<ProtoMemberAttribute>(); Assert.NotNull(member);
            Assert.Equal(row.Tag, member.Tag); Assert.Equal(row.Required, member.IsRequired);
        }
        var empty = new Reply.NpcRequestProto();
        Assert.Equal((ushort)0, empty.Action); Assert.Equal(0u, empty.NpcID);
        Assert.Equal((byte)0, empty.OptionID); Assert.Equal((byte)0, empty.InteractType); Assert.Null(empty.Input);
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Reply.InteractTypes)));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 6, 100, 113 }, Enum.GetValues<Reply.InteractTypes>().Select(value => (byte)value));
        Assert.Equal(new[] { "Dialog", "Option", "Input", "Avatar", "MessageBox", "Finish", "Page" }, Enum.GetNames<Reply.InteractTypes>());
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(LegacyNativeNpcServerReplay.Mode)));
        Assert.Equal(new ushort[] { 3, 5, 6 }, Enum.GetValues<LegacyNativeNpcServerReplay.Mode>().Select(value => (ushort)value));
        Assert.Equal(new[] { "Statue", "Cursor", "PlaceFurniture" }, Enum.GetNames<LegacyNativeNpcServerReplay.Mode>());
        // Signature only: Exists is deliberately never invoked because even null input can open SQL.
        MethodInfo exists = typeof(LegacyNativeDialog).GetMethod("Exists", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(typeof(bool), exists.ReturnType); Assert.Equal(typeof(string), Assert.Single(exists.GetParameters()).ParameterType);
    }

    [Fact]
    public void StaticMessageIsOneByteSequentialAndRetainsEveryNativeEnumNameIncludingSharedValue()
    {
        Assert.Equal(LayoutKind.Sequential, typeof(Static).StructLayoutAttribute!.Value);
        Assert.Equal(1, typeof(Static).StructLayoutAttribute!.Size); Assert.Equal(1, Marshal.SizeOf<Static>());
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Static.Messages)));
        var expected = new Dictionary<string, uint>
        {
            ["None"] = 0, ["GuildWar"] = 10515, ["ClassPk"] = 10519, ["WeeklyPKWar"] = 10521,
            ["MonthlyPk"] = 10532, ["HorseRace"] = 10525, ["MonthlyPK"] = 10527, ["WeeklyPK"] = 10529,
            ["PowerArena"] = 10531, ["ElitePKTournament"] = 10533, ["CapturetheFlag"] = 10535,
            ["GuildContest"] = 10537, ["SkillTeamPKTournament"] = 10541, ["ChampionsArena"] = 10543,
            ["CashRain"] = 10545, ["FlameLit"] = 10547, ["TeamPkTournament"] = 10543
        };
        Assert.Equal(expected.Keys.OrderBy(value => value), Enum.GetNames<Static.Messages>().OrderBy(value => value));
        foreach (var row in expected) Assert.Equal(row.Value, (uint)Enum.Parse<Static.Messages>(row.Key));
        Assert.Equal(Static.Messages.ChampionsArena, Static.Messages.TeamPkTournament);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Static.Action)));
        Assert.Equal(new[] { "Append" }, Enum.GetNames<Static.Action>()); Assert.Equal(6u, (uint)Static.Action.Append);
    }

    [Fact]
    public Task StaticWriterKeepsFullUnknownUnsignedValuesAndReaderReadsOnlyTheCurrentCursor() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Same(scope.Packet, scope.Packet.StaticMessageCreate((Static.Messages)uint.MaxValue, (Static.Action)0x80000000u, 0x89ABCDEF));
            Assert.Equal(Convert.FromHexString("1400100900000000FFFFFFFF00000080EFCDAB89"), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(20, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
            scope.Packet.GetStaticMessage(out uint zero); Assert.Equal(0u, zero); Assert.Equal(8, scope.Packet.Position);
            scope.Packet.GetStaticMessage(out uint message); Assert.Equal(uint.MaxValue, message); Assert.Equal(12, scope.Packet.Position);
            scope.Packet.GetStaticMessage(out uint action); Assert.Equal(0x80000000u, action); Assert.Equal(16, scope.Packet.Position);
            scope.Packet.GetStaticMessage(out uint seconds); Assert.Equal(0x89ABCDEFu, seconds); Assert.Equal(20, scope.Packet.Position);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task StaticReaderAtStrictPhysicalBoundaryReturnsZeroWithoutAdvanceOrManagedSizeCheck() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Packet.Size = 1; scope.Packet.Seek(LegacyNativePacket.MAX_SIZE - 4);
            scope.Packet.GetStaticMessage(out uint rejected); Assert.Equal(0u, rejected);
            Assert.Equal(LegacyNativePacket.MAX_SIZE - 4, scope.Packet.Position);
            scope.Packet.Seek(LegacyNativePacket.MAX_SIZE - 5); scope.Packet.GetStaticMessage(out uint accepted);
            Assert.Equal(0xA5A5A5A5u, accepted); Assert.Equal(LegacyNativePacket.MAX_SIZE - 1, scope.Packet.Position);
            Assert.Equal(1, scope.Packet.Size); Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, scope.Raw(16379, 5));
        });

    [Fact]
    public Task StaticNullStreamFaultsRetainCallerOutputAndLeaveOwnedMemoryUnchanged() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); uint accepted = 77;
            Assert.Throws<NullReferenceException>(() => LegacyNativeStaticMessagePacket.GetStaticMessage(null!, out accepted));
            Assert.Equal(77u, accepted);
            Assert.Throws<NullReferenceException>(() => LegacyNativeStaticMessagePacket.StaticMessageCreate(null!, Static.Messages.GuildWar, Static.Action.Append, 60));
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
        });
}
