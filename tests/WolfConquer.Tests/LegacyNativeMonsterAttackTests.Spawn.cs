using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData(-1, 65535, 4294967295u, 255)]
    [InlineData(0, 256, 0u, 0)]
    [InlineData(500, 15, 500u, 15)]
    public Task GenuineConstructorPreservesPhysicalDefaultsAndConsumesOneFacingDraw(int maxHp, ushort level, uint hp, byte narrowedLevel) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var family = Family(); family.MaxHealth = maxHp; family.Level = level; family.Boss = 7;
        family.extra_battlelev = 9; family.extra_damage = 17; family.AttackRange = -3;
        LegacyNativePoolRandom.GetRandom = new(61); var random = new Random(61); byte facing = (byte)random.Next(0,8);
        DateTime before = DateTime.Now; var role = s.Monster(family); DateTime after = DateTime.Now;
        Assert.Same(family,role.Family); Assert.Same(s.Map,role.GMap); Assert.Equal("owned-spawn",role.LocationSpawn);
        Assert.Equal(hp,role.HitPoints); Assert.Equal(narrowedLevel,role.Level); Assert.Equal(7,role.Boss);
        Assert.Equal(facing,(byte)role.Facing); Assert.Equal(random.Next(),LegacyNativePoolRandom.GetRandom.Next());
        Assert.False(role.AllowDynamic); Assert.False(role.IsTrap()); Assert.Equal(LegacyNativeMapObjectType.Monster,role.ObjType);
        Assert.Equal(320,role.BitVector.Size); Assert.Equal(new uint[10],role.BitVector.bits); Assert.Equal(LegacyNativeMobStatus.Idle,role.State);
        Assert.Null(role.Target); Assert.Null(role.FloorPacket); Assert.Null(role.OwnerFloor); Assert.Null(role.DBSpell); Assert.Empty(role.Hunters);
        Assert.Equal(default,role.RespawnStamp); Assert.InRange(role.RemoveFloor,before,after);
        Assert.Equal(9,role.BattlePower); Assert.Equal(17,role.ExtraDamage); Assert.Equal(-3,role.SizeAdd); Assert.Equal(hp != 0,role.Alive);
        role.X = 60; role.Y = 61; Assert.Equal((ushort)26,role.PX); Assert.Equal((ushort)25,role.PY);
    });

    [Theory]
    [InlineData("NemesisTyrant",4220u)] [InlineData("BloodyBanshee",3976u)]
    [InlineData("DragonWraith",3971u)] [InlineData("QueenofEvil",3970u)]
    [InlineData("RockMonsterKing",8311u)] [InlineData("NetherTyrant",3978u)]
    [InlineData("ThrillingSpook",4212u)] [InlineData("SnowBanshee",4171u)]
    [InlineData("FrostSerpent",2414u)] [InlineData("ChillingSpook",3977u)]
    [InlineData("BladeDevilLord",2417u)] [InlineData("BladeDevilKing",2418u)]
    [InlineData("BladeDevilEnvoy",2415u)] [InlineData("SerpentLord",2412u)]
    [InlineData("NightmareCaptain",7563u)] [InlineData("RedDevilL118",4858u)]
    [InlineData("MonkeyKing",8305u)] [InlineData("MonkeyMessenger",8105u)]
    [InlineData("MonkeyAide",8205u)] [InlineData("monkeyKing",300u)]
    public Task WholeSpawnWireUsesAllSourceNameOverrides(string name,uint expectedId) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var f = Family(); f.Name = name; var role = s.Monster(f); role.Action = LegacyNativePlayerMapFlags.ConquerAction.MagicDefender;
        role.BitVector.bits[0] = uint.MaxValue; role.BitVector.bits[9] = 0x80000000;
        s.Packet.Seek(91); s.Packet.Write(0xDEADBEEFu); var result = role.GetArray(s.Packet,true,true);
        Assert.Same(s.Packet,result); Assert.Equal(4,result.Position);
        Assert.Equal(Spawn(role.UID,100,15,26,25,name,expectedId,500,500,action:unchecked((ushort)273344),flags:[uint.MaxValue,0,0,0,0,0,0,0,0,0x80000000]),NativeSpellTestFixture.Bytes(result));
    });

    [Theory]
    [InlineData(20010u,2416u)] [InlineData(20000u,4860u)]
    public Task NumericOverridesFollowNameOverrides(uint familyId,uint expectedId) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var f = Family(familyId); f.Name = "MonkeyKing"; var role = s.Monster(f);
        role.GetArray(s.Packet,false,false);
        Assert.Equal(Spawn(role.UID,100,15,26,25,"MonkeyKing",expectedId,500,500),NativeSpellTestFixture.Bytes(s.Packet));
    });

    [Theory]
    [InlineData(500,500u,250000u)]
    [InlineData(10000,9000u,9000u)]
    [InlineData(20000,9000u,4500u)]
    [InlineData(0,99u,0u)]
    [InlineData(-1,4294967295u,1u)]
    public Task BossScalingIncludesNativeSmallAndNegativeHealthArithmetic(int max,uint hp,uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var f = Family(); f.MaxHealth = max; f.Boss = 1; var role = s.Monster(f); role.HitPoints = hp;
        role.GetArray(s.Packet,false,false);
        Assert.Equal(Spawn(role.UID,100,15,26,25,"Pheasant",300,expected,unchecked((uint)max),true),NativeSpellTestFixture.Bytes(s.Packet)); Assert.Equal(hp,role.HitPoints);
    });

    [Theory]
    [InlineData(980u,3u,4264u,true)]
    [InlineData(138u,19u,300u,false)]
    [InlineData(137u,19u,300u,false)]
    [InlineData(848u,19u,300u,false)]
    public Task EveryPetMeshUsesRealOwnerAndFloorLifetime(uint mesh,uint pet,uint id,bool floor) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var role = s.Monster(); role.Mesh = mesh; role.OwnerFloor = s.Client; role.PetFlag = 19;
        s.Player.Angle = LegacyNativePlayerMapFlags.ConquerAngle.South; s.Player.GuildID = 89; role.IsFloor = floor; role.StampFloorSecounds = -2;
        role.GetArray(s.Packet,false,false);
        Assert.Equal(Spawn(role.UID,mesh,15,26,25,"Pheasant",id,floor ? 4294967294u : 500u,500,owner:17,pet:pet,facing:7,guild:mesh == 980 ? 89u : 0u),NativeSpellTestFixture.Bytes(s.Packet));
        role.OwnerFloor = null; s.Packet.Seek(77); Assert.Throws<NullReferenceException>(() => role.GetArray(s.Packet,false,false)); Assert.Equal(4,s.Packet.Position);
    });

    [Fact]
    public Task FloorBranchDelegatesToRealFullFloorCodecAndPreservesNullFaultOrder() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var role = s.Monster(); role.IsFloor = true; role.Mesh = 138; role.Family = null!; role.BitVector = null!;
        role.FloorPacket = new() { m_UID=41,m_ID=42,m_X=43,m_Y=44,MaxLife=45,DropType=LegacyNativeMsgDropID.Effect,Life=46,m_Color=47,Plus=98,ItemOwnerUID=48,GuildID=49,FlowerType=50,Timer=51,OwnerX=52,OwnerY=53,Name="Wolf" };
        role.GetArray(s.Packet,true,true); Assert.Equal(FloorWire(role.FloorPacket),NativeSpellTestFixture.Bytes(s.Packet)); Assert.Equal(4,s.Packet.Position);
        role.FloorPacket = null; s.Packet.Seek(81); Assert.Throws<NullReferenceException>(() => role.GetArray(s.Packet,false,false)); Assert.Equal(4,s.Packet.Position);
    });

    private static byte[] FloorWire(LegacyNativeMsgItemPacket item)
    {
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms); writer.Write((ushort)0); writer.Write((ushort)2164);
        writer.Write(item.m_UID);writer.Write(item.m_ID);writer.Write(item.m_X);writer.Write(item.m_Y);writer.Write(item.MaxLife == 0 ? (ushort)40 : item.MaxLife);
        writer.Write((byte)item.DropType);writer.Write(item.Life);writer.Write(item.DropType == LegacyNativeMsgDropID.Effect ? item.m_Color : item.Plus);
        writer.Write(item.ItemOwnerUID);writer.Write(0);writer.Write(0);writer.Write(item.GuildID);writer.Write(item.FlowerType);writer.Write(item.Timer);writer.Write(0);writer.Write(0);
        writer.Write(item.OwnerX);writer.Write(item.OwnerY);writer.Write(new byte[40]);
        var name = new byte[32]; if (!string.IsNullOrEmpty(item.Name)) { var utf8 = System.Text.Encoding.UTF8.GetBytes(item.Name); Array.Copy(utf8,name,Math.Min(32,item.Name.Length)); }
        writer.Write(name);writer.Write(new byte[20]); var bytes=ms.ToArray(); BitConverter.GetBytes((ushort)bytes.Length).CopyTo(bytes,0);return bytes;
    }
}
