using WolfConquer.Core.World;
using Base = WolfConquer.Core.World.LegacyNativeAttackBase;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData("missing-equipment")]
    [InlineData("unowned-equipment")]
    [InlineData("foreign-equipment")]
    [InlineData("missing-player")]
    [InlineData("foreign-player")]
    public Task MinimumMonsterDamageRejectsUnavailableOrMismatchedPhysicalOwners(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var player = s.Player;
        var stamina = player.Stamina;
        var mana = player.Mana;
        var packetSize = s.Packet.Size;
        var packetPosition = s.Packet.Position;
        switch (corruption)
        {
            case "missing-equipment": s.Client.Equipment = null; break;
            case "unowned-equipment": s.Client.Equipment = new LegacyNativeEquipItemStorage(null); break;
            case "foreign-equipment":
                var foreignEquipmentOwner = new LegacyNativeGameClientRuntime();
                foreignEquipmentOwner.Player.UID = player.UID;
                s.Client.Equipment = new LegacyNativeEquipItemStorage(foreignEquipmentOwner);
                break;
            case "missing-player": s.Client.Player = null!; break;
            case "foreign-player":
                var foreignPlayerOwner = new LegacyNativeGameClientRuntime();
                foreignPlayerOwner.Player.UID = player.UID;
                s.Client.Player = foreignPlayerOwner.Player;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }

        Assert.Throws<InvalidDataException>(() => Base.AdjustMinDamageUser2Monster(1, s.Client));

        Assert.Equal(stamina, player.Stamina);
        Assert.Equal(mana, player.Mana);
        Assert.Equal(packetSize, s.Packet.Size);
        Assert.Equal(packetPosition, s.Packet.Position);
        Assert.Equal(new Random(91).Next(), Base.MyRandom.Next());
        Assert.Empty(s.Queue);
    });

    [Fact]
    public void MinimumMonsterDamageRejectsNullClientWithAnExplicitArgumentError()
    {
        Assert.Throws<ArgumentNullException>(() => Base.AdjustMinDamageUser2Monster(1, null!));
    }

    [Theory]
    [InlineData(0, 0u, 0u, int.MinValue, 1)]
    [InlineData(100, 420009u, 410008u, 1, 28)]
    [InlineData(140, 420009u, 410008u, 100, 100)]
    [InlineData(65535, uint.MaxValue, uint.MaxValue, 0, 6564)]
    public Task MinimumMonsterDamagePreservesActualLevelWeaponRemaindersAndLargerInput(
        int level,
        uint leftWeapon,
        uint rightWeapon,
        int damage,
        int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Set("_level", checked((ushort)level));
        s.Client.Equipment!.LeftWeapon = leftWeapon;
        s.Client.Equipment.RightWeapon = rightWeapon;

        Assert.Equal(expected, Base.AdjustMinDamageUser2Monster(damage, s.Client));
        Assert.Equal(new Random(91).Next(), Base.MyRandom.Next());
        Assert.Empty(s.Queue);
    });
}
