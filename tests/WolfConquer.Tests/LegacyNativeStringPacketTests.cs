using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using StringId = WolfConquer.Core.World.LegacyNativeMsgStringPacket.StringID;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeStringPacketTests
{
    [Fact]
    public Task CompleteByteEnumAndMutablePacketDefaultsMatchTheNativeDeclaration() => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(StringId)));
        Assert.Null(typeof(StringId).GetCustomAttribute<FlagsAttribute>());
        var fields = typeof(StringId).GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Fireworks", "GuildName", "Spouse", "LocationEffect", "Effect", "GuildList",
            "Unknown", "ViewEquipSpouse", "StartGamble", "EndGamble", "Sound", "GuildAllies", "GuildEnemies",
            "WhisperDetails", "ServerName" }, fields.Select(field => field.Name));
        Assert.Equal(new byte[] { 1, 3, 6, 9, 10, 11, 13, 16, 17, 18, 20, 21, 22, 26, 61 },
            fields.Select(field => (byte)field.GetRawConstantValue()!));
        Assert.Equal(15, fields.Select(field => field.GetRawConstantValue()).Distinct().Count()); // No aliases.
        var message = new LegacyNativeMsgStringPacket();
        Assert.Null(message.Strings);
        Assert.Equal(0u, message.UID);
        Assert.Equal((ushort)0, message.X);
        Assert.Equal((ushort)0, message.Y);
        Assert.Equal((byte)0, message.StringsLength);
        Assert.Equal((StringId)0, message.ID);
        Assert.All(typeof(LegacyNativeMsgStringPacket).GetFields(BindingFlags.Public | BindingFlags.Instance),
            field => Assert.False(field.IsInitOnly));
        string[] strings = ["first"];
        message.Strings = strings;
        strings[0] = "changed";
        Assert.Same(strings, message.Strings);
        Assert.Equal("changed", message.Strings[0]);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CreateReturnsTheActualStreamWithExact2036FrameAndNativeSealCursor(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope(sealedFrame);
        var stream = scope.Packet;
        string[] strings = ["fx"];
        var message = new LegacyNativeMsgStringPacket
        {
            UID = 0x12345678, ID = StringId.Effect, Strings = strings, X = 9, Y = 10, StringsLength = 255
        };
        stream.Seek(700); // StringPacketCreate resets the writer, not append-at-current-position.
        Assert.Same(stream, stream.StringPacketCreate(message));
        var expected = Convert.FromHexString("0E00F407785634120A0102006678" +
            (sealedFrame ? "5451536572766572" : ""));
        Assert.Equal(expected, NativeStringPacketTestScope.Bytes(stream));
        Assert.Equal(expected.Length, stream.Size);
        Assert.Equal(4, stream.Position);
        Assert.Same(strings, message.Strings);
        Assert.Equal((byte)255, message.StringsLength); // Metadata is neither serialized nor normalized.
        stream.GetStringPacket(out var parsed);
        Assert.Equal(0x12345678u, parsed.UID);
        Assert.Equal(StringId.Effect, parsed.ID);
        Assert.Equal(new[] { "fx" }, parsed.Strings);
        Assert.Equal(14, stream.Position); // Seal remains unread.
        Assert.Equal((byte)0, parsed.StringsLength);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task NullStringsWriteThreeZerosWhileEmptyWritesOnlyTheCount(bool nullStrings) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var message = new LegacyNativeMsgStringPacket { UID = 17, ID = StringId.ServerName,
            Strings = nullStrings ? null! : [] };
        scope.Packet.StringPacketCreate(message);
        Assert.Equal(Convert.FromHexString(nullStrings ? "0C00F407110000003D000000" : "0A00F407110000003D00"),
            NativeStringPacketTestScope.Bytes(scope.Packet));
        scope.Packet.GetStringPacket(out var parsed);
        Assert.Empty(parsed.Strings);
        Assert.Equal(10, scope.Packet.Position); // Null's two extra padding bytes are not consumed.
        Assert.Equal(17u, parsed.UID);
        Assert.Equal(StringId.ServerName, parsed.ID);
    });

    [Fact]
    public Task Utf8LengthsNullEntriesAndEmbeddedNulFollowTheNativeStringList() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        string[] strings = ["é", "中", null!, "", "a\0b"];
        var message = new LegacyNativeMsgStringPacket { UID = 0x12345678, ID = StringId.Effect, Strings = strings };
        scope.Packet.StringPacketCreate(message);
        Assert.Equal(Convert.FromHexString("1C00F407785634120A050200C3A90300E4B8AD000000000300610062"),
            NativeStringPacketTestScope.Bytes(scope.Packet));
        scope.Packet.GetStringPacket(out var parsed);
        Assert.Equal(new[] { "é", "中", "", "", "a" }, parsed.Strings);
        Assert.Equal(28, scope.Packet.Position); // CString truncates returned text, not consumed bytes.
        Assert.Same(strings, message.Strings);
        Assert.Null(strings[2]);
        Assert.Equal("a\0b", strings[4]);
    });

    [Theory]
    [InlineData(255, 255)]
    [InlineData(256, 0)]
    [InlineData(257, 1)]
    public Task ByteCountWrapDoesNotStopWritingAllEntriesInsideOwned16Kb(int count, int advertised) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var message = new LegacyNativeMsgStringPacket { ID = StringId.GuildList,
            Strings = Enumerable.Repeat("x", count).ToArray() };
        scope.Packet.StringPacketCreate(message);
        var bytes = NativeStringPacketTestScope.Bytes(scope.Packet);
        Assert.Equal(10 + count * 3, bytes.Length);
        Assert.Equal((byte)advertised, bytes[9]);
        for (int index = 0; index < count; index++)
            Assert.Equal(new byte[] { 1, 0, (byte)'x' }, bytes.AsSpan(10 + index * 3, 3).ToArray());
        scope.Packet.GetStringPacket(out var parsed);
        Assert.Equal(advertised, parsed.Strings.Length);
        Assert.All(parsed.Strings, text => Assert.Equal("x", text));
        Assert.Equal(10 + advertised * 3, scope.Packet.Position);
    });

    [Fact]
    public Task LocationEffectWritesCoordinatesButNativeReaderReturnsCombinedUidAndDefaultCoordinates() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var message = new LegacyNativeMsgStringPacket { ID = StringId.LocationEffect, UID = 0xDEADBEEF,
            X = 0x1234, Y = 0xABCD, StringsLength = 99, Strings = ["spot"] };
        scope.Packet.StringPacketCreate(message);
        Assert.Equal(Convert.FromHexString("1000F4073412CDAB0901040073706F74"), NativeStringPacketTestScope.Bytes(scope.Packet));
        scope.Packet.GetStringPacket(out var parsed);
        Assert.Equal(0xABCD1234u, parsed.UID);
        Assert.Equal(StringId.LocationEffect, parsed.ID);
        Assert.Equal((ushort)0, parsed.X);
        Assert.Equal((ushort)0, parsed.Y);
        Assert.Equal((byte)0, parsed.StringsLength);
        Assert.Equal(new[] { "spot" }, parsed.Strings);
        Assert.Equal(16, scope.Packet.Position);
        Assert.Equal(0xDEADBEEFu, message.UID);
    });

    [Fact]
    public Task ReaderUsesCurrentCursorWithoutHeaderSizeOrEnumValidation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var stream = scope.Packet;
        stream.Seek(40);
        stream.Write(71u);
        stream.Write((byte)255);
        stream.Write(new[] { "z" });
        stream.Size = 0;
        stream.Seek(40);
        stream.GetStringPacket(out var parsed);
        Assert.Equal(71u, parsed.UID);
        Assert.Equal((StringId)255, parsed.ID);
        Assert.Equal(new[] { "z" }, parsed.Strings);
        Assert.Equal(49, stream.Position);
        Assert.Equal(0, stream.Size);
        stream.StringPacketCreate(new LegacyNativeMsgStringPacket { UID = parsed.UID, ID = parsed.ID, Strings = [] });
        Assert.Equal((byte)255, NativeStringPacketTestScope.Bytes(stream)[8]);
    });

    [Fact]
    public Task OversizedDeclaredCStringReturnsEmptyAfterPublishingFieldsWithoutUnsafeRead() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var stream = scope.Packet;
        stream.Seek(4);
        stream.Write(91u);
        stream.Write((byte)StringId.Sound);
        stream.Write((byte)1);
        stream.Write(ushort.MaxValue);
        stream.Seek(4);
        stream.GetStringPacket(out var parsed);
        Assert.Equal(91u, parsed.UID);
        Assert.Equal(StringId.Sound, parsed.ID);
        Assert.Equal(new[] { "" }, parsed.Strings);
        Assert.Equal(12, stream.Position); // Failed bounded CString read does not advance.
    });

    [Fact]
    public Task EndOfOwnedBufferReturnsPartialFieldsWithCountReadGuardHoldingCursor() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var stream = scope.Packet;
        stream.Seek(16378);
        stream.Write(0x76543210u);
        stream.Write((byte)StringId.Spouse);
        stream.Seek(16378);
        stream.GetStringPacket(out var parsed);
        Assert.Equal(0x76543210u, parsed.UID);
        Assert.Equal(StringId.Spouse, parsed.ID);
        Assert.Empty(parsed.Strings);
        Assert.Equal(16383, stream.Position); // No read of the allocation's last byte.
    });

    [Fact]
    public Task NullMessageFaultsAfterWriterResetButBeforeSizeOrHeaderPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var stream = scope.Packet;
        stream.Size = 123;
        stream.Seek(99);
        Assert.Throws<NullReferenceException>(() => stream.StringPacketCreate(null!));
        Assert.Equal(4, stream.Position);
        Assert.Equal(123, stream.Size);
        Assert.Equal(new byte[4], NativeStringPacketTestScope.Bytes(stream)[..4]);
    });

    [Fact]
    public Task NullReaderPublishesANewMutableResultBeforeTheFirstNativeDereference() => NativePacketTestThread.Run(() =>
    {
        LegacyNativePacket stream = null!;
        var before = new LegacyNativeMsgStringPacket { UID = 123 };
        var result = before;
        Assert.Throws<NullReferenceException>(() => stream.GetStringPacket(out result));
        Assert.NotSame(before, result);
        Assert.Equal(0u, result.UID);
        Assert.Null(result.Strings);
        Assert.Equal((StringId)0, result.ID);
    });
}

