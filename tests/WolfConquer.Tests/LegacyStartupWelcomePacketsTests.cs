using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStartupWelcomePacketsTests
{
    [Fact]
    public void LegacyStartupWelcomeCreatesTalkFramesThenStaticGuiFrames()
    {
        var frames = LegacyStartupWelcomePackets.CreateEnglishStartup("TestSrv");

        Assert.Equal(11, frames.Count);
        for (var index = 0; index < 6; index++)
        {
            var frame = ReadFrame(frames[index]);
            Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var message));
            Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        }

        for (var index = 6; index < frames.Count; index++)
        {
            var frame = ReadFrame(frames[index]);
            Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var staticGui));
            Assert.Equal(LegacyNpcReplyInteractType.StaticGui, staticGui.InteractType);
        }

        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[0]), out var firstTalk));
        Assert.Equal("Welcome to TestSrv.", firstTalk.Content[3]);
        var talkMessages = frames
            .Take(6)
            .Select(frame =>
            {
                Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frame), out var message));
                return message.Content[3];
            })
            .ToArray();
        Assert.Equal(
        [
            "Welcome to TestSrv.",
            "Guild War runs Friday from 21:00 to 22:00.",
            "Drops range from 100 to 5000 CPs; VIP users receive random bonuses.",
            "We have many maps for you to hunt.",
            "Official website: TestSrv ",
            "Enjoy TestSrv."
        ], talkMessages);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frames[6]), out var firstStatic));
        Assert.Equal((byte)LegacyStaticGuiMode.Body, firstStatic.OptionId);
        Assert.Equal("Welcome to TestSrv.", firstStatic.Text);
        var staticMessages = frames
            .Skip(6)
            .Select(frame =>
            {
                Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var message));
                return (message.OptionId, message.Text);
            })
            .ToArray();
        Assert.Equal(
        [
            ((byte)LegacyStaticGuiMode.Body, "Welcome to TestSrv."),
            ((byte)LegacyStaticGuiMode.Body, "- Please avoid cheats and report reproducible bugs."),
            ((byte)LegacyStaticGuiMode.Body, "- Never share your account information with anyone."),
            ((byte)LegacyStaticGuiMode.Body, "- Development build: gameplay and English are incomplete."),
            ((byte)LegacyStaticGuiMode.Footer, "Thank you for testing TestSrv.")
        ], staticMessages);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frames[^1]), out var footer));
        Assert.Equal((byte)LegacyStaticGuiMode.Footer, footer.OptionId);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
