using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(0U)] [InlineData(250U)] [InlineData(65786U)] [InlineData(273344U)] [InlineData(uint.MaxValue)]
    public Task FullUInt32StanceCommitsBeforeSourceOrderedSkinAndLow16EchoWithoutBecomingAnotherAction(uint value) => Run(async f =>
    {
        var files = f.Data.Files(); var spells = f.Native.MySpells!.ClientSpells;
        f.BeforeSend = (_, _) => AssertFilesAndOwner(f, files, value);
        try { await f.StanceAsync(value); } finally { f.BeforeSend = null; }
        AssertFilesAndOwner(f, files, value); Assert.Same(spells, f.Native.MySpells.ClientSpells);
        Assert.Equal(new[] { Uid, Peer, Uid, Peer }, f.Order.Select(row => row.Recipient));
        Assert.Equal(new[] { SkinGolden(), SkinGolden(), StanceGolden(value), StanceGolden(value) }, f.Order.Select(row => row.Bytes));
        await Tick(f, value == 250 ? (ushort)15 : (ushort)5);
        await Tick(f, value == 250 ? (ushort)15 : (ushort)5, held: true);
    });

    [Theory]
    [InlineData(65786U)] [InlineData(273344U)] [InlineData(uint.MaxValue)]
    public Task RealSpawnRequestProjectsOnlyTheSourceLow16ActionValueAndPreservesTheWholeBeforePacket(uint value) => Run(async f =>
    {
        var query = new MsgActionPacket { ObjId = 999, DwParam = Uid, Timestamp = 123456, Type = MsgActionType.QuerySpawn }.ToFrameBytes();
        await f.SendAsync(query, Peer); var before = Assert.Single(f.PeerSent); ClearOutput(f);
        await f.StanceAsync(value); var files = f.Data.Files(); ClearOutput(f);
        await f.SendAsync(query, Peer);
        var after = Assert.Single(f.PeerSent); Assert.Equal(ReplaceSpawnAction(before, value), after);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(Read(after), out var parsed)); Assert.Equal((ulong)(ushort)value, parsed.GetNumericField(31));
        AssertFilesAndOwner(f, files, value);
    });

    [Theory]
    [InlineData(250U, 25)] [InlineData(65786U, 15)]
    public Task TheActualQueryMintedFanUsesTheFullActionAndStanceNeverResetsEitherClock(uint value, int first) => Run(async f =>
    {
        await f.EquipFanAsync(); Assert.True(f.Native.Player!.BitVector.ArrayFlags.TryGetValue(194, out var row));
        var timer = row!.Timer; ClearOutput(f); await f.StanceAsync(value); ClearOutput(f);
        var files = f.Data.Files(); await f.TickAsync(); Assert.Equal((ushort)first, Stamina(f));
        AssertCoherent(f, value); f.Data.AssertFiles(files);
        Assert.Equal(StaminaGolden(checked((ushort)first)), f.Sent[^1]);
        Assert.Equal(2, f.Sent.Count); Assert.Single(f.PeerSent); // Actual fan effect precedes the owner scalar.
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
        ClearOutput(f); await f.StanceAsync(value); ClearOutput(f); await Tick(f, checked((ushort)first), held: true);
    });

    private static byte[] ReplaceSpawnAction(byte[] before, uint action)
    {
        // Independent raw BEFORE packet delta, not a regenerated AFTER spawn.
        // Preserve every field/opaque byte except the one source-proven value.
        var body = new List<byte>(); int offset = 4, found = 0;
        while (offset < before.Length)
        {
            int start = offset; ulong tag = ReadVarint(before, ref offset); int tagEnd = offset;
            int wire = (int)(tag & 7), field = checked((int)(tag >> 3));
            switch (wire)
            {
                case 0: ReadVarint(before, ref offset); break;
                case 1: offset = checked(offset + 8); break;
                case 2: int length = checked((int)ReadVarint(before, ref offset)); offset = checked(offset + length); break;
                case 5: offset = checked(offset + 4); break;
                default: throw new InvalidDataException("unexpectedSpawnWireType");
            }
            Assert.InRange(offset, tagEnd, before.Length);
            if (field == 31) { found++; Assert.Equal(0, wire); body.AddRange(before[start..tagEnd]); Varint(body, (ushort)action); }
            else body.AddRange(before[start..offset]);
        }
        Assert.Equal(1, found); return Frame(BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(2)), body);
    }
    private static ulong ReadVarint(byte[] bytes, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            Assert.InRange(offset, 0, bytes.Length - 1); byte next = bytes[offset++]; value |= (ulong)(next & 127) << shift;
            if ((next & 128) == 0) return value;
        }
        throw new InvalidDataException("invalidSpawnVarint");
    }
}
