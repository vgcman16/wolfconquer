using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using AchievementAction = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;
using ArenaDialog = WolfConquer.Core.World.LegacyNativeMsgArenaSignup.DialogType;
using ArenaOption = WolfConquer.Core.World.LegacyNativeMsgArenaSignup.DialogButton;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Replay = WolfConquer.Core.World.LegacyNativeNpcServerReplay;

namespace WolfConquer.Tests;

// Corrected-only complete-codec controls. Every native byte is owned and initialized;
// no old unsafe/OOB implementation is executed to manufacture a red baseline.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class CorrectedOnlyNativeCodecReadPublicationTests
{
    [Theory]
    [InlineData("achievement", 0)] [InlineData("achievement", 3)]
    [InlineData("achievement", 4)] [InlineData("achievement", 7)]
    [InlineData("achievement", 8)] [InlineData("achievement", 11)]
    [InlineData("achievement", 12)] [InlineData("achievement", 15)]
    [InlineData("npc", 0)] [InlineData("npc", 3)] [InlineData("npc", 4)]
    [InlineData("npc", 5)] [InlineData("npc", 6)] [InlineData("npc", 7)]
    [InlineData("npc", 8)] [InlineData("npc", 9)] [InlineData("npc", 10)]
    [InlineData("npc", 11)] [InlineData("npc", 13)]
    [InlineData("arena", 0)] [InlineData("arena", 3)]
    [InlineData("arena", 4)] [InlineData("arena", 7)]
    public Task MissingAnyRecordTailPreservesEveryCallerOutputAndTheInitialCursor(string codec, int count) =>
        NativePacketTestThread.Run(() =>
        {
            byte[] initialized = new byte[] { 0xA5, 0xCC, 0xEE }.Concat(Record(codec).Take(count)).ToArray();
            using var packet = Exact(initialized);
            packet.Seek(3);
            var outputs = new CallerOutputs();
            byte[] before = Raw(packet);
            nuint cursor = (nuint)packet.stream;
            int size = packet.Size;

            Assert.Throws<EndOfStreamException>(() => outputs.Read(packet, codec));

            outputs.AssertUnchanged();
            Assert.Equal(cursor, (nuint)packet.stream);
            Assert.Equal(size, packet.Size);
            Assert.Equal(before, Raw(packet));
        });

    [Theory]
    [InlineData("achievement")] [InlineData("npc")] [InlineData("arena")]
    public Task NullRequiredPacketRefusesBeforeAnyCallerOutputIsAssigned(string codec) =>
        NativePacketTestThread.Run(() =>
        {
            var outputs = new CallerOutputs();
            Assert.Throws<ArgumentNullException>(() => outputs.Read(null!, codec));
            outputs.AssertUnchanged();
        });

    [Theory]
    [InlineData("achievement")] [InlineData("npc")] [InlineData("arena")]
    public Task DisposedPacketRefusesBeforeAnyCallerOutputIsAssigned(string codec) =>
        NativePacketTestThread.Run(() =>
        {
            byte[] input = Record(codec);
            using var packet = Exact(input);
            packet.Seek(1);
            int size = packet.Size;
            nuint cursor = (nuint)packet.stream;
            packet.Dispose();
            var outputs = new CallerOutputs();

            Assert.Throws<ObjectDisposedException>(() => outputs.Read(packet, codec));

            outputs.AssertUnchanged();
            Assert.Equal(size, packet.Size);
            Assert.Equal(cursor, (nuint)packet.stream);
            Assert.Equal((nuint)0, (nuint)packet.Memory);
            Assert.Equal(Record(codec), input);
        });

    [Fact]
    public Task IndependentGoldensPublishOnlyCompleteRecordsAtTheirExactTinyAllocationEnds() =>
        NativePacketTestThread.Run(() =>
        {
            using var achievement = Exact(Record("achievement"));
            achievement.GetAchievement(out var action, out var uid, out var flag);
            Assert.Equal(0x11223344u, (uint)action);
            Assert.Equal(0xAABBCCDDu, uid); Assert.Equal(0x87654321u, flag);
            Assert.Equal(16, achievement.Position); Assert.Equal(16, achievement.Capacity);
            Assert.Equal(Record("achievement"), Raw(achievement));

            using var npc = Exact(Record("npc"));
            Assert.Same(npc, npc.NpcDialog(out uint npcId, out ushort mesh,
                out byte option, out byte type, out var mode, out string input));
            Assert.Equal(0x12345678u, npcId); Assert.Equal((ushort)0xBEEF, mesh);
            Assert.Equal((byte)0xFE, option); Assert.Equal((byte)0xFD, type);
            Assert.Equal((Replay.Mode)255, mode); Assert.Equal("狼", input);
            Assert.Equal(14, npc.Position); Assert.Equal(14, npc.Capacity);
            Assert.Equal(Record("npc"), Raw(npc));

            using var arena = Exact(Record("arena"));
            arena.GetArenaSignup(out var dialog, out var button);
            Assert.Equal(0xFEDCBA98u, (uint)dialog); Assert.Equal(0x87654321u, (uint)button);
            Assert.Equal(8, arena.Position); Assert.Equal(8, arena.Capacity);
            Assert.Equal(Record("arena"), Raw(arena));
        });

    private static byte[] Record(string codec) => Convert.FromHexString(codec switch
    {
        "achievement" => "44332211DDCCBBAAEFBEADDE21436587",
        "npc" => "78563412EFBEFEFDFF0300E78BBC",
        "arena" => "98BADCFE21436587",
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    });

    private static Packet Exact(byte[] input)
    {
        var packet = new Packet(input.Length);
        Marshal.Copy(input, 0, (nint)packet.Memory, input.Length);
        packet.Size = input.Length; // The exact managed initialized source, not spare allocation.
        return packet;
    }

    private static byte[] Raw(Packet packet)
    {
        byte[] result = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, result, 0, result.Length);
        return result;
    }

    private sealed class CallerOutputs
    {
        private uint uid = 91, flag = 93;
        private ushort mesh = 92;
        private byte option = 93, type = 94;
        private AchievementAction achievement = (AchievementAction)95;
        private Replay.Mode mode = (Replay.Mode)96;
        private ArenaDialog dialog = (ArenaDialog)97;
        private ArenaOption button = (ArenaOption)98;
        private string input = "before";

        internal void Read(Packet packet, string codec)
        {
            switch (codec)
            {
                case "achievement": packet.GetAchievement(out achievement, out uid, out flag); break;
                case "npc": packet.NpcDialog(out uid, out mesh, out option, out type, out mode, out input); break;
                case "arena": packet.GetArenaSignup(out dialog, out button); break;
                default: throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        internal void AssertUnchanged()
        {
            Assert.Equal(91u, uid); Assert.Equal(93u, flag); Assert.Equal((ushort)92, mesh);
            Assert.Equal((byte)93, option); Assert.Equal((byte)94, type);
            Assert.Equal((AchievementAction)95, achievement); Assert.Equal((Replay.Mode)96, mode);
            Assert.Equal((ArenaDialog)97, dialog); Assert.Equal((ArenaOption)98, button);
            Assert.Equal("before", input);
        }
    }
}
