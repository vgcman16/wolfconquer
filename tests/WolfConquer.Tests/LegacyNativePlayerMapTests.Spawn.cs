using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WholeSpawnSerializesBothActualBranchesAndEveryOwnerFamily(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.SpawinEffect = effect;
        player.UID = 41; player.Name = "Wolf中文"; player.ClanName = "Clan";
        Set(player, "_class", 1005u); Set(player, "_level", (ushort)130); Set(player, "_hair", 12345u);
        Set(player, "_minhitpoints", -1); Set(player, "body", (ushort)1003); Set(player, "face", (ushort)123);
        player.HeadId = 11; player.GarmentId = 12; player.ArmorId = 13;
        player.LeftWeaponId = 14; player.RightWeaponId = 15; player.LeftWeapsonSoul = 16; player.RightWeapsonSoul = 17;
        player.LeftWeaponAccessoryId = 18; player.RightWeaponAccessoryId = 19; player.SteedId = 20; player.MountArmorId = 21;
        player.WingId = 22; player.WingPlus = 23; player.WingProgress = 24; player.Bottle = 25;
        player.AparenceType = 65537; player.GuildID = 27; player.GuildRank = LegacyNativeGuildMemberRank.Manager;
        player.X = 28; player.Y = 29; player.Angle = LegacyNativePlayerMapFlags.ConquerAngle.North;
        player.Action = LegacyNativePlayerMapFlags.ConquerAction.MagicDefender; player.Reborn = 2;
        player.FirstClass = 30; player.SecoundeClass = 31; player.Away = 2;
        player.ExtraBattlePower = 3; Set(player, "guildBP", 9u); player.FlowerRank = uint.MaxValue;
        player.NobilityRank = LegacyNativeNobilityRank.Earl;
        player.ArmorSoul = 32; player.HeadSoul = 33; player.SteedColor = 34; player.SteedPlus = 35;
        player.ClanUID = 36; player.ClanRank = 37; player.MyTitle = 38;
        player.ActiveSublass = (LegacyNativeDbLevExp.Sort)2; player.SubClassHasPoints = 39;
        player.JiangHuActive = 2; player.JiangHuTalent = 40;
        scope.Client.Status.MaxHitpoints = 41; player.ServerID = 42;
        player.MyUnion = new LegacyNativeUnionRuntime { UID = 43, Name = "Union", IsKingdom = 2 };
        player.UnionMemeber = new LegacyNativeUnionMember { Rank = LegacyNativeUnionMember.MilitaryRanks.LeftPre };
        Set(player, "_KingDomExploits", 44u); player.ExploitsRank = LegacyNativePlayerExploitsRank.General;
        player.RealUID = 45; player.QuizPoints = 46; player.SpecialWingID = 47; player.SpecialTitleID = 48;
        player.SpecialTitleScore = 49; player.CountryID = 50; player.MainFlag = (Player.MainFlagType)511; player.Enilghten = 51;
        scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Poker", 999) { Seat = 52, Table = new LegacyNativePokerTableRuntime(53) };
        player.InnerPower!.Stages = [new() { ID = 1, NeiGongs = [new() { Score = 54 }] }];
        player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Guild");
        player.BitVector.bits = [1, uint.MaxValue, 3]; player.CollectionID = 55;
        Assert.Same(scope.Packet, player.GetArray(scope.Packet, true, true));
        var message = Read(scope.Packet);
        Assert.Equal(41u, message.UID); Assert.Equal(1231003u, message.Mesh);
        Assert.Equal(11u, message.Head); Assert.Equal(12u, message.Garment); Assert.Equal(13u, message.Armor);
        Assert.Equal(14u, message.LeftWeapon); Assert.Equal(15u, message.RightWeapon);
        Assert.Equal(16u, message.LeftWeaponSoul); Assert.Equal(17u, message.RightWeaponSoul);
        Assert.Equal(18u, message.LeftWeaponAccessory); Assert.Equal(19u, message.RightWeaponAccessory);
        Assert.Equal(20u, message.Steed); Assert.Equal(21u, message.MountArmor);
        Assert.Equal(effect ? 0u : 22u, message.Wing); Assert.Equal(effect ? 0u : 23u, message.WingPlus);
        Assert.Equal(effect ? 0u : 24u, message.WingProgress); Assert.Equal(25u, message.Bottle);
        Assert.Equal((ushort)1, message.AppearanceType); Assert.Equal(27u, message.GuildID); Assert.Equal(3u, message.GuildRank);
        Assert.Equal(uint.MaxValue, message.Hitpoints); Assert.Equal((ushort)28, message.X); Assert.Equal((ushort)29, message.Y);
        Assert.Equal(12345u, message.HairStyle); Assert.Equal((byte)3, message.Facing);
        Assert.Equal(unchecked((ushort)273344), message.Action); Assert.Equal((byte)2, message.Reborn);
        Assert.Equal(30u, message.FirstRebornClass); Assert.Equal(31u, message.SecondRebornClass); Assert.Equal((ushort)130, message.Level);
        Assert.True(message.WindowSpawn); Assert.True(message.Away); Assert.Equal(unchecked(3u - 9u), message.ExtraBattlePower);
        Assert.Equal(9999u, message.FlowerIcon); Assert.Equal((byte)LegacyNativeNobilityRank.Earl, message.NobilityRank);
        Assert.Equal(32u, message.ArmorSoul); Assert.Equal(33u, message.HeadSoul);
        Assert.Equal(34u, message.SteedColor); Assert.Equal(35u, message.SteedPlus);
        Assert.Equal(36u, message.ClanUID); Assert.Equal((ushort)37, message.ClanRank); Assert.Equal(effect ? 0u : 38u, message.Title);
        Assert.Equal((byte)2, message.ActiveSubClasses); Assert.Equal(39u, message.SubClass);
        Assert.True(message.JiangHuActive); Assert.Equal((byte)40, message.JiangHuTalent);
        Assert.Equal(41u, message.MaxLife); Assert.Equal((ushort)42, message.ServerID);
        Assert.Equal(43u, message.UnionID); Assert.Equal(44u, message.UnionExploits); Assert.Equal(2u, message.UnionRank);
        Assert.Equal(2u, message.UnionType); Assert.Equal(45u, message.OwnerUID); Assert.Equal(player.BattlePower, message.BattlePower);
        Assert.Equal(46u, message.QuizPoints); Assert.Equal(9u, message.GuildBattlePower); Assert.Equal(1005u, message.Class);
        Assert.Equal(effect ? 0u : 47u, message.MyWing); Assert.Equal(effect ? 0u : 48u, message.MyTitle);
        Assert.Equal(effect ? 0u : 49u, message.MyTitleScore); Assert.Equal((ushort)50, message.CountryCode);
        Assert.Equal((byte)52, message.PokerSeat); Assert.Equal(53u, message.PokerTableID); Assert.Equal(54u, message.InnerStrengthScore);
        Assert.Equal(effect ? (byte)0 : (byte)255, message.MainFlag); Assert.Equal(51u, message.Enlighten);
        Assert.Equal(9u, message.Official_Harem_Guards);
        Assert.Equal(new[] { "Wolf中文", "", "Clan", "", "", "Guild", "Union" }, message.Names);
        Assert.Equal(new ulong[] { 1, uint.MaxValue, 3 }, message.StatusFlags); Assert.Equal(new uint[5], message.HundredWeapons);
        Assert.Equal(55u, message.CollectionID); Assert.Equal(1u, message.Test1); Assert.Equal(1u, message.Test2); Assert.True(message.UnknownBool);
        var raw = Scalars(NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(new ulong[] { 41 }, raw[2]); Assert.Equal(new ulong[] { uint.MaxValue }, raw[21]);
        Assert.Equal(new ulong[] { 1, uint.MaxValue, 3 }, raw[6]);
        Assert.Equal(new ulong[] { 1 }, raw[108]); Assert.Equal(new ulong[] { 55 }, raw[121]);
        Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SpawnReadsEffectChoiceFromCurrentOwnerPlayerAndIgnoresSt(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var subject = scope.Player;
        subject.SpawinEffect = !effect; subject.WingId = 456;
        scope.Client.Player = new Player(scope.Client) { SpawinEffect = effect };
        subject.GetArray(scope.Packet, false, false); byte[] first = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(effect ? 0u : 456u, Read(scope.Packet).Wing);
        Assert.False(Read(scope.Packet).WindowSpawn);
        subject.GetArray(scope.Packet, false, true);
        Assert.Equal(first, NativeSpellTestFixture.Bytes(scope.Packet));
    });
}
