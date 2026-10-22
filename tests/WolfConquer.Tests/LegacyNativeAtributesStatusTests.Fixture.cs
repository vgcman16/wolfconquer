using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeAtributesStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAtributesStatusTests
{
    private static readonly MethodInfo Position = typeof(Table).GetMethod("PositionArray", BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Explicit physical getter prestate; no Class/Level setter or loader behavior is simulated.
    private static LegacyNativePlayerRuntime Player(uint job = 1000, ushort level = 7, byte reborn = 0)
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        SetGetter(player, "_class", job);
        SetGetter(player, "_level", level);
        player.Reborn = reborn;
        return player;
    }

    private static void SetGetter(LegacyNativePlayerRuntime player, string name, object value) =>
        typeof(LegacyNativePlayerRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, value);

    private static Table.Instant Row(ushort strength = 10) => new()
    {
        Strenght = strength, Vitality = (ushort)(strength + 1), Agility = (ushort)(strength + 2), Spirit = (ushort)(strength + 3)
    };

    private static void AssertStats(Table.Instant row, LegacyNativePlayerRuntime player) =>
        Assert.Equal((row.Strenght, row.Vitality, row.Agility, row.Spirit), Stats(player));

    private static (ushort Strength, ushort Vitality, ushort Agility, ushort Spirit) Stats(LegacyNativePlayerRuntime player) =>
        (player.Strength, player.Vitality, player.Agility, player.Spirit);

    private static void Sentinel(LegacyNativePlayerRuntime player) =>
        (player.Strength, player.Vitality, player.Agility, player.Spirit) = (91, 92, 93, 94);
}
