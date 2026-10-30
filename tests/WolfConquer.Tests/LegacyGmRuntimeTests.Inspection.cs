using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmRuntimeTests
{
    [Theory]
    [InlineData("overview", "UID")]
    [InlineData("position", "Map")]
    [InlineData("vitals", "Native HP")]
    [InlineData("inventory", "Managed inventory")]
    [InlineData("equipment", "Managed equipment")]
    [InlineData("spells", "Known spells")]
    [InlineData("proficiencies", "Weapon proficiencies")]
    [InlineData("quests", "Quest rows")]
    [InlineData("mail", "runtime diagnostic")]
    public Task RegisteredChatInspectsTheRealOwnerWithoutChangingFiles(string section, string expected) => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        var high = owner.HighWater;
        var current = owner.Current;
        var binding = owner.Native.OrdinarySpellBinding;
        await CommandAsync(owner, "@wolf inspect " + section);
        var texts = Messages(owner);
        Assert.NotEmpty(texts);
        Assert.Contains(texts, text => text.Contains(expected, StringComparison.Ordinal));
        if (section == "mail") Assert.Contains(texts, text =>
            text.StartsWith("Cached live mails:", StringComparison.Ordinal) ||
            text.StartsWith("Mailbox cache unavailable;", StringComparison.Ordinal));
        Assert.All(texts, text => { Assert.InRange(text.Length, 1, 180); Assert.All(text, ch => Assert.InRange(ch, ' ', '~')); });
        Assert.InRange(texts.Length, 1, 12);
        Assert.Same(current, owner.Current);
        Assert.Same(binding, owner.Native.OrdinarySpellBinding);
        Assert.Equal(high, owner.HighWater);
        owner.Fixture.AssertFiles(files);
        Assert.DoesNotContain("Frame exception", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
    });

    [Theory]
    [InlineData("inspect.self", true, false)]
    [InlineData("mail.testkit.self", false, true)]
    public Task HelpShowsOnlyExplicitlyGrantedCapabilities(string capability, bool inspect, bool kit) => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        await CommandAsync(owner, "@wolf help");
        var messages = Messages(owner);
        Assert.Contains(messages, text => text.StartsWith("WolfConquer GM", StringComparison.Ordinal));
        Assert.Equal(inspect, messages.Any(text => text.StartsWith("@wolf inspect", StringComparison.Ordinal)));
        Assert.Equal(kit, messages.Any(text => text.StartsWith("@wolf kit", StringComparison.Ordinal)));
        owner.Fixture.AssertFiles(files);
    }, [capability]);

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, 2399701U, null)]
    [InlineData(true, null, 2399701U)]
    public Task MissingOrForeignGrantCannotUseForgedMessageIdentity(bool enabled, uint? account, uint? character) => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        var high = owner.HighWater;
        var current = owner.Current;
        await CommandAsync(owner, "@wolf help");
        AssertRefusal(owner);
        Assert.Equal(high, owner.HighWater);
        Assert.Same(current, owner.Current);
        owner.Fixture.AssertFiles(files);
    }, enabled: enabled, account: account, character: character);

    [Theory]
    [InlineData("@wolf inspect", LegacyMsgMessagePacket.WorldChatType)]
    [InlineData("@wolf inspect", LegacyMsgMessagePacket.WhisperChatType)]
    [InlineData("@wolf inspect inventory extra", LegacyMsgMessagePacket.TalkChatType)]
    [InlineData("@wolf kit consumables not-a-guid", LegacyMsgMessagePacket.TalkChatType)]
    [InlineData("@wolf teleport 1002 430 380", LegacyMsgMessagePacket.TalkChatType)]
    public Task MalformedOrWrongChannelCommandsRefuseWithoutSideEffects(string command, uint channel) => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        var high = owner.HighWater;
        await CommandAsync(owner, command, channel);
        AssertRefusal(owner);
        Assert.Equal(high, owner.HighWater);
        owner.Fixture.AssertFiles(files);
    });

    [Theory]
    [InlineData("inspect.self", "@wolf kit consumables 5437728aece64a4785c19c3aa856518a")]
    [InlineData("mail.testkit.self", "@wolf inspect")]
    public Task OneCapabilityDoesNotGrantTheOther(string capability, string command) => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        var high = owner.HighWater;
        await CommandAsync(owner, command);
        AssertRefusal(owner);
        Assert.Equal(high, owner.HighWater);
        owner.Fixture.AssertFiles(files);
    }, [capability]);
}
