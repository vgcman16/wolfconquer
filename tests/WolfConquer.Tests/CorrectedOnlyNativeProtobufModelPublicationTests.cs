using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Records = WolfConquer.Core.World.LegacyNativeMsgItemRefineRecord;
using Titles = WolfConquer.Core.World.LegacyNativeMsgTitleStorage;
using Ranks = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow;

namespace WolfConquer.Tests;

// Corrected-only outer-publication controls using genuinely copied owned bytes.
// No old unsafe native implementation is executed for a red baseline.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class CorrectedOnlyNativeProtobufModelPublicationTests
{
    [Theory]
    [InlineData("refine", "short-header")] [InlineData("refine", "declared-tail")]
    [InlineData("refine", "truncated")] [InlineData("refine", "nested-tail")]
    [InlineData("title", "short-header")] [InlineData("title", "declared-tail")]
    [InlineData("title", "truncated")] [InlineData("title", "nested-tail")]
    [InlineData("rank", "short-header")] [InlineData("rank", "declared-tail")]
    [InlineData("rank", "truncated")] [InlineData("rank", "nested-tail")]
    public Task FailedFrameOrPayloadRetainsTheOriginalOuterAndNestedGraph(string codec, string fault) =>
        NativePacketTestThread.Run(() =>
        {
            byte[] input = Fault(codec, fault);
            using var packet = Exact(input);
            packet.Seek(1);
            var callers = new CallerModels();
            byte[] before = Raw(packet);
            nuint cursor = (nuint)packet.stream;

            if (fault is "short-header" or "declared-tail")
                Assert.Throws<InvalidDataException>(() => callers.Read(packet, codec));
            else Assert.Throws<EndOfStreamException>(() => callers.Read(packet, codec));

            callers.AssertUnchanged(codec);
            Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(input.Length, packet.Size);
            Assert.Equal(before, Raw(packet));
        });

    [Theory]
    [InlineData("refine")] [InlineData("title")] [InlineData("rank")]
    public Task NullRequiredPacketCannotPublishADefaultOuterModel(string codec) => NativePacketTestThread.Run(() =>
    {
        var callers = new CallerModels();
        Assert.Throws<ArgumentNullException>(() => callers.Read(null!, codec));
        callers.AssertUnchanged(codec);
    });

    [Theory]
    [InlineData("refine")] [InlineData("title")] [InlineData("rank")]
    public Task NullPriorOutReferenceStaysNullOnTruncatedPayload(string codec) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Fault(codec, "truncated");
        using var packet = Exact(input); packet.Seek(1);
        var callers = new CallerModels(); callers.Clear(codec);
        Assert.Throws<EndOfStreamException>(() => callers.Read(packet, codec));
        Assert.Null(callers.Selected(codec));
        Assert.Equal(1, packet.Position); Assert.Equal(input.Length, packet.Size); Assert.Equal(input, Raw(packet));
    });

    [Theory]
    [InlineData("refine")] [InlineData("title")] [InlineData("rank")]
    public Task DisposedStorageCannotReplaceThePriorModelOrItsGraph(string codec) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Fault(codec, "truncated");
        using var packet = Exact(input); packet.Seek(1);
        nuint cursor = (nuint)packet.stream; packet.Dispose();
        var callers = new CallerModels();
        Assert.Throws<ObjectDisposedException>(() => callers.Read(packet, codec));
        callers.AssertUnchanged(codec);
        Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(input.Length, packet.Size);
        Assert.Equal((nuint)0, (nuint)packet.Memory);
    });

    private static byte[] Fault(string codec, string fault)
    {
        if (fault == "short-header") return [4, 0, 0xFF];
        byte[] payload = fault == "nested-tail" ? Convert.FromHexString(codec switch
        {
            "refine" => "080710081809200A2A0108",
            "title" => "0805100718A11F20032A0108",
            "rank" => "0801120108",
            _ => throw new ArgumentOutOfRangeException(nameof(codec)),
        }) : [0x08];
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 0xCAFE);
        payload.CopyTo(frame, 4);
        if (fault == "declared-tail") BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)(frame.Length + 1)));
        return frame;
    }

    private static Packet Exact(byte[] input)
    {
        var packet = new Packet(input.Length);
        Marshal.Copy(input, 0, (nint)packet.Memory, input.Length);
        packet.Size = input.Length; // Captured managed source length, never Capacity/header-derived.
        return packet;
    }

    private static byte[] Raw(Packet packet)
    {
        var result = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, result, 0, result.Length);
        return result;
    }

    private sealed class CallerModels
    {
        private Records.ItemRefineRecord refine;
        private Titles.TitleStorage title;
        private Ranks.MsgRankMemberShowProto rank;
        private readonly Records.ItemRefineRecord originalRefine;
        private readonly Titles.TitleStorage originalTitle;
        private readonly Ranks.MsgRankMemberShowProto originalRank;
        private readonly Records.RefineRecord[] refineRows;
        private readonly Titles.Title titleRow;
        private readonly Ranks.MsgRankMemberShowProto.Entry[] rankRows;

        internal CallerModels()
        {
            refineRows = [new() { Index=1,ItemUID=2,ItemID=3,PurificationID=4,OpenStars=5,RequestStars=6,str="prior" }];
            refine = originalRefine = new() { Type=91,Points=92,UnKnow=93,Unlocked=94,msgs=refineRows };
            titleRow = new() { ID=101,SubId=102,dwparam1=103,dwparam2=104 };
            title = originalTitle = new() { ActionID=Titles.Action.UnEquip,dwparam1=111,dwparam2=112,dwparam3=113,Title=titleRow };
            rankRows = [new() { EntityUID=121,Name="prior-name",GuildName="prior-guild",Rank=122 }];
            rank = originalRank = new() { Type=123,Items=rankRows };
        }

        internal void Read(Packet packet, string codec)
        {
            switch (codec)
            {
                case "refine": packet.GetItemRefineRecord(out refine); break;
                case "title": packet.GetTitleStorage(out title); break;
                case "rank": packet.GetRankMemberShow(out rank); break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        internal void Clear(string codec)
        {
            switch (codec)
            {
                case "refine": refine = null!; break;
                case "title": title = null!; break;
                case "rank": rank = null!; break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        internal object? Selected(string codec) => codec switch
        {
            "refine" => refine, "title" => title, "rank" => rank,
            _ => throw new ArgumentOutOfRangeException(nameof(codec)),
        };

        internal void AssertUnchanged(string codec)
        {
            switch (codec)
            {
                case "refine":
                    Assert.Same(originalRefine, refine); Assert.Same(refineRows, refine.msgs);
                    Assert.Equal(91u, refine.Type); Assert.Equal(92u, refine.Points);
                    Assert.Equal(93u, refine.UnKnow); Assert.Equal(94u, refine.Unlocked);
                    var row = Assert.Single(refine.msgs!); Assert.Same(refineRows[0], row);
                    Assert.Equal(new uint[] { 1,2,3,4,5,6 }, new[] { row.Index,row.ItemUID,row.ItemID,row.PurificationID,row.OpenStars,row.RequestStars });
                    Assert.Equal("prior", row.str);
                    break;
                case "title":
                    Assert.Same(originalTitle, title); Assert.Same(titleRow, title.Title);
                    Assert.Equal(Titles.Action.UnEquip, title.ActionID);
                    Assert.Equal(new uint[] { 111,112,113 }, new[] { title.dwparam1,title.dwparam2,title.dwparam3 });
                    Assert.Equal(new uint[] { 101,102,103,104 }, new[] { title.Title.ID,title.Title.SubId,title.Title.dwparam1,title.Title.dwparam2 });
                    break;
                case "rank":
                    Assert.Same(originalRank, rank); Assert.Same(rankRows, rank.Items);
                    Assert.Same(rankRows[0], Assert.Single(rank.Items));
                    Assert.Equal(123u, rank.Type); Assert.Equal(121u, rank.Items[0].EntityUID);
                    Assert.Equal(122u, rank.Items[0].Rank); Assert.Equal("prior-name", rank.Items[0].Name);
                    Assert.Equal("prior-guild", rank.Items[0].GuildName);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }
    }
}
