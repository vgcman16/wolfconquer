using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaMatchPacketTests
{
    private static byte[] Rank10Bytes(params LegacyNativeArenaUser[] rows)
    {
        byte[] bytes = new byte[12 + 68 * rows.Length]; U16(bytes, 0, (ushort)bytes.Length); U16(bytes, 2, 2293);
        U32(bytes, 4, 10); U32(bytes, 8, 1);
        for (int i = 0; i < rows.Length; i++)
        {
            int offset = 12 + 68 * i; var row = rows[i];
            U32(bytes, offset, row.UID); Ascii32(bytes, offset + 4, row.Name); U32(bytes, offset + 36, row.Mesh);
            U32(bytes, offset + 40, row.Level); U32(bytes, offset + 44, row.Class);
            U32(bytes, offset + 48, row.LastSeasonRank); U32(bytes, offset + 52, row.LastSeasonRank);
            U32(bytes, offset + 56, row.LastSeasonArenaPoints); U32(bytes, offset + 60, row.LastSeasonWin);
            U32(bytes, offset + 64, row.LastSeasonLose);
        }
        return bytes;
    }

    [Fact]
    public Task EntireRank10CodecWritesBothRankCopiesAndDoesNotReadOrRequireArenaInfo() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(42); row.Info = null!;
        Assert.Same(scope.Packet, scope.Packet.ArenaRank10Create(LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList));
        Assert.Equal(12, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Same(scope.Packet, scope.Packet.AddItemArenaRank10(row)); Assert.Equal(80, scope.Packet.Position);
        Assert.Same(scope.Packet, scope.Packet.ArenaRank10Finalize());
        Assert.Equal(Rank10Bytes(row), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task FullPrivateRank10ReceiverSendsAnEmptyNativeHeaderFromTheActualStaticRankArray() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); InvokePrivate<LegacyNativeMsgArenaRank10>(scope.Client, scope.Packet);
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgArenaRank10>());
        Assert.Equal(LayoutKind.Sequential, typeof(LegacyNativeMsgArenaRank10).StructLayoutAttribute!.Value);
        Assert.Equal(Rank10Bytes(), Assert.Single(scope.Queue)); Assert.Equal(12, scope.Packet.Size);
    });

    [Fact]
    public Task FullRank10ReceiverFiltersHolesButKeepsPhysicalArrayOrderAliasesAndNullInfoRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var a = Row(1); var b = Row(2); a.Info = null!;
        LegacyNativeArenaRankings.Top10[2] = b; LegacyNativeArenaRankings.Top10[5] = a; LegacyNativeArenaRankings.Top10[9] = b;
        InvokePrivate<LegacyNativeMsgArenaRank10>(scope.Client, scope.Packet);
        Assert.Equal(Rank10Bytes(b, a, b), Assert.Single(scope.Queue));
        Assert.Same(b, LegacyNativeArenaRankings.Top10[2]); Assert.Null(LegacyNativeArenaRankings.Top10[0]);
    });

    [Fact]
    public Task FullRank10ReceiverDoesNotInventAnAdditionalTenRowCapOnAMutableLongerArray() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var rows = Enumerable.Range(1, 11).Select(x => Row((uint)x)).ToArray();
        LegacyNativeArenaRankings.Top10 = rows;
        InvokePrivate<LegacyNativeMsgArenaRank10>(scope.Client, scope.Packet);
        Assert.Equal(Rank10Bytes(rows), Assert.Single(scope.Queue)); Assert.Equal(760, scope.Packet.Size);
    });

    [Fact]
    public Task FullRank10ReceiverCatchesNativeNameFaultAfterUidPrefixWithoutSendingOrFinalizing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(7); row.Name = null!; LegacyNativeArenaRankings.Top10[0] = row;
        InvokePrivate<LegacyNativeMsgArenaRank10>(scope.Client, scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(16, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(7u, BitConverter.ToUInt32(NativeSpellTestFixture.Bytes(scope.Packet), 12)); Assert.Null(row.Name);
    });

    [Fact]
    public Task FullRank10ReceiverCatchesNullArrayBeforeWriterInitializationAndPreservesCallerCursor() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeArenaRankings.Top10 = null!;
        InvokePrivate<LegacyNativeMsgArenaRank10>(scope.Client, scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Null(LegacyNativeArenaRankings.Top10);
    });
}
