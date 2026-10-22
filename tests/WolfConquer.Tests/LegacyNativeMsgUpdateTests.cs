using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using NativeDataType = WolfConquer.Core.World.LegacyNativeMsgUpdate.DataType;
using NativeUpdate = WolfConquer.Core.World.LegacyNativeMsgUpdate;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeMsgUpdateTests
{
    [Fact]
    public Task NativeDataTypeKeepsFlagsUInt32AndLiteralSourceMemberNames() => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(NativeDataType)));
        Assert.True(typeof(NativeDataType).IsDefined(typeof(FlagsAttribute), inherit: false));
        Assert.Equal(91, Enum.GetNames<NativeDataType>().Length);
        Assert.Equal(0x12u, (uint)NativeDataType.DoubleExpTimer);
        Assert.Equal(0x17u, (uint)NativeDataType.VirtutePoints);
        Assert.Equal(0x33u, (uint)NativeDataType.FirsRebornClass);
        Assert.Equal(0x3Eu, (uint)NativeDataType.IncreaseBreack);
        Assert.Equal(0x7Bu, (uint)NativeDataType.NiniaP0);
        Assert.Equal(0x79u, (uint)NativeDataType.ClassExperience);
        Assert.Equal(0x8Au, (uint)NativeDataType.Bloodthirst_Elan);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public Task ConstructorIgnoresNullStreamAndCountAndLeavesNativeMutableDefaults(int count) => NativePacketTestThread.Run(() =>
    {
        var update = new NativeUpdate(null!, 17, count);

        Assert.Equal(17u, update.Info.UID);
        Assert.Null(update.Info.Updates);
        Assert.Empty(update.myUpdates);
        var query = new NativeUpdate.UpdateQuery();
        Assert.Equal((NativeDataType)0, query.Type);
        Assert.Equal(0ul, query.Value);
        Assert.Null(query.Values);
        Assert.Equal(0u, query.Flag);
        Assert.Equal(0u, query.Time);
        Assert.Equal(0u, query.Damage);
        Assert.Equal(0u, query.SpellLevel);
    });

    [Fact]
    public Task AppendReturnsItsSuppliedStreamWithoutUsingConstructorOrAppendCursor() => NativePacketTestThread.Run(() =>
    {
        using var first = Packet();
        using var second = Packet();
        first.Seek(11);
        second.Seek(13);
        var update = new NativeUpdate(first, 1);

        Assert.Same(second, update.Append(second, NativeDataType.ClassExperience, 300L));
        Assert.Same(second, update.Append(second, NativeDataType.ClassExperience, 1u, 2u, 3u, 4u));
        Assert.Same(second, update.Append(second, NativeDataType.ClassExperience, new uint[] { 5, 6 }));

        Assert.Equal(11, first.Position);
        Assert.Equal(13, second.Position);
        Assert.Equal(0, first.Size);
        Assert.Equal(0, second.Size);
        Assert.Equal(3, update.myUpdates.Count);
        Assert.Null(update.Info.Updates);
    });

    [Fact]
    public Task AllAppendOverloadsIgnoreAndReturnNullStream() => NativePacketTestThread.Run(() =>
    {
        var update = new NativeUpdate(null!, 1);

        Assert.Null(update.Append(null!, NativeDataType.ClassExperience, 1L));
        Assert.Null(update.Append(null!, NativeDataType.ClassExperience, 1u, 2u, 3u, 4u));
        Assert.Null(update.Append(null!, NativeDataType.ClassExperience, new uint[] { 0, uint.MaxValue }));

        Assert.Equal(3, update.myUpdates.Count);
        Assert.Equal(1ul, update.myUpdates[0].Value);
        Assert.Equal(1u, update.myUpdates[1].Flag);
        Assert.Equal(2u, update.myUpdates[1].Time);
        Assert.Equal(3u, update.myUpdates[1].Damage);
        Assert.Equal(4u, update.myUpdates[1].SpellLevel);
        Assert.Equal(new ulong[] { 0, uint.MaxValue }, update.myUpdates[2].Values);
    });

    [Fact]
    public Task ArrayAppendEagerlyCopiesEveryValueAndNullArrayFaultDoesNotPublish() => NativePacketTestThread.Run(() =>
    {
        var update = new NativeUpdate(null!, 1);
        uint[] input = [0, 1, uint.MaxValue];
        update.Append(null!, NativeDataType.ClassExperience, input);
        var query = Assert.Single(update.myUpdates);

        input[1] = 99;
        Assert.Equal(new ulong[] { 0, 1, uint.MaxValue }, query.Values);
        Assert.Throws<NullReferenceException>(() =>
            update.Append(null!, NativeDataType.ClassExperience, (uint[])null!));
        Assert.Same(query, Assert.Single(update.myUpdates));
        Assert.Null(update.Info.Updates);
    });

    [Theory]
    [InlineData(-1L, ulong.MaxValue)]
    [InlineData(long.MinValue, 0x8000000000000000ul)]
    public Task SignedScalarAppendUsesUncheckedNativeUnsignedCast(long value, ulong expected) => NativePacketTestThread.Run(() =>
    {
        var update = new NativeUpdate(null!, 1);

        update.Append(null!, NativeDataType.ClassExperience, value);

        Assert.Equal(expected, Assert.Single(update.myUpdates).Value);
    });

    [Fact]
    public Task ReplacedInfoAndListRemainMutableAliasesAndPublicationCopiesOnlyArray() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var oldArray = new NativeUpdate.UpdateQuery[0];
        var info = new NativeUpdate.UpdateProto { UID = 23, Updates = oldArray };
        var query = new NativeUpdate.UpdateQuery
        {
            Type = NativeDataType.ClassExperience,
            Values = [0, 1, uint.MaxValue],
        };
        var list = new List<NativeUpdate.UpdateQuery> { query };
        var update = new NativeUpdate(null!, 99) { Info = info, myUpdates = list };

        Assert.Same(packet, update.GetArray(packet));

        Assert.Same(info, update.Info);
        Assert.Same(list, update.myUpdates);
        Assert.NotSame(oldArray, info.Updates);
        Assert.Same(query, Assert.Single(info.Updates!));
        var publishedBytes = Frame(packet);
        query.Values![1] = 42;
        Assert.Equal(42ul, info.Updates![0].Values![1]);
        Assert.Equal(publishedBytes, Frame(packet));
        list.Clear();
        Assert.Same(query, Assert.Single(info.Updates));
    });

    [Fact]
    public Task EachGetArrayReplacesPublishedArrayWithoutChangingHeldEarlierArray() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 1);
        update.Append(packet, NativeDataType.ClassExperience, 1L);
        update.GetArray(packet);
        var first = update.Info.Updates!;
        var query = Assert.Single(first);

        update.myUpdates.Clear();
        update.GetArray(packet);

        Assert.NotSame(first, update.Info.Updates);
        Assert.Empty(update.Info.Updates!);
        Assert.Same(query, Assert.Single(first));
        Assert.Equal("060080080801", Convert.ToHexString(Frame(packet)));
        Assert.Equal(4, packet.Position);
    });

    [Theory]
    [InlineData(1u, 300L, "0D00800808011205087910AC02")]
    [InlineData(0u, 0L, "0A008008080012020879")]
    [InlineData(uint.MaxValue, 4294967295L, "1400800808FFFFFFFF0F1208087910FFFFFFFF0F")]
    [InlineData(17u, -1L, "150080080811120D087910FFFFFFFFFFFFFFFFFF01")]
    public Task ClassExperienceFramesMatchExactContractGoldensAndExistingValidCodec(uint uid, long value, string hex) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, uid);
        update.Append(packet, NativeDataType.ClassExperience, value);

        Assert.Same(packet, update.GetArray(packet));

        var bytes = Frame(packet);
        Assert.Equal(hex, Convert.ToHexString(bytes));
        Assert.Equal(new LegacyMsgUpdatePacket(uid,
            [new LegacyMsgUpdateEntry((LegacyMsgUpdateDataType)NativeDataType.ClassExperience, unchecked((ulong)value))]).ToFrameBytes(), bytes);
        Assert.Equal(bytes.Length, packet.Size);
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task FlagAppendSerializesFourIndependentFieldsInNativeOrder() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 19);
        update.Append(packet, NativeDataType.ClassExperience, 1u, 2u, 3u, 4u);

        update.GetArray(packet);

        var bytes = Frame(packet);
        Assert.Equal("120080080813120A08792001280230033804", Convert.ToHexString(bytes));
        Assert.Equal(new LegacyMsgUpdatePacket(19,
            [new LegacyMsgUpdateEntry((LegacyMsgUpdateDataType)NativeDataType.ClassExperience, 0, 1, 2, 3, 4)]).ToFrameBytes(), bytes);
    });

    [Fact]
    public Task ValuesAreUnpackedRepeatedVarintsIncludingZero() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 23);
        update.Append(packet, NativeDataType.ClassExperience, new uint[] { 0, 1, uint.MaxValue });

        update.GetArray(packet);

        var bytes = Frame(packet);
        Assert.Equal("140080080817120C08791800180118FFFFFFFF0F", Convert.ToHexString(bytes));
        Assert.Equal(new LegacyMsgUpdatePacket(23,
            [new LegacyMsgUpdateEntry((LegacyMsgUpdateDataType)NativeDataType.ClassExperience, 0,
                Values: new ulong[] { 0, 1, uint.MaxValue })]).ToFrameBytes(), bytes);
    });

    [Fact]
    public Task EmptyUpdatesStillWriteRequiredZeroUidWithoutInventedEntryOrGuard() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 0);

        update.GetArray(packet);

        Assert.Empty(update.Info.Updates!);
        Assert.Equal("060080080800", Convert.ToHexString(Frame(packet)));
        Assert.Equal(6, packet.Size);
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task NullQueryIsRetainedInPublishedArrayButSkippedByTypedSerialization() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 29);
        update.myUpdates.Add(null!);

        update.GetArray(packet);

        Assert.Null(Assert.Single(update.Info.Updates!));
        Assert.Null(Assert.Single(update.myUpdates));
        // Pinned original protobuf-net.dll with source-shaped contracts returned payload081D.
        Assert.Equal("06008008081D", Convert.ToHexString(Frame(packet)));
    });

    [Fact]
    public Task FlagsEnumHighBitValueUsesOriginalProtobufSignedInt32WireEncoding() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = Packet();
        var update = new NativeUpdate(packet, 31);
        var highBitType = (NativeDataType)0xDEADBEEFu;
        update.Append(packet, highBitType, 0L);

        update.GetArray(packet);

        Assert.Equal(highBitType, Assert.Single(update.Info.Updates!).Type);
        // Pinned original protobuf-net.dll plus source-shaped [Flags] enum contract.
        // The existing DTO's unsigned enum writer is not an authority for this case.
        Assert.Equal("13008008081F120B08EFFDB6F5FDFFFFFFFF01", Convert.ToHexString(Frame(packet)));
    });

    [Fact]
    public Task FinalizeAppendsNativeSealButHeaderExcludesItsEightBytesAndCursorReturnsFour() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        LegacyNativePacket.SealString = "TQServer";
        using var packet = Packet();
        var update = new NativeUpdate(packet, 1);
        update.Append(packet, NativeDataType.ClassExperience, 300L);

        update.GetArray(packet);

        Assert.Equal("0D00800808011205087910AC025451536572766572", Convert.ToHexString(Frame(packet)));
        Assert.Equal(21, packet.Size);
        Assert.Equal(8, LegacyNativePacket.SealSize);
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task NullStreamFaultOccursAfterUpdatesArrayPublication() => NativePacketTestThread.Run(() =>
    {
        var update = new NativeUpdate(null!, 1);
        var stale = new NativeUpdate.UpdateQuery[0];
        update.Info.Updates = stale;
        update.Append(null!, NativeDataType.ClassExperience, 1L);

        Assert.Throws<NullReferenceException>(() => update.GetArray(null!));

        Assert.NotSame(stale, update.Info.Updates);
        Assert.Same(update.myUpdates[0], Assert.Single(update.Info.Updates!));
    });

    [Fact]
    public Task NullInfoFaultLeavesValidListAndNativeStreamUntouched() => NativePacketTestThread.Run(() =>
    {
        using var packet = Packet();
        var update = new NativeUpdate(packet, 1);
        update.Append(packet, NativeDataType.ClassExperience, 1L);
        var list = update.myUpdates;
        var query = Assert.Single(list);
        update.Info = null!;

        Assert.Throws<NullReferenceException>(() => update.GetArray(packet));

        Assert.Null(update.Info);
        Assert.Same(list, update.myUpdates);
        Assert.Same(query, Assert.Single(list));
        Assert.Equal(4, packet.Position);
        Assert.Equal(0, packet.Size);
    });

    [Fact]
    public Task NullListFaultLeavesOldPublishedArrayAndNativeStreamUntouched() => NativePacketTestThread.Run(() =>
    {
        using var packet = Packet();
        var update = new NativeUpdate(packet, 1);
        var old = new[] { new NativeUpdate.UpdateQuery() };
        update.Info.Updates = old;
        update.myUpdates = null!;

        Assert.Throws<NullReferenceException>(() => update.GetArray(packet));

        Assert.Same(old, update.Info.Updates);
        Assert.Null(update.myUpdates);
        Assert.Equal(4, packet.Position);
        Assert.Equal(0, packet.Size);
    });

    private static LegacyNativePacket Packet()
    {
        var packet = new LegacyNativePacket(512);
        packet.InitWriter();
        return packet;
    }

    private static unsafe byte[] Frame(LegacyNativePacket packet)
    {
        Assert.InRange(packet.Size, 0, 512);
        return new ReadOnlySpan<byte>(packet.Memory, packet.Size).ToArray();
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(LegacyNativePacket).GetField(
            "seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Field.GetValue(null);

        public SealScope() => Field.SetValue(null, null);

        public void Dispose() => Field.SetValue(null, previous);
    }
}
