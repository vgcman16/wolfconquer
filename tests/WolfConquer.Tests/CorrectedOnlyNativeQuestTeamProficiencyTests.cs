using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using QuestMode = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestMode;
using QuestStatus = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;
using TeamType = WolfConquer.Core.World.LegacyNativeTeamPacket.TeamTypes;

namespace WolfConquer.Tests;

// Corrected-only owned-buffer controls. Never run these malformed/small-buffer
// inputs against the old unsafe codecs to manufacture a red baseline.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class CorrectedOnlyNativeQuestTeamProficiencyTests
{
    [Theory]
    [InlineData("data", 0)] [InlineData("data", 3)] [InlineData("data", 4)]
    [InlineData("data", 7)] [InlineData("data", 8)] [InlineData("data", 11)]
    [InlineData("list", 0)] [InlineData("list", 1)] [InlineData("list", 2)]
    [InlineData("list", 3)] [InlineData("list", 4)] [InlineData("list", 7)]
    [InlineData("list", 8)] [InlineData("list", 11)] [InlineData("list", 12)] [InlineData("list", 15)]
    [InlineData("team", 0)] [InlineData("team", 3)] [InlineData("team", 4)] [InlineData("team", 7)]
    [InlineData("proficiency", 0)] [InlineData("proficiency", 3)] [InlineData("proficiency", 4)]
    [InlineData("proficiency", 7)] [InlineData("proficiency", 8)] [InlineData("proficiency", 11)]
    [InlineData("proficiency", 12)] [InlineData("proficiency", 15)]
    public Task IncompleteRecordPreservesAllCallerOutputsAndTheInitialCursor(string codec, int count) =>
        NativePacketTestThread.Run(() =>
        {
            byte[] input = new byte[] { 0xA5, 0xCC, 0xEE }.Concat(Record(codec).Take(count)).ToArray();
            using var packet = Exact(input); packet.Seek(3);
            var outputs = new Outputs(); byte[] before = Raw(packet);
            Assert.Throws<EndOfStreamException>(() => outputs.Read(packet, codec));
            outputs.AssertUnchanged(); Assert.Equal(3, packet.Position);
            Assert.Equal(input.Length, packet.Size); Assert.Equal(before, Raw(packet));
        });

    [Theory]
    [InlineData("data")] [InlineData("list")] [InlineData("team")] [InlineData("proficiency")]
    public Task MissingPacketRefusesBeforePublishingAnyCallerOutput(string codec) => NativePacketTestThread.Run(() =>
    {
        var outputs = new Outputs();
        Assert.Throws<ArgumentNullException>(() => outputs.Read(null!, codec));
        outputs.AssertUnchanged();
    });

    [Theory]
    [InlineData("data")] [InlineData("list")] [InlineData("team")] [InlineData("proficiency")]
    public Task DisposedPacketRefusesWithoutReplacingOutputsOrResurrectingStorage(string codec) =>
        NativePacketTestThread.Run(() =>
        {
            using var packet = Exact(Record(codec)); packet.Seek(1);
            nuint cursor = (nuint)packet.stream; int size = packet.Size; packet.Dispose();
            var outputs = new Outputs();
            Assert.Throws<ObjectDisposedException>(() => outputs.Read(packet, codec));
            outputs.AssertUnchanged(); Assert.Equal(cursor, (nuint)packet.stream);
            Assert.Equal(size, packet.Size); Assert.Equal((nuint)0, (nuint)packet.Memory);
        });

    [Fact]
    public Task IndependentBodyGoldensConsumeTheirExactTinyAllocationsAndRetainUnknownBits() =>
        NativePacketTestThread.Run(() =>
        {
            using var data = Exact(Record("data"));
            data.GetQuestData(out var unknown, out var uid, out var intents);
            Assert.Equal(0x11223344u, unknown); Assert.Equal(0xAABBCCDDu, uid);
            Assert.Equal(new uint[] { 0x87654321 }, intents);
            Assert.Equal(12, data.Position); Assert.Equal(12, data.Capacity); Assert.Equal(Record("data"), Raw(data));

            using var list = Exact(Record("list"));
            list.GetQuestList(out var mode, out var count, out var questId, out var status, out var timer);
            Assert.Equal((QuestMode)ushort.MaxValue, mode); Assert.Equal((ushort)65534, count);
            Assert.Equal(0xAABBCCDDu, questId); Assert.Equal((QuestStatus)0xDEADBEEFu, status);
            Assert.Equal(0x87654321u, timer); Assert.Equal(16, list.Position);
            Assert.Equal(16, list.Capacity); Assert.Equal(Record("list"), Raw(list));

            using var team = Exact(Record("team"));
            team.GetTeamPacket(out var type, out var teamUid);
            Assert.Equal((TeamType)0xFEDCBA98u, type); Assert.Equal(0x87654321u, teamUid);
            Assert.Equal(8, team.Position); Assert.Equal(8, team.Capacity); Assert.Equal(Record("team"), Raw(team));

            using var proficiency = Exact(Record("proficiency"));
            proficiency.GetProficiency(out var result);
            Assert.Equal(0x11223344u, result.ID); Assert.Equal(0xAABBCCDDu, result.Level);
            Assert.Equal(0xDEADBEEFu, result.Experience); Assert.Equal(0x87654321u, result.NeededExperience);
            Assert.Equal((byte)0, result.PreviouseLevel); Assert.Equal(16, proficiency.Position);
            Assert.Equal(16, proficiency.Capacity); Assert.Equal(Record("proficiency"), Raw(proficiency));
        });

    [Theory]
    [InlineData(23, false)] [InlineData(24, true)]
    public Task QuestListHeaderWritesItsCompleteExplicitBodyOrPreservesTheOldTarget(int capacity, bool fits) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new SealScope(false); using var packet = Target(capacity);
            byte[] before = Raw(packet);
            if (!fits)
            {
                Assert.Throws<InvalidOperationException>(() => packet.QuestListCreate(QuestMode.Review, 17));
                Assert.Equal(before, Raw(packet)); Assert.Equal(2, packet.Position); Assert.Equal(3, packet.Size);
                return;
            }
            Assert.Same(packet, packet.QuestListCreate(QuestMode.Review, 17));
            byte[] expectedBody = Convert.FromHexString("03001100" + "00000000000000000000000000000000");
            Assert.Equal(expectedBody, Raw(packet)[4..]); Assert.Equal(before[..4], Raw(packet)[..4]);
            Assert.Equal(24, packet.Position); Assert.Equal(3, packet.Size);
        });

    [Theory]
    [InlineData(35, false)] [InlineData(36, true)]
    public Task QuestRowAppendsAllTwelveBytesOrLeavesTheEarlierSuccessfulHeaderUntouched(int capacity, bool fits) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new SealScope(false); using var packet = Target(capacity);
            packet.QuestListCreate(QuestMode.Review, 17);
            var row = new LegacyNativeMsgQuestList.QuestListItem
                { UID = 0x12345678, Status = (QuestStatus)uint.MaxValue, Time = 0xABCDEF12, Intentions = null! };
            byte[] before = Raw(packet);
            if (!fits)
            {
                Assert.Throws<InvalidOperationException>(() => packet.AddItemQuestList(row));
                Assert.Equal(before, Raw(packet)); Assert.Equal(24, packet.Position); Assert.Equal(3, packet.Size);
            }
            else
            {
                Assert.Same(packet, packet.AddItemQuestList(row));
                Assert.Equal(36, packet.Position); Assert.Equal(3, packet.Size);
                packet.QuestListFinalize();
                byte[] expected = Convert.FromHexString("2400480803001100" + "00000000000000000000000000000000" + "78563412FFFFFFFF12EFCDAB");
                Assert.Equal(expected, Raw(packet)); Assert.Equal(36, packet.Size); Assert.Equal(4, packet.Position);
            }
            Assert.Equal(0x12345678u, row.UID); Assert.Equal((QuestStatus)uint.MaxValue, row.Status);
            Assert.Equal(0xABCDEF12u, row.Time); Assert.Null(row.Intentions);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task UnrepresentableQuestIntentCountRefusesWithoutWrappingOrChangingTheSource(bool owner) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new SealScope(false); using var packet = Target(48);
            uint[] values = Enumerable.Repeat(7u, 65_536).ToArray(); byte[] before = Raw(packet);
            Assert.Throws<ArgumentOutOfRangeException>(() => WriteData(packet, owner, values));
            Assert.Equal(before, Raw(packet)); Assert.Equal(2, packet.Position); Assert.Equal(3, packet.Size);
            Assert.Equal(65_536, values.Length); Assert.All(values, value => Assert.Equal(7u, value));
        });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public Task QuestDataFullFramesUseExactCapacityAndPreserveOneByteShortTargets(bool owner, bool sealedFrame) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new SealScope(sealedFrame);
            byte[] expected = Convert.FromHexString((owner ? "10005808000001001100000007000000" :
                "1400580800000100110000000700000000000000") + (sealedFrame ? "5451536572766572" : ""));
            uint[] values = [7]; using var exact = Target(expected.Length);
            Assert.Same(exact, WriteData(exact, owner, values));
            Assert.Equal(expected, Raw(exact)); Assert.Equal(expected.Length, exact.Size); Assert.Equal(4, exact.Position);
            using var shortTarget = Target(expected.Length - 1); byte[] before = Raw(shortTarget);
            Assert.Throws<InvalidOperationException>(() => WriteData(shortTarget, owner, values));
            Assert.Equal(before, Raw(shortTarget)); Assert.Equal(2, shortTarget.Position); Assert.Equal(3, shortTarget.Size);
            Assert.Equal(new uint[] { 7 }, values);
        });

    [Theory]
    [InlineData("data")] [InlineData("owner")] [InlineData("header")] [InlineData("row")]
    public Task MissingAndDisposedQuestTargetsRefuseWithoutChangingRetainedMetadata(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var seal = new SealScope(false);
            Assert.Throws<ArgumentNullException>(() => WriteQuest(null!, operation));
            using var packet = Target(48); nuint cursor = (nuint)packet.stream; packet.Dispose();
            Assert.Throws<ObjectDisposedException>(() => WriteQuest(packet, operation));
            Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(3, packet.Size); Assert.Equal((nuint)0, (nuint)packet.Memory);
        });

    private static Packet WriteData(Packet packet, bool owner, uint[] values) => owner
        ? packet.MsgQuestDataCreate(uint.MaxValue, 17, uint.MaxValue, values)
        : packet.MsgQuestDataCreate(uint.MaxValue, 17, values);

    private static void WriteQuest(Packet packet, string operation)
    {
        switch (operation)
        {
            case "data": WriteData(packet, false, [7]); break;
            case "owner": WriteData(packet, true, [7]); break;
            case "header": packet.QuestListCreate(QuestMode.Review, 17); break;
            case "row": packet.AddItemQuestList(new() { UID = 1, Status = QuestStatus.Available, Time = 3 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static byte[] Record(string codec) => Convert.FromHexString(codec switch
    {
        "data" => "44332211DDCCBBAA21436587",
        "list" => "FFFFFEFFDDCCBBAAEFBEADDE21436587",
        "team" => "98BADCFE21436587",
        "proficiency" => "44332211DDCCBBAAEFBEADDE21436587",
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    });

    private static Packet Exact(byte[] input)
    {
        var packet = new Packet(Math.Max(1, input.Length));
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);
        if (input.Length > 0) Marshal.Copy(input, 0, (nint)packet.Memory, input.Length);
        packet.Size = input.Length; // The actual copied source; a spare byte is not input.
        return packet;
    }

    private static Packet Target(int capacity)
    {
        var packet = new Packet(capacity);
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(), 0, (nint)packet.Memory, capacity);
        packet.Size = 3; packet.Seek(2);
        return packet;
    }

    private static byte[] Raw(Packet packet)
    {
        byte[] result = new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory, result, 0, result.Length); return result;
    }

    private sealed class Outputs
    {
        private uint unknown = 91, uid = 92, questId = 96, timer = 98, teamUid = 100;
        private QuestMode mode = (QuestMode)94;
        private ushort count = 95;
        private QuestStatus status = (QuestStatus)97;
        private TeamType team = (TeamType)99;
        private readonly uint[] priorIntents = [93];
        private uint[] intents;
        private readonly LegacyNativeMsgProficiency priorProficiency = new()
            { ID = 101, Level = 102, Experience = 103, NeededExperience = 104, PreviouseLevel = 105 };
        private LegacyNativeMsgProficiency proficiency;

        internal Outputs() { intents = priorIntents; proficiency = priorProficiency; }
        internal void Read(Packet packet, string codec)
        {
            switch (codec)
            {
                case "data": packet.GetQuestData(out unknown, out uid, out intents); break;
                case "list": packet.GetQuestList(out mode, out count, out questId, out status, out timer); break;
                case "team": packet.GetTeamPacket(out team, out teamUid); break;
                case "proficiency": packet.GetProficiency(out proficiency); break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        internal void AssertUnchanged()
        {
            Assert.Equal(91u, unknown); Assert.Equal(92u, uid); Assert.Same(priorIntents, intents); Assert.Equal(new uint[] { 93 }, intents);
            Assert.Equal((QuestMode)94, mode); Assert.Equal((ushort)95, count); Assert.Equal(96u, questId);
            Assert.Equal((QuestStatus)97, status); Assert.Equal(98u, timer); Assert.Equal((TeamType)99, team); Assert.Equal(100u, teamUid);
            Assert.Same(priorProficiency, proficiency); Assert.Equal(101u, proficiency.ID); Assert.Equal(102u, proficiency.Level);
            Assert.Equal(103u, proficiency.Experience); Assert.Equal(104u, proficiency.NeededExperience); Assert.Equal((byte)105, proficiency.PreviouseLevel);
        }
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(Packet).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Seal.GetValue(null);
        internal SealScope(bool enabled) => Seal.SetValue(null, enabled ? "TQServer" : null);
        public void Dispose() => Seal.SetValue(null, previous);
    }
}