// Shared only by these two test files: real owned16KB storage, exact seal restoration,
// and an explicitly seeded native queue MODEL, never SecuritySocket.Create/an OS send pump.
internal sealed class NativeStringPacketTestScope : IDisposable
{
    private static readonly FieldInfo SealField = typeof(LegacyNativePacket)
        .GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly object? priorSeal;
    internal LegacyNativePacket Packet { get; } = new(LegacyNativePacket.MAX_SIZE);

    internal NativeStringPacketTestScope(bool sealedFrame = false)
    {
        priorSeal = SealField.GetValue(null);
        SealField.SetValue(null, sealedFrame ? "TQServer" : null);
        Packet.ZeroFill(16383);
        Packet.Seek(0);
    }

    internal static LegacyNativeGameClientRuntime Owner(Queue<byte[]> queue)
    {
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue);
        Assert.Null(socket.Connection);
        Assert.Null(socket.Crypto);
        return new LegacyNativeGameClientRuntime { Socket = socket };
    }

    internal static unsafe byte[] Bytes(LegacyNativePacket stream)
    {
        var bytes = new byte[stream.Size];
        Marshal.Copy((IntPtr)stream.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    public void Dispose()
    {
        Packet.Dispose();
        SealField.SetValue(null, priorSeal);
    }
}
