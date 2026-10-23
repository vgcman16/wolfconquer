using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportMapViewCycleTests
{
    [Fact]
    public Task ReusedPetUidReplacesTheRealRegistryValueWithoutRejectingTheSecondProducer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        scope.PetFamily();
        LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet);
        var first = Assert.Single(scope.Player.ListPets).Value;
        LegacyNativeMsgGuild.UID = first.UID;

        LegacyNativeMsgGuild.AddPet(scope.Client, 6659, 5, 12, 14, scope.Packet);

        var second = Assert.Single(scope.Player.ListPets).Value;
        Assert.NotSame(first, second);
        Assert.Equal(first.UID, second.UID);
        Assert.Equal(6659u, second.Family.ID);
        Assert.Equal(137u, second.Mesh);
        Assert.Equal(700011u, LegacyNativeMsgGuild.UID);
        Assert.Equal(400002u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.True(map.MonsterOnTile(12, 13));
        Assert.True(map.MonsterOnTile(12, 14));
        Assert.Equal(new ushort[] { 2192, 2227, 2046, 2192, 2227, 2046 }, scope.Queue.Select(Opcode));
    });

    [Theory]
    [InlineData(6638u, 100u, 500u)]
    [InlineData(6639u, 138u, 10u)]
    [InlineData(6658u, 138u, 10u)]
    [InlineData(6659u, 137u, 10u)]
    [InlineData(6678u, 137u, 10u)]
    [InlineData(6679u, 848u, 10u)]
    [InlineData(6698u, 848u, 10u)]
    [InlineData(6699u, 100u, 500u)]
    public Task CompleteAddPetCreatesGenuineMonsterTwoSourceMapAliasesAndAllPackets(uint id, uint mesh, uint hp) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        var original = scope.PetFamily();
        Assert.Empty(scope.Player.ListPets);
        var before = DateTime.Now;
        LegacyNativeMsgGuild.AddPet(scope.Client, id, 6, 12, 13, scope.Packet);
        var after = DateTime.Now;
        var pair = Assert.Single(scope.Player.ListPets);
        var pet = pair.Value;
        Assert.Equal(700010u, pair.Key);
        Assert.Equal(700010u, pet.UID);
        Assert.Equal(700011u, LegacyNativeMsgGuild.UID);
        Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.NotSame(original, pet.Family);
        Assert.Equal(1u, original.ID);
        Assert.Equal(500, original.MaxHealth);
        Assert.Equal(id, pet.Family.ID);
        Assert.Equal(mesh, pet.Mesh);
        Assert.Equal(hp, pet.HitPoints);
        Assert.Equal((int)hp, pet.Family.MaxHealth);
        Assert.Equal((byte)140, pet.Level);
        Assert.Equal((byte)0, pet.Boss);
        Assert.Equal(6u, pet.PetFlag);
        Assert.Equal("", pet.Name);
        Assert.Equal(LegacyNativeMonsterSettings.Lottus, pet.Family.Settings);
        Assert.Equal(13190u, pet.Family.SpellId);
        Assert.Equal(1000, pet.Family.AttackSpeed);
        Assert.Equal(10000, pet.StampFloorSecounds);
        Assert.InRange(pet.FloorStampTimer, before.AddSeconds(1), after.AddSeconds(1));
        Assert.Same(scope.Client, pet.OwnerFloor);
        Assert.Same(map, pet.GMap);
        Assert.Equal((ushort)12, pet.X);
        Assert.Equal((ushort)13, pet.Y);
        Assert.True(map.MonsterOnTile(12, 13));
        var roles = map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).ToArray();
        Assert.Equal(2, roles.Length); // Native producer changes UID after initial Add publication.
        Assert.Same(pet, roles[0]);
        Assert.Same(pet, roles[1]); // Do not silently "repair" the original alias quirk.
        Assert.Equal(new ushort[] { 2192, 2227, 2046 }, scope.Queue.Select(Opcode));
        var info = scope.Queue.First();
        Assert.Equal(700010u, BitConverter.ToUInt32(info, 4));
        Assert.Equal(id, BitConverter.ToUInt32(info, 8));
        Assert.Equal(6u, BitConverter.ToUInt32(info, 12));
        Assert.Equal((ulong)mesh, BitConverter.ToUInt64(info, 16));
        Assert.Equal((ushort)14, BitConverter.ToUInt16(info, 24));
    });

    [Fact]
    public Task MissingPetFamilyDoesNotInventSpawnOrConsumeEitherUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet);
        Assert.Equal(700010u, LegacyNativeMsgGuild.UID);
        Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Empty(scope.Player.ListPets);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task UnspawnableTileConsumesOnlyRealMonsterCtorUidAndNeverPetSuccessEffects() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        scope.PetFamily();
        map.cells[12, 13] = LegacyNativeMapFlagType.None;
        LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet);
        Assert.Equal(700010u, LegacyNativeMsgGuild.UID);
        Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Empty(scope.Player.ListPets);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingCollectionFaultDoesNotAdvancePetUidOrPublishAfterFamilyCopy() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        scope.PetFamily();
        Assert.Null(map.MonstersColletion);
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet));
        Assert.Equal(700010u, LegacyNativeMsgGuild.UID);
        Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Empty(scope.Player.ListPets);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullListFaultRetainsSpawnAndPetInfoButOccursBeforeActionBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        scope.PetFamily();
        scope.Player.ListPets = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet));
        Assert.Equal(700011u, LegacyNativeMsgGuild.UID);
        Assert.True(map.MonsterOnTile(12, 13));
        Assert.Equal(2, map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).Count());
        Assert.Equal(new ushort[] { 2192, 2227 }, scope.Queue.Select(Opcode));
        Assert.Null(scope.Player.ListPets);
    });

    [Fact]
    public Task NullPlayerViewFaultRetainsRealPetRegistryAfterEarlierPackets() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        scope.PetFamily();
        scope.Player.View = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet));
        Assert.Equal(700011u, LegacyNativeMsgGuild.UID);
        Assert.Equal(700010u, Assert.Single(scope.Player.ListPets).Key);
        Assert.Equal(new ushort[] { 2192, 2227 }, scope.Queue.Select(Opcode));
    });

    [Fact]
    public Task PetUidWrapUsesNativeUncheckedCounterAndStillCompletesRealProducer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        map.MonstersColletion = new(700);
        scope.PetFamily();
        LegacyNativeMsgGuild.UID = uint.MaxValue;
        LegacyNativeMsgGuild.AddPet(scope.Client, 6639, 4, 12, 13, scope.Packet);
        Assert.Equal(0u, LegacyNativeMsgGuild.UID);
        Assert.Equal(uint.MaxValue, Assert.Single(scope.Player.ListPets).Key);
        Assert.Equal(new ushort[] { 2192, 2227, 2046 }, scope.Queue.Select(Opcode));
    });
}
