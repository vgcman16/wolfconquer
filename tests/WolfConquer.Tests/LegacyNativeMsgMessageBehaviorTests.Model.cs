using System.Reflection;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgMessageBehaviorTests
{
    [Fact]
    public void BothEnumsKeepNativeUIntWidthSourceOrderAndBothDuplicateChatNames()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Msg.MsgColor)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Msg.ChatMode)));
        Assert.Null(typeof(Msg.MsgColor).GetCustomAttribute<FlagsAttribute>());
        Assert.Null(typeof(Msg.ChatMode).GetCustomAttribute<FlagsAttribute>());
        var colors = typeof(Msg.MsgColor).GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "black", "blue", "orange", "white", "whitesmoke", "yellow", "yellowgreen", "violet",
            "purple", "red", "pink", "lightyellow", "cyan", "blueviolet", "antiquewhite" }, colors.Select(field => field.Name));
        Assert.Equal(new uint[] { 0, 255, 16753920, 16777215, 16119285, 16776960, 10145074, 15631086,
            8388736, 16711680, 16761035, 16777184, 65535, 9055202, 16444375 }, colors.Select(field => (uint)field.GetRawConstantValue()!));
        var modes = typeof(Msg.ChatMode).GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Talk", "Whisper", "Team", "Guild", "TopLeftSystem", "Clan", "System", "Friend", "Center", "TopLeft",
            "Service", "NewChat", "Tip", "CrossServerIcon", "Ally", "WebSite", "World", "Qualifier", "Study", "JianHu",
            "InnerPower", "PopUP", "Dialog", "CrosTheServer2", "SlideCrosTheServer", "CrosTheServer", "FirstRightCorner",
            "ContinueRightCorner", "SystemWhisper", "GuildAnnouncement", "asd", "Agate", "BroadcastMessage", "Monster",
            "SlideFromRight", "HawkMessage", "SlideFromRightRedVib", "WhiteVibrate" }, modes.Select(field => field.Name));
        Assert.Equal(new uint[] { 2000, 2001, 2003, 2004, 2005, 2006, 2000, 2009, 2011, 2012, 2014, 2032, 2015, 2016,
            2025, 2105, 2021, 2022, 2024, 2026, 2027, 2100, 2101, 2400, 2401, 2402, 2108, 2109, 2110, 2111, 2032,
            2115, 2500, 2600, 100000, 2104, 1000000, 10000000 }, modes.Select(field => (uint)field.GetRawConstantValue()!));
        Assert.Equal(Msg.ChatMode.Talk, Msg.ChatMode.System);
        Assert.Equal(Msg.ChatMode.NewChat, Msg.ChatMode.asd);
    }

    [Fact]
    public void DefaultConstructorRetainsNullStringsMutableFieldsAndOnlyDeclarationDefaults()
    {
        var message = new Msg();
        AssertNoProtoOrAncillaryBirth(message);
        Assert.Null(message._From); Assert.Null(message._To); Assert.Null(message.__Message);
        Assert.Equal(0u, message.Mesh); Assert.Equal(0u, message.Color); Assert.Equal((Msg.ChatMode)0u, message.ChatType);
        Assert.All(typeof(Msg).GetFields(BindingFlags.Public | BindingFlags.Instance), field => Assert.False(field.IsInitOnly));
        Assert.NotNull(Msg.commandsFile);
        Assert.False(typeof(Msg).GetField("commandsFile")!.IsInitOnly);
        Assert.Equal(typeof(uint), typeof(Msg).GetField("TestGui")!.FieldType);
        Assert.True(typeof(Msg).GetField("TestGui")!.IsStatic);
    }

    [Fact]
    public void ThreeArgumentConstructorUsesSystemAndAllWithoutNormalizingMessageOrEnumValues()
    {
        var message = new Msg(null!, (Msg.MsgColor)uint.MaxValue, (Msg.ChatMode)uint.MaxValue);
        AssertNoProtoOrAncillaryBirth(message);
        Assert.Null(message.__Message); Assert.Equal("SYSTEM", message._From); Assert.Equal("ALL", message._To);
        Assert.Equal(0u, message.Mesh); Assert.Equal(uint.MaxValue, message.Color); Assert.Equal((Msg.ChatMode)uint.MaxValue, message.ChatType);
    }

    [Fact]
    public void FourArgumentConstructorRetainsTheExplicitRecipientIncludingNull()
    {
        var message = new Msg("text", null!, Msg.MsgColor.cyan, Msg.ChatMode.Whisper);
        AssertNoProtoOrAncillaryBirth(message);
        Assert.Equal("text", message.__Message); Assert.Null(message._To); Assert.Equal("SYSTEM", message._From);
        Assert.Equal(0u, message.Mesh); Assert.Equal(65535u, message.Color); Assert.Equal(Msg.ChatMode.Whisper, message.ChatType);
    }

    [Fact]
    public void FiveArgumentConstructorKeepsTheExplicitSenderRecipientAndEmbeddedNulMessage()
    {
        var message = new Msg("a\0b", "to", null!, Msg.MsgColor.blue, Msg.ChatMode.Talk);
        AssertNoProtoOrAncillaryBirth(message);
        Assert.Equal("a\0b", message.__Message); Assert.Equal("to", message._To); Assert.Null(message._From);
        Assert.Equal(0u, message.Mesh); Assert.Equal(255u, message.Color); Assert.Equal(Msg.ChatMode.Talk, message.ChatType);
    }

    [Fact]
    public void SixArgumentConstructorRetainsTheFullMeshWithoutChangingOtherUidFields()
    {
        var message = new Msg("text", uint.MaxValue, "to", "from", Msg.MsgColor.orange, Msg.ChatMode.HawkMessage);
        AssertNoProtoOrAncillaryBirth(message);
        Assert.Equal("text", message.__Message); Assert.Equal("to", message._To); Assert.Equal("from", message._From);
        Assert.Equal(uint.MaxValue, message.Mesh); Assert.Equal(16753920u, message.Color); Assert.Equal(Msg.ChatMode.HawkMessage, message.ChatType);
    }

    [Fact]
    public void NineArgumentConstructorCopiesBothUidsAndBothNewFieldsButDoesNotBuildProto()
    {
        var message = new Msg(Msg.MsgColor.red, Msg.ChatMode.PopUP, 1, uint.MaxValue,
            0x80000000, 4, null!, "to", "text");
        Assert.Null(message.proto); Assert.Equal(string.Empty, message.ServerName); Assert.Equal(0u, message.Mesh);
        Assert.Equal(1u, message.MessageUID1); Assert.Equal(uint.MaxValue, message.MessageUID2);
        Assert.Equal(0x80000000u, message.New1); Assert.Equal(4u, message.New2);
        Assert.Null(message._From); Assert.Equal("to", message._To); Assert.Equal("text", message.__Message);
        Assert.Equal(16711680u, message.Color); Assert.Equal(Msg.ChatMode.PopUP, message.ChatType);
    }
}
