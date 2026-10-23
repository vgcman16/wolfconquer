using System.Text;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Potion = WolfConquer.Core.World.LegacyNativeMsgRacePotion.RaceItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    [Theory]
    [InlineData(760u,8336)] [InlineData(761u,8333)] [InlineData(762u,8335)]
    [InlineData(763u,8337)] [InlineData(764u,8339)] [InlineData(765u,8334)]
    [InlineData(766u,8338)] [InlineData(768u,8331)] [InlineData(769u,8332)] [InlineData(770u,8330)]
    public Task WholeStaticRoleKeepsRaceMeshMappingWithoutRandomDraw(uint mesh, ushort potion) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = new LegacyNativeStaticRoleRuntime(12, 13, FlagMesh: mesh);
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(31); var control = new Random(31);
        Assert.Equal((Potion)potion, role.Type); Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
        Assert.Equal(12013u, role.UID); Assert.Equal((byte)1, role.Level); Assert.Equal("Flag", role.Name);
        Assert.False(role.Viable); Assert.False(role.Alive); Assert.False(role.IsTrap()); Assert.False(role.QuestionMark);
        Assert.Equal(10, role.BitVector.bits.Length);
    });

    [Theory]
    [InlineData(767u)] [InlineData(9999u)]
    public Task StaticRoleFallbackRaceTypeRetriesExcludedValuesOnTheActualSharedRng(uint mesh) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = new LegacyNativeStaticRoleRuntime(12, 13, FlagMesh: mesh);
        // SafeRandom(0) means clock-seeded birth, not System.Random(0).
        for (int seed = 1; seed <= 32; seed++)
        {
            var expected = new Random(seed); int selected;
            do selected = expected.Next(8330, 8340); while (selected == 8338 || selected == 8333);
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
            Assert.Equal(selected, (int)role.Type); Assert.Equal(expected.Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        Assert.Equal(mesh == 767, role.QuestionMark);
    });

    [Fact]
    public Task StaticRolePickFrozenTrapAndActualMapAdmissionPreserveNativeStateAndCollision() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); var role = new LegacyNativeStaticRoleRuntime(12, 13) { Map = 700 };
        uint[] meshes = [760,762,763,764,765,766,767,768,769,770];
        string[] names = ["GuardPotion","SluggishPotion","DizzyHammer","RestorePotion","ScreamBomb","SuperExclamationMark","SuperQuestionMark","SpiritPotion","ExcitementPotion","ChaosBomb"];
        var covered = new HashSet<int>();
        for (int seed = 1; seed <= 128; seed++)
        {
            var expected = new Random(seed); int row = expected.Next(10); covered.Add(row);
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed); role.Pick();
            Assert.Equal(meshes[row], role.Mesh); Assert.Equal(names[row], role.Name); Assert.True(role.Viable);
            Assert.Equal((byte)(row == 1 || row == 3 ? 3 : 1), role.Level);
            Assert.Equal(expected.Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        Assert.Equal(10, covered.Count);
        role.DoFrozenTrap(17); Assert.Equal(17u, role.SetBy); Assert.Equal(761u, role.Mesh); Assert.Equal("Frozen Trap", role.Name);
        Assert.Equal((byte)1, role.Level); Assert.True(map.AddStaticRole(role)); Assert.Equal(Cell.Npc, map.cells[12,13]);
        Assert.Same(role, SingleRole<LegacyNativeStaticRoleRuntime>(map, LegacyNativeMapObjectType.StaticRole));
        Assert.True(map.RemoveStaticRole(role));
        Assert.True(map.RemoveStaticRole(role)); // Native LeaveMap reports true for an absent in-bounds object.
        Assert.Equal(Cell.Valid, map.cells[12,13]); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task StaticRoleWholeSpawnFlagsAndMovementUseActualPlayerProtoAndSpatialOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var role = new LegacyNativeStaticRoleRuntime(12, 13, "OwnedFlag") { Map = 700, HitPoints = 1 };
        Assert.True(map.AddStaticRole(role)); Assert.True(role.AddFlag(Flag.Poisoned, 30, true));
        Assert.False(role.AddFlag(Flag.Poisoned, 30, true)); Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
        scope.Queue.Clear(); role.GetArray(scope.Packet, false, false);
        byte[] spawn = Bytes(scope.Packet); Assert.Equal((ushort)2227, Opcode(spawn));
        Assert.Contains("OwnedFlag", Encoding.UTF8.GetString(spawn));
        var fields = Scalars(spawn); Assert.Equal(12013UL, Assert.Single(fields[2]));
        Assert.Equal(8910UL, Assert.Single(fields[1])); Assert.Equal(1UL, Assert.Single(fields[26]));
        Assert.Equal(1UL, Assert.Single(fields[35])); Assert.Equal(12UL, Assert.Single(fields[27]));
        Assert.Equal(13UL, Assert.Single(fields[28])); Assert.False(fields.ContainsKey(21));
        Assert.Equal(role.BitVector.bits.Select(word => (ulong)word).ToArray(), fields[6].ToArray());
        Assert.True(role.UpdateFlag(Flag.Poisoned, 5, true, 10)); Assert.True(role.RemoveFlag(Flag.Poisoned, map));
        Assert.False(role.RemoveFlag(Flag.Poisoned, map)); scope.Queue.Clear();
        Assert.False(role.MoveTo(12,13)); Assert.Empty(scope.Queue);
        Assert.True(role.MoveTo(20,13)); Assert.Equal((ushort)13, role.X); Assert.Equal((ushort)13, role.Y);
        Assert.Equal((ushort)2205, Opcode(Assert.Single(scope.Queue)));
        Assert.Same(role, SingleRole<LegacyNativeStaticRoleRuntime>(map, LegacyNativeMapObjectType.StaticRole));
    });

    [Theory]
    [InlineData(18,true)] [InlineData(19,false)]
    public Task StaticVisibilityUsesInclusiveEighteenAndExactMapDynamicUidGates(int distance, bool visible) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = new LegacyNativeStaticRoleRuntime((ushort)(10 + distance), 10) { Map = 700 };
        scope.Player.Map = 700; scope.Player.X = 10; scope.Player.Y = 10;
        Assert.Equal(visible, role.CanSee(scope.Player)); scope.Player.DynamicID = 9; Assert.False(role.CanSee(scope.Player));
        scope.Player.DynamicID = 0; role.UID = scope.Player.UID; Assert.False(role.CanSee(scope.Player));
        Assert.Throws<NullReferenceException>(() => role.CanSee(null!));
    });

    [Fact]
    public Task NativeRacePotionCodecRoundTripsSignedLocationAndRawEnumWithoutReceiverState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new LegacyNativeMsgRacePotion.RacePotion
        { Amount = ushort.MaxValue, PotionType = (Potion)60000, Location = -7, dwParam = uint.MaxValue };
        scope.Packet.CreateRecePotion(row);
        var expected = new List<byte>(); expected.AddRange(BitConverter.GetBytes((ushort)16)); expected.AddRange(BitConverter.GetBytes((ushort)2180));
        expected.AddRange(BitConverter.GetBytes(ushort.MaxValue)); expected.AddRange(BitConverter.GetBytes((ushort)60000));
        expected.AddRange(BitConverter.GetBytes(-7)); expected.AddRange(BitConverter.GetBytes(uint.MaxValue));
        Assert.Equal(expected.ToArray(), Bytes(scope.Packet)); scope.Packet.Seek(4); scope.Packet.GetRecePotion(out var decoded);
        Assert.NotSame(row, decoded); Assert.Equal(row.Amount, decoded.Amount); Assert.Equal(row.PotionType, decoded.PotionType);
        Assert.Equal(row.Location, decoded.Location); Assert.Equal(row.dwParam, decoded.dwParam); Assert.Empty(scope.Queue);
    });
}
