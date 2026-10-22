using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerLoadStateTests
{
    [Fact]
    public void NativeMembersAreMutablePublicInstanceFieldsWithoutAccessorSubstitutes()
    {
        var type = typeof(LegacyNativePlayerRuntime);
        var relic = type.GetField("RelicSpirit", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var online = type.GetField("Online", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotNull(relic);
        Assert.NotNull(online);
        Assert.Equal(typeof(bool), relic.FieldType);
        Assert.Equal(typeof(uint), online.FieldType);
        Assert.False(relic.IsStatic || relic.IsInitOnly || relic.IsLiteral);
        Assert.False(online.IsStatic || online.IsInitOnly || online.IsLiteral);
        Assert.Null(type.GetProperty("RelicSpirit"));
        Assert.Null(type.GetProperty("Online"));
    }

    [Fact]
    public void FreshPhysicalPlayerKeepsNativeZeroDefaultsWithoutLoadingOwners()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.False(client.Player.RelicSpirit);
        Assert.Equal(0u, client.Player.Online);
        Assert.Same(client, client.Player.Owner);
        Assert.Null(client.Beasts);
        Assert.Null(client.Inventory);
        Assert.Null(client.Equipment);
        Assert.Null(client.Rune);
    }

    [Fact]
    public void OnlineStoresFullUInt32AndSupportsNativeUncheckedIncrement()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        player.Online = 2147483648u;
        Assert.Equal(2147483648u, player.Online);
        player.Online = uint.MaxValue;
        Assert.Equal(uint.MaxValue, player.Online);
        // MODEL of the native760 expression, NOT a LoadCharacter implementation or call.
        unchecked
        {
            player.Online++;
        }
        Assert.Equal(0u, player.Online);
        player.Online++;
        Assert.Equal(1u, player.Online);
    }

    [Fact]
    public void FieldMutationsDoNotReplaceExistingOwnerRandomFlagsOrAdjacentState()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var random = player.MyRandom;
        var flags = player.BitVector;
        player.UID = 31;
        player.GuildID = 67;
        player.Kills = 89;
        player.SpawinEffect = true;
        player.SpawinEffectSkills = true;
        player.RelicSpirit = true;
        player.Online = 123;
        Assert.True(player.RelicSpirit);
        Assert.Equal(123u, player.Online);
        player.RelicSpirit = false;
        Assert.Equal(123u, player.Online);
        player.Online = 0;
        Assert.False(player.RelicSpirit);
        Assert.Same(player, client.Player);
        Assert.Same(client, player.Owner);
        Assert.Same(random, player.MyRandom);
        Assert.Same(flags, player.BitVector);
        Assert.Equal(31u, player.UID);
        Assert.Equal(67u, player.GuildID);
        Assert.Equal(89u, player.Kills);
        Assert.True(player.SpawinEffect);
        Assert.True(player.SpawinEffectSkills);
        Assert.Equal(0L, player.CompletedRateDraws);
    }

    [Fact]
    public void SamePhysicalPlayerAliasesObserveTheSameMutableFieldStorage()
    {
        var client = new LegacyNativeGameClientRuntime();
        var original = client.Player;
        var alias = original;
        alias.RelicSpirit = true;
        alias.Online = uint.MaxValue;
        Assert.Same(original, alias);
        Assert.Same(original, client.Player);
        Assert.True(client.Player.RelicSpirit);
        Assert.Equal(uint.MaxValue, client.Player.Online);
        original.RelicSpirit = false;
        original.Online = 9;
        Assert.False(alias.RelicSpirit);
        Assert.Equal(9u, alias.Online);
    }

    [Fact]
    public void EqualUidPlayersKeepTheirOnlineAndRelicStateIndependent()
    {
        var first = new LegacyNativeGameClientRuntime().Player;
        var second = new LegacyNativeGameClientRuntime().Player;
        first.UID = second.UID = 37;
        first.Online = 11;
        first.RelicSpirit = true;
        Assert.NotSame(first, second);
        Assert.Equal(0u, second.Online);
        Assert.False(second.RelicSpirit);
        second.Online = 23;
        second.RelicSpirit = true;
        first.RelicSpirit = false;
        Assert.Equal(11u, first.Online);
        Assert.False(first.RelicSpirit);
        Assert.Equal(23u, second.Online);
        Assert.True(second.RelicSpirit);
    }
}
