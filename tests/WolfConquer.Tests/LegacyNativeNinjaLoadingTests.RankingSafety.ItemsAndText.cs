using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false, "alias")]
    [InlineData(true, "alias")]
    [InlineData(false, "keyMismatch")]
    [InlineData(true, "keyMismatch")]
    [InlineData(false, "zeroId")]
    [InlineData(true, "zeroId")]
    public Task CorruptSigilIdentityCannotDoubleCountOrPublishAnAddItemPrefix(bool addItem, string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        var item = scope.Item(100, 100, 1);
        scope.Meta(7, 100, 1, score: 600);
        if (mutation == "alias") Assert.True(scope.Ninja.Items.TryAdd(900, item));
        if (mutation == "keyMismatch") item.ItemID = 101;
        if (mutation == "zeroId") { item.ItemID = 0; scope.Ninja.Items.TryRemove(100, out _); scope.Ninja.Items[0] = item; }
        var items = scope.Ninja.Items;
        var pairs = items.ToArray();
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (addItem) scope.Ninja.AddItem(100, 1, 0, 0, 0);
            else scope.Ninja.UpdateRank();
        });
        Assert.Same(items, scope.Ninja.Items);
        Assert.Equal(pairs, items.ToArray());
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData("nameNull")]
    [InlineData("nameSlash")]
    [InlineData("nameControl")]
    [InlineData("nameUnicode")]
    [InlineData("guildNull")]
    [InlineData("guildSlash")]
    [InlineData("guildControl")]
    [InlineData("guildUnicode")]
    public Task MalformedRankAppearanceTextRefusesBeforeAnyRankStore(string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        scope.Ninja.FireMastery = 1000;
        scope.Client.Player.Name = "Owned Ninja";
        switch (mutation)
        {
            case "nameNull": scope.Client.Player.Name = null!; break;
            case "nameSlash": scope.Client.Player.Name = "bad/name"; break;
            case "nameControl": scope.Client.Player.Name = "bad\nname"; break;
            case "nameUnicode": scope.Client.Player.Name = "bad\ud800"; break;
            default:
                scope.Client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Owned Guild");
                scope.Client.Player.MyGuild.GuildName = mutation switch
                {
                    "guildNull" => null!, "guildSlash" => "bad/guild",
                    "guildControl" => "bad\rguild", _ => "bad\udfff",
                };
                break;
        }
        var before = new RankingGraphWitness();
        Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task ValidUnicodeAppearanceAndEmptyOptionalGuildAreRetainedWithoutTransliteration() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.Name = "侠忍者\ud83d\udc3a";
        scope.Client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("");
        scope.Ninja.FireMastery = 1000;
        scope.Ninja.UpdateRank();
        var entry = scope.Rank(RankType.Overall).RankingList[17];
        Assert.Equal("侠忍者\ud83d\udc3a", entry.Name);
        Assert.Equal("", entry.GuildName);
        AssertNoRankingPackets(scope);
    });
}
