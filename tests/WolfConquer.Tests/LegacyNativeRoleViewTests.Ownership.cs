using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeRoleViewTests
{
    [Fact]
    public void PlayerBirthOwnsViewSevenDistinctSafeDictionariesAndMonsterActions()
    {
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        var view = first.Player.View;

        Assert.Same(first, first.Player.Owner);
        Assert.Same(first, view.Owner);
        Assert.Same(first.Player, view.GetPlayer());
        Assert.NotSame(view, second.Player.View);
        Assert.False(view.InColorScrenn);
        Assert.Equal(18, LegacyNativeRoleView.ViewThreshold);
        Assert.Equal(7, view.src.Length);
        Assert.Equal(7, view.src.Distinct().Count());
        Assert.NotSame(view.src, second.Player.View.src);
        for (var i = 0; i < 7; i++)
        {
            var bucket = Assert.IsType<LegacyNativeDbSafeDictionary<uint, ILegacyNativeMapObj>>(view.src[i]);
            Assert.Empty(bucket);
            Assert.False(bucket.Update);
            Assert.Empty(bucket.MyArray);
            Assert.NotNull(bucket.SyncRoot);
            Assert.NotSame(bucket, second.Player.View.src[i]);
            Assert.NotSame(bucket.SyncRoot, second.Player.View.src[i].SyncRoot);
        }
        Assert.Equal(7, view.src.Select(bucket => bucket.SyncRoot).Distinct().Count());
        var model = Model(12);
        view.src[0].Add(12, model);
        Assert.Same(model, view.src[0][12]);
        Assert.All(view.src.Skip(1), bucket => Assert.Empty(bucket));
        Assert.All(second.Player.View.src, bucket => Assert.Empty(bucket));

        Assert.IsType<LegacyNativeMonsterActionHandler>(view.MobActions);
        Assert.NotSame(view.MobActions, second.Player.View.MobActions);
        Assert.NotSame(view.MobActions.Random, second.Player.View.MobActions.Random);
        Assert.IsType<Random>(view.MobActions.SystemRandom);
        Assert.NotSame(view.MobActions.SystemRandom, second.Player.View.MobActions.SystemRandom);
        Assert.NotSame(first.Player.MyRandom, view.MobActions.SystemRandom);
        Assert.Null(first.Map);
        Assert.Null(second.Map);
    }

    [Fact]
    public void GetPlayerReadsCurrentOwnerPlayerWithoutReparentingOldPhysicalPlayer()
    {
        var client = Client();
        var original = client.Player;
        var retainedView = original.View;
        var replacement = new LegacyNativePlayerRuntime(client);

        client.Player = replacement;

        Assert.Same(replacement, retainedView.GetPlayer());
        Assert.Same(retainedView, original.View);
        Assert.Same(client, original.Owner);
        Assert.Same(client, retainedView.Owner);
        Assert.NotSame(retainedView, replacement.View);
        Assert.Null(client.Map);
    }

    [Fact]
    public void MonsterMsRandomStartsAtOneWithLiteralNativeSequenceAndIndependentOwners()
    {
        int[] expected = [41, 18467, 6334, 26500, 19169, 15724, 11478, 29358, 26962, 24464];
        var first = new LegacyNativeMonsterActionHandler();
        var second = new LegacyNativeMonsterActionHandler();

        Assert.IsType<LegacyNativeMSRandom>(first.Random);
        Assert.Equal(32767, LegacyNativeMSRandom.RAND_MAX);
        Assert.Equal(expected, expected.Select(_ => first.Random.Next()).ToArray());
        Assert.Equal(41, second.Random.Next());
        Assert.NotSame(first.Random, second.Random);
        Assert.IsType<Random>(first.SystemRandom);
        Assert.NotSame(first.SystemRandom, second.SystemRandom);
    }

    [Fact]
    public void MonsterRateUsesStrictComparisonAndConsumesOneMsRandomDrawEvenForExtremeRates()
    {
        var actions = new LegacyNativeMonsterActionHandler();
        var systemRandom = actions.SystemRandom;

        Assert.False(actions.Rate(41)); // 41 % 100 equals threshold, not a success.
        Assert.True(actions.Rate(68));  // 18467 % 100 is 67.
        Assert.False(actions.Rate(34)); // 6334 % 100 equals threshold.
        Assert.True(actions.Rate(1));   // 26500 % 100 is zero.
        Assert.False(actions.Rate(int.MinValue));
        Assert.True(actions.Rate(int.MaxValue));
        Assert.Equal(11478, actions.Random.Next());
        Assert.Same(systemRandom, actions.SystemRandom);
    }

    [Theory]
    [InlineData(3, 5, 1002)]
    [InlineData(0, 7, 0)]
    [InlineData(7, 0, -1)]
    [InlineData(0, 0, int.MinValue)]
    public void GameMapConstructorOwnsExactCellsBoundsClientsAndLeavesViewUnpublished(int width, int height, int id)
    {
        var map = new LegacyNativeGameMap(width, height, id);
        var other = new LegacyNativeGameMap(width, height, id);
        var field = typeof(LegacyNativeGameMap).GetField("Clients", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var clients = Assert.IsType<ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>>(field.GetValue(map));
        var otherClients = Assert.IsType<ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>>(field.GetValue(other));

        Assert.Equal(new Size(width, height), map.bounds);
        Assert.Equal(unchecked((uint)id), map.ID);
        Assert.True(typeof(LegacyNativeGameMap).GetProperty(nameof(map.ID))!.SetMethod!.IsPrivate);
        Assert.IsType<LegacyNativeMapFlagType[,]>(map.cells);
        Assert.Equal(width, map.cells.GetLength(0));
        Assert.Equal(height, map.cells.GetLength(1));
        Assert.All(map.cells.Cast<LegacyNativeMapFlagType>(), cell => Assert.Equal(LegacyNativeMapFlagType.None, cell));
        Assert.NotSame(map.cells, other.cells);
        Assert.NotSame(clients, otherClients);
        Assert.Empty(clients);
        Assert.Empty(otherClients);
        var client = new LegacyNativeGameClientRuntime();
        Assert.True(clients.TryAdd(4, client));
        Assert.Same(client, clients[4]);
        Assert.Empty(otherClients);
        Assert.Null(client.Map);
        Assert.Null(map.View);
        Assert.Null(other.View);
    }

    [Fact]
    public void GameMapFlagsKeepNativeByteValuesAndFlagsMarker()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeMapFlagType)));
        Assert.NotNull(typeof(LegacyNativeMapFlagType).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { "None", "Valid", "Monster", "Item", "Player", "Npc" },
            Enum.GetNames<LegacyNativeMapFlagType>());
        Assert.Equal(new byte[] { 0, 1, 2, 4, 8, 16 },
            Enum.GetValues<LegacyNativeMapFlagType>().Select(value => (byte)value));
    }

    [Fact]
    public void PlayerCoordinatesRetainIndependentPreviousAssignmentsAndDoNotPublishAMap()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        Assert.False(player.AllowDynamic);
        Assert.Equal(LegacyNativeMapObjectType.Player, player.ObjType);
        Assert.Equal(0u, player.Map);
        Assert.Equal(0u, player.DynamicID);
        Assert.Equal((ushort)0, player.X);
        Assert.Equal((ushort)0, player.Y);
        Assert.Equal((ushort)0, player.Px);
        Assert.Equal((ushort)0, player.Py);

        player.X = 50;
        player.Y = 60;
        player.X = 75;
        Assert.Equal((ushort)50, player.Px);
        Assert.Equal((ushort)0, player.Py);
        Assert.Equal((ushort)60, player.Y);
        player.Y = 60; // Same-value assignments still capture the previous value.
        Assert.Equal((ushort)60, player.Py);
        player.Y = ushort.MaxValue;
        player.X = 0;
        Assert.Equal((ushort)75, player.Px);
        Assert.Equal((ushort)60, player.Py);
        Assert.Equal(ushort.MaxValue, player.Y);
        player.Map = uint.MaxValue;
        player.DynamicID = uint.MaxValue;
        Assert.Equal(uint.MaxValue, player.Map);
        Assert.Equal(uint.MaxValue, player.DynamicID);
        Assert.Null(client.Map);
        Assert.Same(player, player.View.GetPlayer());
    }
}
